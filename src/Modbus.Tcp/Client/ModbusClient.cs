using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AMWD.Modbus.Common;
using AMWD.Modbus.Common.Interfaces;
using AMWD.Modbus.Common.Structures;
using AMWD.Modbus.Common.Util;
using AMWD.Modbus.Tcp.Protocol;
using AMWD.Modbus.Tcp.Util;
using Microsoft.Extensions.Logging;

namespace AMWD.Modbus.Tcp.Client
{
	/// <summary>
	/// A client to communicate with modbus devices via TCP.
	/// </summary>
	public class ModbusClient : IModbusClient
	{
		#region Fields

		// Optional logger for all actions
		private readonly ILogger logger;

		// The tcp client to connect to the server side.
		private TcpClient tcpClient;

		// Connection handling
		private CancellationTokenSource mainCts;
		private CancellationTokenSource receivingCts;
		private bool isStarted = false;
		private Task receiveTask;

		// Reconnection parameters
		private bool wasConnected = false;
		private bool isReconnecting = false;
		private readonly int maxTimeout;

		// Transaction handling
		private readonly object syncLock = new object();
		private ushort transactionId = 0;
		private readonly ConcurrentDictionary<ushort, TaskCompletionSource<Response>> awaitedResponses = new ConcurrentDictionary<ushort, TaskCompletionSource<Response>>();

		#endregion Fields

		#region Events

		/// <summary>
		/// Raised when the client has the connection successfully established.
		/// </summary>
		public event EventHandler Connected;

		/// <summary>
		/// Raised when the client has closed the connection.
		/// </summary>
		public event EventHandler Disconnected;

		#endregion Events

		#region Constructors

		/// <summary>
		/// Initializes a new instance of the <see cref="ModbusClient"/> class.
		/// </summary>
		/// <param name="host">The remote host name or ip.</param>
		/// <param name="port">The remote port.</param>
		/// <param name="logger"><see cref="ILogger"/> instance to write log entries.</param>
		/// <param name="maxTimeout">The maximum number of seconds to wait between two reconnect attempts.</param>
		public ModbusClient(string host, int port = 502, ILogger logger = null, int maxTimeout = 30)
		{
			if (maxTimeout <= 0)
				throw new ArgumentOutOfRangeException(nameof(maxTimeout), "The maximum timeout hast to be greater than zero.");

			if (string.IsNullOrWhiteSpace(host))
				throw new ArgumentNullException(nameof(host), "Hostname has to be set.");

			if (port < 1 || port > 65535)
				throw new ArgumentOutOfRangeException(nameof(port), "Ports are limited from 1 to 65535.");

			this.logger = logger;
			this.maxTimeout = maxTimeout;
			Host = host;
			Port = port;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ModbusClient"/> class.
		/// </summary>
		/// <param name="host">The remote host name or ip.</param>
		/// <param name="port">The remote port.</param>
		/// <param name="logger"><see cref="ILogger{ModbusClient}"/> instance to write log entries.</param>
		public ModbusClient(string host, int port, ILogger<ModbusClient> logger)
			: this(host, port, (ILogger)logger)
		{ }

		/// <summary>
		/// Initializes a new instance of the <see cref="ModbusClient"/> class.
		/// </summary>
		/// <param name="address">The remote ip address.</param>
		/// <param name="port">The remote port.</param>
		/// <param name="logger"><see cref="ILogger"/> instance to write log entries.</param>
		public ModbusClient(IPAddress address, int port = 502, ILogger logger = null)
			: this(address.ToString(), port, logger)
		{ }

		/// <summary>
		/// Initializes a new instance of the <see cref="ModbusClient"/> class.
		/// </summary>
		/// <param name="address">The remote ip address.</param>
		/// <param name="port">The remote port.</param>
		/// <param name="logger"><see cref="ILogger{ModbusClient}"/> instance to write log entries.</param>
		public ModbusClient(IPAddress address, int port, ILogger<ModbusClient> logger)
			: this(address.ToString(), port, logger)
		{ }

		#endregion Constructors

		#region Properties

		/// <summary>
		/// Gets or sets the host name.
		/// </summary>
		public string Host { get; private set; }

		/// <summary>
		/// Gets or sets the port.
		/// </summary>
		public int Port { get; private set; }

		/// <summary>
		/// Gets the result of the asynchronous initialization of this instance.
		/// </summary>
		public Task ConnectingTask { get; private set; }

		/// <summary>
		/// Gets a value indicating whether the connection is established.
		/// </summary>
		public bool IsConnected => !isReconnecting && (tcpClient?.Connected ?? false);

		/// <summary>
		/// Gets or sets the max. reconnect timespan until the reconnect is aborted.
		/// </summary>
		public TimeSpan ReconnectTimeSpan { get; set; } = TimeSpan.MaxValue;

		/// <summary>
		/// Gets or sets the send timeout in milliseconds. Default: 1000.
		/// </summary>
		public int SendTimeout { get; set; } = 1000;

		/// <summary>
		/// Gets ors sets the receive timeout in milliseconds. Default: 1000;
		/// </summary>
		public int ReceiveTimeout { get; set; } = 1000;

		#endregion Properties

		#region Public Methods

		#region Control

		/// <summary>
		/// Connects the client to the server.
		/// </summary>
		/// <returns>An awaitable task.</returns>
		public Task Connect()
		{
			logger?.LogTrace("ModbusClient.Connect");
			if (isDisposed)
				throw new ObjectDisposedException(GetType().FullName);

			if (isStarted)
				return ConnectingTask;

			isStarted = true;
			logger?.LogInformation("ModbusClient.Connect: ModbusClient starting");

			wasConnected = false;
			mainCts = new CancellationTokenSource();

			Task.Run(() => Reconnect(mainCts.Token));

			ConnectingTask = GetWaitTask(mainCts.Token);
			logger?.LogInformation("ModbusClient.Connect: ModbusClient started");
			return ConnectingTask;
		}

		/// <summary>
		/// Disconnects the client.
		/// </summary>
		/// <returns>An awaitable task.</returns>
		public Task Disconnect()
		{
			return Task.Run(() => DisconnectInternal(false));
		}

		#endregion Control

		#region Read Methods

		/// <summary>
		/// Reads one or more coils of a device. (Modbus function 1).
		/// </summary>
		/// <param name="deviceId">The id to address the device (slave).</param>
		/// <param name="startAddress">The first coil number to read.</param>
		/// <param name="count">The number of coils to read.</param>
		/// <param name="cancellationToken">A cancellation token to abort the action.</param>
		/// <returns>A list of coils or null on error.</returns>
		public async Task<List<Coil>> ReadCoils(byte deviceId, ushort startAddress, ushort count, CancellationToken cancellationToken = default)
		{
			logger?.LogTrace($"ModbusClient.ReadCoils({deviceId}, {startAddress}, {count})");

			if (deviceId < Consts.MinDeviceIdTcp || Consts.MaxDeviceId < deviceId)
				throw new ArgumentOutOfRangeException(nameof(deviceId));

			if (startAddress < Consts.MinAddress || Consts.MaxAddress < startAddress + count)
				throw new ArgumentOutOfRangeException(nameof(startAddress));

			if (count < Consts.MinCount || Consts.MaxCoilCountRead < count)
				throw new ArgumentOutOfRangeException(nameof(count));

			List<Coil> list = null;
			try
			{
				var request = new Request
				{
					DeviceId = deviceId,
					Function = FunctionCode.ReadCoils,
					Address = startAddress,
					Count = count
				};
				var response = await SendRequest(request, cancellationToken);
				if (response.IsTimeout)
					throw new SocketException((int)SocketError.TimedOut);

				if (response.IsError)
				{
					throw new ModbusException(response.ErrorMessage)
					{
						ErrorCode = response.ErrorCode
					};
				}
				if (request.TransactionId != response.TransactionId)
					throw new ModbusException(nameof(response.TransactionId) + " does not match");

				list = new List<Coil>();
				for (int i = 0; i < count; i++)
				{
					int posByte = i / 8;
					int posBit = i % 8;

					int val = response.Data[posByte] & (byte)Math.Pow(2, posBit);

					list.Add(new Coil
					{
						Address = (ushort)(startAddress + i),
						BoolValue = val > 0
					});
				}
			}
			catch (SocketException ex)
			{
				logger?.LogWarning(ex, "ModbusClient.ReadCoils failed. Reconnecting.");
				Task.Run(() => Reconnect(mainCts.Token)).Forget();
				ConnectingTask = GetWaitTask(mainCts.Token);
			}
			catch (IOException ex)
			{
				logger?.LogWarning(ex, "ModbusClient.ReadCoils failed. Reconnecting.");
				Task.Run(() => Reconnect(mainCts.Token)).Forget();
				ConnectingTask = GetWaitTask(mainCts.Token);
			}

			return list;
		}

		/// <summary>
		/// Reads one or more discrete inputs of a device. (Modbus function 2).
		/// </summary>
		/// <param name="deviceId">The id to address the device (slave).</param>
		/// <param name="startAddress">The first discrete input number to read.</param>
		/// <param name="count">The number of discrete inputs to read.</param>
		/// <param name="cancellationToken">A cancellation token to abort the action.</param>
		/// <returns>A list of discrete inputs or null on error.</returns>
		public async Task<List<DiscreteInput>> ReadDiscreteInputs(byte deviceId, ushort startAddress, ushort count, CancellationToken cancellationToken = default)
		{
			logger?.LogTrace($"ModbusClient.ReadDiscreteInputs({deviceId}, {startAddress}, {count})");

			if (deviceId < Consts.MinDeviceIdTcp || Consts.MaxDeviceId < deviceId)
				throw new ArgumentOutOfRangeException(nameof(deviceId));

			if (startAddress < Consts.MinAddress || Consts.MaxAddress < startAddress + count)
				throw new ArgumentOutOfRangeException(nameof(startAddress));

			if (count < Consts.MinCount || Consts.MaxCoilCountRead < count)
				throw new ArgumentOutOfRangeException(nameof(count));

			List<DiscreteInput> list = null;
			try
			{
				var request = new Request
				{
					DeviceId = deviceId,
					Function = FunctionCode.ReadDiscreteInputs,
					Address = startAddress,
					Count = count
				};
				var response = await SendRequest(request, cancellationToken);
				if (response.IsTimeout)
					throw new SocketException((int)SocketError.TimedOut);

				if (response.IsError)
				{
					throw new ModbusException(response.ErrorMessage)
					{
						ErrorCode = response.ErrorCode
					};
				}
				if (request.TransactionId != response.TransactionId)
					throw new ModbusException(nameof(response.TransactionId) + " does not match");

				list = new List<DiscreteInput>();
				for (int i = 0; i < count; i++)
				{
					int posByte = i / 8;
					int posBit = i % 8;

					int val = response.Data[posByte] & (byte)Math.Pow(2, posBit);

					list.Add(new DiscreteInput
					{
						Address = (ushort)(startAddress + i),
						BoolValue = val > 0
					});
				}
			}
			catch (SocketException ex)
			{
				logger?.LogWarning(ex, "ModbusClient.ReadDiscreteInputs failed. Reconnecting.");
				Task.Run(() => Reconnect(mainCts.Token)).Forget();
				ConnectingTask = GetWaitTask(mainCts.Token);
			}
			catch (IOException ex)
			{
				logger?.LogWarning(ex, "ModbusClient.ReadDiscreteInputs failed. Reconnecting.");
				Task.Run(() => Reconnect(mainCts.Token)).Forget();
				ConnectingTask = GetWaitTask(mainCts.Token);
			}

			return list;
		}

		/// <summary>
		/// Reads one or more holding registers of a device. (Modbus function 3).
		/// </summary>
		/// <param name="deviceId">The id to address the device (slave).</param>
		/// <param name="startAddress">The first register number to read.</param>
		/// <param name="count">The number of registers to read.</param>
		/// <param name="cancellationToken">A cancellation token to abort the action.</param>
		/// <returns>A list of registers or null on error.</returns>
		public async Task<List<Register>> ReadHoldingRegisters(byte deviceId, ushort startAddress, ushort count, CancellationToken cancellationToken = default)
		{
			logger?.LogTrace($"ModbusClient.ReadHoldingRegisters({deviceId}, {startAddress}, {count})");

			if (deviceId < Consts.MinDeviceIdTcp || Consts.MaxDeviceId < deviceId)
				throw new ArgumentOutOfRangeException(nameof(deviceId));

			if (startAddress < Consts.MinAddress || Consts.MaxAddress < startAddress + count)
				throw new ArgumentOutOfRangeException(nameof(startAddress));

			if (count < Consts.MinCount || Consts.MaxRegisterCountRead < count)
				throw new ArgumentOutOfRangeException(nameof(count));

			List<Register> list = null;
			try
			{
				var request = new Request
				{
					DeviceId = deviceId,
					Function = FunctionCode.ReadHoldingRegisters,
					Address = startAddress,
					Count = count
				};
				var response = await SendRequest(request, cancellationToken);
				if (response.IsTimeout)
					throw new SocketException((int)SocketError.TimedOut);

				if (response.IsError)
				{
					throw new ModbusException(response.ErrorMessage)
					{
						ErrorCode = response.ErrorCode
					};
				}
				if (request.TransactionId != response.TransactionId)
					throw new ModbusException(nameof(response.TransactionId) + " does not match");

				list = new List<Register>();
				for (int i = 0; i < count; i++)
				{
					list.Add(new Register
					{
						Type = ModbusObjectType.HoldingRegister,
						Address = (ushort)(startAddress + i),
						HiByte = response.Data[i * 2],
						LoByte = response.Data[i * 2 + 1]
					});
				}
			}
			catch (SocketException ex)
			{
				logger?.LogWarning(ex, "ModbusClient.ReadHoldingRegisters failed. Reconnecting.");
				Task.Run(() => Reconnect(mainCts.Token)).Forget();
				ConnectingTask = GetWaitTask(mainCts.Token);
			}
			catch (IOException ex)
			{
				logger?.LogWarning(ex, "ModbusClient.ReadHoldingRegisters failed. Reconnecting.");
				Task.Run(() => Reconnect(mainCts.Token)).Forget();
				ConnectingTask = GetWaitTask(mainCts.Token);
			}

			return list;
		}

		/// <summary>
		/// Reads one or more input registers of a device. (Modbus function 4).
		/// </summary>
		/// <param name="deviceId">The id to address the device (slave).</param>
		/// <param name="startAddress">The first register number to read.</param>
		/// <param name="count">The number of registers to read.</param>
		/// <param name="cancellationToken">A cancellation token to abort the action.</param>
		/// <returns>A list of registers or null on error.</returns>
		public async Task<List<Register>> ReadInputRegisters(byte deviceId, ushort startAddress, ushort count, CancellationToken cancellationToken = default)
		{
			logger?.LogTrace($"ModbusClient.ReadInputRegisters({deviceId}, {startAddress}, {count})");

			if (deviceId < Consts.MinDeviceIdTcp || Consts.MaxDeviceId < deviceId)
				throw new ArgumentOutOfRangeException(nameof(deviceId));

			if (startAddress < Consts.MinAddress || Consts.MaxAddress < startAddress + count)
				throw new ArgumentOutOfRangeException(nameof(startAddress));

			if (count < Consts.MinCount || Consts.MaxRegisterCountRead < count)
				throw new ArgumentOutOfRangeException(nameof(count));

			List<Register> list = null;
			try
			{
				var request = new Request
				{
					DeviceId = deviceId,
					Function = FunctionCode.ReadInputRegisters,
					Address = startAddress,
					Count = count
				};
				var response = await SendRequest(request, cancellationToken);
				if (response.IsTimeout)
					throw new SocketException((int)SocketError.TimedOut);

				if (response.IsError)
				{
					throw new ModbusException(response.ErrorMessage)
					{
						ErrorCode = response.ErrorCode
					};
				}
				if (request.TransactionId != response.TransactionId)
					throw new ModbusException(nameof(response.TransactionId) + " does not match");

				list = new List<Register>();
				for (int i = 0; i < count; i++)
				{
					list.Add(new Register
					{
						Type = ModbusObjectType.InputRegister,
						Address = (ushort)(startAddress + i),
						HiByte = response.Data[i * 2],
						LoByte = response.Data[i * 2 + 1]
					});
				}
			}
			catch (SocketException ex)
			{
				logger?.LogWarning(ex, "ModbusClient.ReadInputRegisters failed. Reconnecting.");
				Task.Run(() => Reconnect(mainCts.Token)).Forget();
				ConnectingTask = GetWaitTask(mainCts.Token);
			}
			catch (IOException ex)
			{
				logger?.LogWarning(ex, "ModbusClient.ReadInputRegisters failed. Reconnecting.");
				Task.Run(() => Reconnect(mainCts.Token)).Forget();
				ConnectingTask = GetWaitTask(mainCts.Token);
			}

			return list;
		}

		/// <summary>
		/// Reads device information. (Modbus function 43).
		/// </summary>
		/// <param name="deviceId">The id to address the device (slave).</param>
		/// <param name="categoryId">The category to read (basic, regular, extended, individual).</param>
		/// <param name="objectId">The first object id to read.</param>
		/// <param name="cancellationToken">A cancellation token to abort the action.</param>
		/// <returns>A map of device information and their content as string.</returns>
		public async Task<Dictionary<DeviceIDObject, string>> ReadDeviceInformation(byte deviceId, DeviceIDCategory categoryId, DeviceIDObject objectId = DeviceIDObject.VendorName, CancellationToken cancellationToken = default)
		{
			var raw = await ReadDeviceInformationRaw(deviceId, categoryId, objectId, cancellationToken);
			if (raw == null)
				return null;

			var dict = new Dictionary<DeviceIDObject, string>();
			foreach (var kvp in raw)
			{
				dict.Add((DeviceIDObject)kvp.Key, Encoding.ASCII.GetString(kvp.Value));
			}
			return dict;
		}

		/// <summary>
		/// Reads device information. (Modbus function 43).
		/// </summary>
		/// <param name="deviceId">The id to address the device (slave).</param>
		/// <param name="categoryId">The category to read (basic, regular, extended, individual).</param>
		/// <param name="objectId">The first object id to read.</param>
		/// <param name="cancellationToken">A cancellation token to abort the action.</param>
		/// <returns>A map of device information and their content as raw bytes.</returns>
		public async Task<Dictionary<byte, byte[]>> ReadDeviceInformationRaw(byte deviceId, DeviceIDCategory categoryId, DeviceIDObject objectId = DeviceIDObject.VendorName, CancellationToken cancellationToken = default)
		{
			logger?.LogTrace($"ModbusClient.ReadDeviceInformation({deviceId}, {categoryId}, {objectId})");

			if (deviceId < Consts.MinDeviceIdTcp || Consts.MaxDeviceId < deviceId)
				throw new ArgumentOutOfRangeException(nameof(deviceId));

			try
			{
				var request = new Request
				{
					DeviceId = deviceId,
					Function = FunctionCode.EncapsulatedInterface,
					MEIType = MEIType.ReadDeviceInformation,
					MEICategory = categoryId,
					MEIObject = objectId
				};
				var response = await SendRequest(request, cancellationToken);
				if (response.IsTimeout)
					throw new SocketException((int)SocketError.TimedOut);

				if (response.IsError)
				{
					throw new ModbusException(response.ErrorMessage)
					{
						ErrorCode = response.ErrorCode
					};
				}
				if (request.TransactionId != response.TransactionId)
					throw new ModbusException(nameof(response.TransactionId) + " does not match");

				var dict = new Dictionary<byte, byte[]>();
				for (int i = 0, idx = 0; i < response.ObjectCount && idx < response.Data.Length; i++)
				{
					byte objId = response.Data.GetByte(idx);
					idx++;
					byte len = response.Data.GetByte(idx);
					idx++;
					byte[] bytes = response.Data.GetBytes(idx, len);
					idx += len;

					dict.Add(objId, bytes);
				}

				if (response.MoreRequestsNeeded)
				{
					var transDict = await ReadDeviceInformationRaw(deviceId, categoryId, (DeviceIDObject)response.NextObjectId);
					foreach (var kvp in transDict)
					{
						dict.Add(kvp.Key, kvp.Value);
					}
				}

				return dict;
			}
			catch (SocketException ex)
			{
				logger?.LogWarning(ex, "ModbusClient.ReadDeviceInformation failed. Reconnecting.");
				Task.Run(() => Reconnect(mainCts.Token)).Forget();
				ConnectingTask = GetWaitTask(mainCts.Token);
			}
			catch (IOException ex)
			{
				logger?.LogWarning(ex, "ModbusClient.ReadDeviceInformation failed. Reconnecting.");
				Task.Run(() => Reconnect(mainCts.Token)).Forget();
				ConnectingTask = GetWaitTask(mainCts.Token);
			}

			return null;
		}

		#endregion Read Methods

		#region Write Methods

		/// <summary>
		/// Writes a single coil status to the Modbus device. (Modbus function 5)
		/// </summary>
		/// <param name="deviceId">The id to address the device (slave).</param>
		/// <param name="coil">The coil to write.</param>
		/// <param name="cancellationToken">A cancellation token to abort the action.</param>
		/// <returns>true on success, otherwise false.</returns>
		public async Task<bool> WriteSingleCoil(byte deviceId, ModbusObject coil, CancellationToken cancellationToken = default)
		{
			logger?.LogTrace($"ModbusClient.WriteSingleCoil({deviceId}, {coil})");

			if (coil == null)
				throw new ArgumentNullException(nameof(coil));

			if (coil.Type != ModbusObjectType.Coil)
				throw new ArgumentException("Invalid coil type set");

			if (deviceId < Consts.MinDeviceIdTcp || Consts.MaxDeviceId < deviceId)
				throw new ArgumentOutOfRangeException(nameof(deviceId));

			if (coil.Address < Consts.MinAddress || Consts.MaxAddress < coil.Address)
				throw new ArgumentOutOfRangeException(nameof(coil.Address));

			try
			{
				var request = new Request
				{
					DeviceId = deviceId,
					Function = FunctionCode.WriteSingleCoil,
					Address = coil.Address,
					Data = new DataBuffer(2)
				};
				ushort value = (ushort)(coil.BoolValue ? 0xFF00 : 0x0000);
				request.Data.SetUInt16(0, value);
				var response = await SendRequest(request, cancellationToken);
				if (response.IsTimeout)
					throw new SocketException((int)SocketError.TimedOut);

				if (response.IsError)
				{
					throw new ModbusException(response.ErrorMessage)
					{
						ErrorCode = response.ErrorCode
					};
				}
				if (request.TransactionId != response.TransactionId)
					throw new ModbusException(nameof(response.TransactionId) + " does not match");

				return request.TransactionId == response.TransactionId &&
					request.DeviceId == response.DeviceId &&
					request.Function == response.Function &&
					request.Address == response.Address &&
					request.Data.Equals(response.Data);
			}
			catch (SocketException ex)
			{
				logger?.LogWarning(ex, "ModbusClient.WriteSingleCoil failed. Reconnecting.");
				Task.Run(() => Reconnect(mainCts.Token)).Forget();
				ConnectingTask = GetWaitTask(mainCts.Token);
			}
			catch (IOException ex)
			{
				logger?.LogWarning(ex, "ModbusClient.WriteSingleCoil failed. Reconnecting.");
				Task.Run(() => Reconnect(mainCts.Token)).Forget();
				ConnectingTask = GetWaitTask(mainCts.Token);
			}

			return false;
		}

		/// <summary>
		/// Writes a single register to the Modbus device. (Modbus function 6)
		/// </summary>
		/// <param name="deviceId">The id to address the device (slave).</param>
		/// <param name="register">The register to write.</param>
		/// <param name="cancellationToken">A cancellation token to abort the action.</param>
		/// <returns>true on success, otherwise false.</returns>
		public async Task<bool> WriteSingleRegister(byte deviceId, ModbusObject register, CancellationToken cancellationToken = default)
		{
			logger?.LogTrace($"ModbusClient.WriteSingleRegister({deviceId}, {register})");
			if (isDisposed)
				throw new ObjectDisposedException(GetType().FullName);

			if (register == null)
				throw new ArgumentNullException(nameof(register));

			if (register.Type != ModbusObjectType.HoldingRegister)
				throw new ArgumentException("Invalid register type set");

			if (deviceId < Consts.MinDeviceIdTcp || Consts.MaxDeviceId < deviceId)
				throw new ArgumentOutOfRangeException(nameof(deviceId));

			if (register.Address < Consts.MinAddress || Consts.MaxAddress < register.Address)
				throw new ArgumentOutOfRangeException(nameof(register.Address));

			try
			{
				var request = new Request
				{
					DeviceId = deviceId,
					Function = FunctionCode.WriteSingleRegister,
					Address = register.Address,
					Data = new DataBuffer(new[] { register.HiByte, register.LoByte })
				};
				var response = await SendRequest(request,cancellationToken);
				if (response.IsTimeout)
					throw new SocketException((int)SocketError.TimedOut);

				if (response.IsError)
				{
					throw new ModbusException(response.ErrorMessage)
					{
						ErrorCode = response.ErrorCode
					};
				}
				if (request.TransactionId != response.TransactionId)
					throw new ModbusException(nameof(response.TransactionId) + " does not match");

				return request.TransactionId == response.TransactionId &&
					request.DeviceId == response.DeviceId &&
					request.Function == response.Function &&
					request.Address == response.Address &&
					request.Data.Equals(response.Data);
			}
			catch (SocketException ex)
			{
				logger?.LogWarning(ex, "ModbusClient.WriteSingleRegister failed. Reconnecting.");
				Task.Run(() => Reconnect(mainCts.Token)).Forget();
				ConnectingTask = GetWaitTask(mainCts.Token);
			}
			catch (IOException ex)
			{
				logger?.LogWarning(ex, "ModbusClient.WriteSingleRegister failed. Reconnecting.");
				Task.Run(() => Reconnect(mainCts.Token)).Forget();
				ConnectingTask = GetWaitTask(mainCts.Token);
			}

			return false;
		}

		/// <summary>
		/// Writes multiple coil status to the Modbus device. (Modbus function 15)
		/// </summary>
		/// <param name="deviceId">The id to address the device (slave).</param>
		/// <param name="coils">A list of coils to write.</param>
		/// <param name="cancellationToken">A cancellation token to abort the action.</param>
		/// <returns>true on success, otherwise false.</returns>
		public async Task<bool> WriteCoils(byte deviceId, IEnumerable<ModbusObject> coils, CancellationToken cancellationToken = default)
		{
			logger?.LogTrace($"ModbusClient.WriteCoils({deviceId}, Length: {coils.Count()})");

			if (coils == null || !coils.Any())
				throw new ArgumentNullException(nameof(coils));

			if (coils.Any(c => c.Type != ModbusObjectType.Coil))
				throw new ArgumentException("Invalid coil type set");

			if (deviceId < Consts.MinDeviceIdTcp || Consts.MaxDeviceId < deviceId)
				throw new ArgumentOutOfRangeException(nameof(deviceId));

			var orderedList = coils.OrderBy(c => c.Address).ToList();
			if (orderedList.Count < Consts.MinCount || Consts.MaxCoilCountWrite < orderedList.Count)
				throw new ArgumentOutOfRangeException("Count");

			ushort firstAddress = orderedList.First().Address;
			ushort lastAddress = orderedList.Last().Address;

			if (firstAddress + orderedList.Count - 1 != lastAddress)
				throw new ArgumentException("No address gabs allowed within a request");

			if (firstAddress < Consts.MinAddress || Consts.MaxAddress < lastAddress)
				throw new ArgumentOutOfRangeException("Address");

			int numBytes = (int)Math.Ceiling(orderedList.Count / 8.0);
			byte[] coilBytes = new byte[numBytes];
			for (int i = 0; i < orderedList.Count; i++)
			{
				if (orderedList[i].BoolValue)
				{
					int posByte = i / 8;
					int posBit = i % 8;

					byte mask = (byte)Math.Pow(2, posBit);
					coilBytes[posByte] = (byte)(coilBytes[posByte] | mask);
				}
			}

			try
			{
				var request = new Request
				{
					DeviceId = deviceId,
					Function = FunctionCode.WriteMultipleCoils,
					Address = firstAddress,
					Count = (ushort)orderedList.Count,
					Data = new DataBuffer(coilBytes)
				};
				var response = await SendRequest(request, cancellationToken);
				if (response.IsTimeout)
					throw new SocketException((int)SocketError.TimedOut);

				if (response.IsError)
				{
					throw new ModbusException(response.ErrorMessage)
					{
						ErrorCode = response.ErrorCode
					};
				}
				if (request.TransactionId != response.TransactionId)
					throw new ModbusException(nameof(response.TransactionId) + " does not match");

				return request.TransactionId == response.TransactionId &&
					request.Address == response.Address &&
					request.Count == response.Count;
			}
			catch (SocketException ex)
			{
				logger?.LogWarning(ex, "ModbusClient.WriteCoils failed. Reconnecting.");
				Task.Run(() => Reconnect(mainCts.Token)).Forget();
				ConnectingTask = GetWaitTask(mainCts.Token);
			}
			catch (IOException ex)
			{
				logger?.LogWarning(ex, "ModbusClient.WriteCoils failed. Reconnecting.");
				Task.Run(() => Reconnect(mainCts.Token)).Forget();
				ConnectingTask = GetWaitTask(mainCts.Token);
			}

			return false;
		}

		/// <summary>
		/// Writes multiple registers to the Modbus device. (Modbus function 16)
		/// </summary>
		/// <param name="deviceId">The id to address the device (slave).</param>
		/// <param name="registers">A list of registers to write.</param>
		/// <param name="cancellationToken">A cancellation token to abort the action.</param>
		/// <returns>true on success, otherwise false.</returns>
		public async Task<bool> WriteRegisters(byte deviceId, IEnumerable<ModbusObject> registers, CancellationToken cancellationToken = default)
		{
			logger?.LogTrace($"ModbusClient.WriteRegisters({deviceId}, Length: {registers.Count()})");
			if (isDisposed)
				throw new ObjectDisposedException(GetType().FullName);

			if (registers == null || !registers.Any())
				throw new ArgumentNullException(nameof(registers));

			if (registers.Any(r => r.Type != ModbusObjectType.HoldingRegister))
				throw new ArgumentException("Invalid register type set");

			if (deviceId < Consts.MinDeviceIdTcp || Consts.MaxDeviceId < deviceId)
				throw new ArgumentOutOfRangeException(nameof(deviceId));

			var orderedList = registers.OrderBy(c => c.Address).ToList();
			if (orderedList.Count < Consts.MinCount || Consts.MaxRegisterCountWrite < orderedList.Count)
				throw new ArgumentOutOfRangeException("Count");

			ushort firstAddress = orderedList.First().Address;
			ushort lastAddress = orderedList.Last().Address;

			if (firstAddress + orderedList.Count - 1 != lastAddress)
				throw new ArgumentException("No address gabs allowed within a request");

			if (firstAddress < Consts.MinAddress || Consts.MaxAddress < lastAddress)
				throw new ArgumentOutOfRangeException("Address");

			var data = new DataBuffer(orderedList.Count * 2);
			for (int i = 0; i < orderedList.Count; i++)
			{
				data.SetUInt16(i * 2, orderedList[i].RegisterValue);
			}

			try
			{
				var request = new Request
				{
					DeviceId = deviceId,
					Function = FunctionCode.WriteMultipleRegisters,
					Address = firstAddress,
					Count = (ushort)orderedList.Count,
					Data = data
				};
				var response = await SendRequest(request, cancellationToken);
				if (response.IsTimeout)
					throw new SocketException((int)SocketError.TimedOut);

				if (response.IsError)
				{
					throw new ModbusException(response.ErrorMessage)
					{
						ErrorCode = response.ErrorCode
					};
				}
				if (request.TransactionId != response.TransactionId)
					throw new ModbusException(nameof(response.TransactionId) + " does not match");

				return request.TransactionId == response.TransactionId &&
					request.Address == response.Address &&
					request.Count == response.Count;
			}
			catch (SocketException ex)
			{
				logger?.LogWarning(ex, "ModbusClient.WriteRegisters failed. Reconnecting.");
				Task.Run(() => Reconnect(mainCts.Token)).Forget();
				ConnectingTask = GetWaitTask(mainCts.Token);
			}
			catch (IOException ex)
			{
				logger?.LogWarning(ex, "ModbusClient.WriteRegisters failed. Reconnecting.");
				Task.Run(() => Reconnect(mainCts.Token)).Forget();
				ConnectingTask = GetWaitTask(mainCts.Token);
			}

			return false;
		}

		#endregion Write Methods

		#endregion Public Methods

		#region Private Methods

		private async Task ReceiveLoop(CancellationToken cancellationToken)
		{
			logger?.LogInformation("ModbusClient.ReceiveLoop started");
			bool reported = false;

			while (!cancellationToken.IsCancellationRequested)
			{
				try
				{
					var stream = tcpClient?.GetStream();
					if (stream == null)
					{
						if (!reported)
							logger?.LogTrace("ModbusClient.ReceiveLoop got no stream, waiting...");

						reported = true;
						await Task.Delay(100);
						continue;
					}
					if (reported)
					{
						logger?.LogTrace("ModbusClient.ReceiveLoop stream available");
						reported = false;
					}

					SpinWait.SpinUntil(() => cancellationToken.IsCancellationRequested || stream.DataAvailable);
					if (cancellationToken.IsCancellationRequested)
					{
						break;
					}

					var bytes = new List<byte>();

					int expectedCount = 6;
					do
					{
						byte[] buffer = new byte[6];
						int count = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
						bytes.AddRange(buffer.Take(count));
						expectedCount -= count;
					}
					while (expectedCount > 0 && !cancellationToken.IsCancellationRequested);
					if (cancellationToken.IsCancellationRequested)
						break;

					byte[] lenBytes = bytes.Skip(4).Take(2).ToArray();
					if (BitConverter.IsLittleEndian)
						Array.Reverse(lenBytes);

					expectedCount = BitConverter.ToUInt16(lenBytes, 0);

					do
					{
						byte[] buffer = new byte[expectedCount];
						int count = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
						bytes.AddRange(buffer.Take(count));
						expectedCount -= count;
					}
					while (expectedCount > 0 && !cancellationToken.IsCancellationRequested);
					if (cancellationToken.IsCancellationRequested)
					{
						break;
					}

					var response = new Response(bytes.ToArray());
					if (awaitedResponses.TryRemove(response.TransactionId, out var tcs))
					{
						tcs.TrySetResultAsync(response);
					}
				}
				catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
				{
					// stream already gone
				}
				catch (InvalidOperationException) when (isReconnecting)
				{
					// connection broken
				}
				catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
				{
					// dis- or reconnect
				}
				catch (Exception ex)
				{
					logger?.LogError(ex, "ModbusClient.ReceiveLoop");
				}
			}

			foreach (var awaitedResponse in awaitedResponses.Values)
			{
				awaitedResponse.TrySetCanceled();
			}
			awaitedResponses.Clear();
			logger?.LogInformation("ModbusClient.ReceiveLoop stopped");
		}

		private async Task<Response> SendRequest(Request request, CancellationToken ct)
		{
			if (isDisposed)
				throw new ObjectDisposedException(GetType().FullName);

			if (!IsConnected)
			{
				if (!isReconnecting)
				{
					Task.Run(() => Reconnect(mainCts.Token)).Forget();
					ConnectingTask = GetWaitTask(mainCts.Token);
				}

				throw new InvalidOperationException("Client is not connected");
			}

			var stream = tcpClient.GetStream();

			var tcs = new TaskCompletionSource<Response>();

			try
			{
				using (var cts = new CancellationTokenSource())
				using (cts.Token.Register(() => tcs.TrySetCanceled()))
				using (ct.Register(() => cts.Cancel()))
				using (mainCts.Token.Register(() => cts.Cancel()))
				{
					try
					{
						lock (syncLock)
						{
							request.TransactionId = transactionId;
							transactionId++;

							awaitedResponses[request.TransactionId] = tcs;
						}

						logger?.LogTrace(request.ToString());

						byte[] bytes = request.Serialize();
						var task = stream.WriteAsync(bytes, 0, bytes.Length, cts.Token);
						if (await Task.WhenAny(task, Task.Delay(SendTimeout, cts.Token)) == task && !cts.Token.IsCancellationRequested)
						{
							logger?.LogTrace("ModbusClient.SendRequest - Request sent");
							if (await Task.WhenAny(tcs.Task, Task.Delay(ReceiveTimeout, cts.Token)) == tcs.Task && !cts.Token.IsCancellationRequested)
							{
								logger?.LogTrace("ModbusClient.SendRequest - Response received");
								var response = await tcs.Task;
								return response;
							}
						}
					}
					catch (Exception ex)
					{
						logger?.LogError(ex, $"ModbusClient.SendRequest - Transaction {request.TransactionId}");
					}
				}
			}
			catch (OperationCanceledException) when (mainCts.IsCancellationRequested)
			{
				// keep it quiet on shutdown
			}
			return new Response(new byte[] { 0, 0, 0, 0, 0, 0 });
		}

		private async Task Reconnect(CancellationToken ct)
		{
			if (isReconnecting || ct.IsCancellationRequested)
				return;

			logger?.LogInformation("ModbusClient.Reconnect started");
			isReconnecting = true;

			if (wasConnected)
			{
				receivingCts?.Cancel();
				receiveTask = null;
				Task.Run(() => Disconnected?.Invoke(this, EventArgs.Empty)).Forget();
			}

			ConnectingTask = GetWaitTask(ct);
			int timeout = 2;
			var startTime = DateTime.UtcNow;

			try
			{
				while (!ct.IsCancellationRequested)
				{
					try
					{
						tcpClient?.Dispose();
						tcpClient = new TcpClient(AddressFamily.InterNetworkV6);
						tcpClient.Client.DualMode = true;

						var task = tcpClient.ConnectAsync(Host, Port);
						if (await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(timeout), ct)) == task && tcpClient.Connected)
						{
							logger?.LogInformation("ModbusClient.Reconnect connected");
							Task.Run(() => Connected?.Invoke(this, EventArgs.Empty)).Forget();
							wasConnected = true;

							receivingCts = new CancellationTokenSource();
							receiveTask = Task.Run(() => ReceiveLoop(receivingCts.Token));
							return;
						}
						else if (ct.IsCancellationRequested)
						{
							logger?.LogInformation("ModbusClient.Reconnect was cancelled");
							return;
						}
						else
						{
							timeout += 2;

							if (timeout > maxTimeout)
							{
								timeout = maxTimeout;
							}
							else
							{
								logger?.LogWarning($"ModbusClient.Reconnect failed to connect within {timeout - 2} seconds.");
							}

							throw new SocketException((int)SocketError.TimedOut);
						}
					}
					catch (SocketException) when (ReconnectTimeSpan == TimeSpan.MaxValue || DateTime.UtcNow <= startTime + ReconnectTimeSpan)
					{
						await Task.Delay(1000, ct);
						continue;
					}
					catch (Exception ex)
					{
						logger?.LogError(ex, "ModbusClient.Reconnect failed");
					}
				}
			}
			finally
			{
				isReconnecting = false;
			}
		}

		private async Task GetWaitTask(CancellationToken cancellationToken)
		{
			await Task.Run(() => SpinWait.SpinUntil(() => IsConnected || cancellationToken.IsCancellationRequested));
		}

		private void DisconnectInternal(bool disposing)
		{
			if (isDisposed && !disposing)
				throw new ObjectDisposedException(GetType().FullName);

			if (!isStarted)
				return;

			isStarted = false;
			logger?.LogInformation("ModbusClient.Disconnect started");

			bool wasConnected = IsConnected;

			try
			{
				mainCts?.Cancel();
				receivingCts?.Cancel();
			}
			catch (Exception ex)
			{
				logger?.LogError(ex, "ModbusClient.Disconnect stopping reconnect");
			}

			try
			{
				tcpClient?.Close();
				tcpClient?.Dispose();
				tcpClient = null;
			}
			catch (Exception ex)
			{
				logger?.LogError(ex, "ModbusClient.Disconnect closing connection");
			}

			if (wasConnected)
				Task.Run(() => Disconnected?.Invoke(this, EventArgs.Empty)).Forget();

			ConnectingTask?.GetAwaiter().GetResult();
			receiveTask?.GetAwaiter().GetResult();
			logger?.LogInformation("ModbusClient.Disconnect done");
		}

		#endregion Private Methods

		#region IDisposable implementation

		private bool isDisposed;

		/// <summary>
		/// Releases all managed and unmanaged resources used by the <see cref="ModbusClient"/>.
		/// </summary>
		public void Dispose()
		{
			if (isDisposed)
				return;

			isDisposed = true;
			DisconnectInternal(true);

			GC.SuppressFinalize(this);
		}


		#endregion IDisposable implementation

		#region Overrides

		/// <inheritdoc/>
		public override string ToString()
		{
			if (isDisposed)
				throw new ObjectDisposedException(GetType().FullName);

			return $"Modbus TCP {Host}:{Port} - Connected: {IsConnected}";
		}

		#endregion Overrides
	}
}
