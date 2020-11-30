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

		private readonly SemaphoreSlim connectLock = new SemaphoreSlim(1, 1);


		private readonly object sendLock = new object();
		private readonly ILogger logger;

		private TcpClient tcpClient;
		private NetworkStream stream;
		private bool isReconnecting;
		private bool wasConnected;
		private ushort transactionId = 0;
		private readonly ConcurrentDictionary<ushort, TaskCompletionSource<Response>> awaitingResponses = new ConcurrentDictionary<ushort, TaskCompletionSource<Response>>();

		private CancellationTokenSource stopCts;
		private CancellationTokenSource receiveCts;

		#endregion Fields

		#region Constructors

		/// <summary>
		/// Initializes a new instance of the <see cref="ModbusClient"/> class.
		/// </summary>
		/// <param name="host">The remote host name or ip address.</param>
		/// <param name="port">The remote port (Default: 502).</param>
		/// <param name="logger">A logger (optional).</param>
		public ModbusClient(string host, int port = 502, ILogger logger = null)
		{
			if (string.IsNullOrWhiteSpace(host))
				throw new ArgumentNullException(nameof(host), "A hostname is required.");

			if (port < 1 || ushort.MaxValue < port)
				throw new ArgumentOutOfRangeException(nameof(port), $"The port should be between 1 and {ushort.MaxValue}.");

			this.logger = logger;
			Host = host;
			Port = port;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ModbusClient"/> class.
		/// </summary>
		/// <param name="host">The remote host name of ip address.</param>
		/// <param name="logger">A logger.</param>
		public ModbusClient(string host, ILogger logger)
			: this(host, 502, logger)
		{ }

		/// <summary>
		/// Initializes a new instance of the <see cref="ModbusClient"/> class.
		/// </summary>
		/// <param name="address">The ip address of the remote host.</param>
		/// <param name="port">The remote port (Default: 502).</param>
		/// <param name="logger">A logger (optional).</param>
		public ModbusClient(IPAddress address, int port = 502, ILogger logger = null)
			: this(address.ToString(), port, logger)
		{ }

		/// <summary>
		/// Initializes a new instance of the <see cref="ModbusClient"/> class.
		/// </summary>
		/// <param name="address">The ip address of the remote host.</param>
		/// <param name="logger">A logger.</param>
		public ModbusClient(IPAddress address, ILogger logger)
			: this(address, 502, logger)
		{ }

		#endregion Constructors

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
		public Task ConnectingTask { get; private set; } = Task.CompletedTask;

		/// <summary>
		/// Gets a value indicating whether the connection is established.
		/// </summary>
		public bool IsConnected { get; private set; }

		/// <summary>
		/// Gets or sets the max. reconnect timespan until the reconnect is aborted.
		/// </summary>
		public TimeSpan ReconnectTimeSpan { get; set; } = TimeSpan.MaxValue;

		/// <summary>
		/// Gets or sets the max. timeout per try to connect.
		/// </summary>
		/// <remarks>
		/// The connect timeout starts with 2 seconds and on each try another 2 seconds
		/// will be added until this maximum is reached.
		/// </remarks>
		public TimeSpan MaxConnectTimeout { get; set; } = TimeSpan.FromSeconds(30);

		/// <summary>
		/// Gets or sets the send timeout. Default: 1 second.
		/// </summary>
		public TimeSpan SendTimeout { get; set; } = TimeSpan.FromSeconds(1);

		/// <summary>
		/// Gets ors sets the receive timeout. Default: 1 second.
		/// </summary>
		public TimeSpan ReceiveTimeout { get; set; } = TimeSpan.FromSeconds(1);

		#endregion Properties

		#region Public methods

		#region Control

		/// <summary>
		/// Connects the client to the remote host.
		/// </summary>
		/// <returns>An awaitable task.</returns>
		public Task Connect()
		{
			try
			{
				logger?.LogTrace("ModbusClient.Connect enter");
				CheckDisposed();

				if (stopCts != null)
					return ConnectingTask;

				stopCts = new CancellationTokenSource();

				logger?.LogInformation("Modbus client starting.");

				wasConnected = false;
				ConnectingTask = Task.Run(async () => await Reconnect());

				logger?.LogInformation("Modbus client started.");

				return ConnectingTask;
			}
			finally
			{
				logger?.LogTrace("ModbusClient.Connect leave");
			}
		}

		/// <summary>
		/// Disconnects the client.
		/// </summary>
		/// <returns>An awaitable task.</returns>
		public async Task Disconnect()
		{
			try
			{
				logger?.LogTrace("ModbusClient.Disconnect enter");
				CheckDisposed();

				stopCts?.Cancel();
				receiveCts?.Cancel();

				bool wasConnected = IsConnected;
				IsConnected = false;

				await ConnectingTask;

				stream?.Dispose();
				tcpClient?.Dispose();

				if (wasConnected)
					Task.Run(() => Disconnected?.Invoke(this, EventArgs.Empty)).Forget();
			}
			finally
			{
				logger?.LogTrace("ModbusClient.Disconnect leave");
			}
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
			try
			{
				logger?.LogTrace("ModbusClient.ReadCoils enter");

				if (deviceId < Consts.MinDeviceIdTcp || Consts.MaxDeviceId < deviceId)
					throw new ArgumentOutOfRangeException(nameof(deviceId));

				if (startAddress < Consts.MinAddress || Consts.MaxAddress < startAddress + count)
					throw new ArgumentOutOfRangeException(nameof(startAddress));

				if (count < Consts.MinCount || Consts.MaxCoilCountRead < count)
					throw new ArgumentOutOfRangeException(nameof(count));

				logger?.LogDebug($"Read coils from device #{deviceId} starting on {startAddress} for {count} coils.");

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
					logger?.LogWarning(ex, $"Reading coils failed: {ex.GetMessage()}, reconnecting.");
					if (!isReconnecting)
						ConnectingTask = Task.Run(async () => await Reconnect());
				}
				catch (IOException ex)
				{
					logger?.LogWarning(ex, $"Reading coils failed: {ex.GetMessage()}, reconnecting.");
					if (!isReconnecting)
						ConnectingTask = Task.Run(async () => await Reconnect());
				}

				return list;
			}
			finally
			{
				logger?.LogTrace("ModbusClient.ReadCoils leave");
			}
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
			try
			{
				logger?.LogTrace("ModbusClient.ReadDiscreteInputs enter");

				if (deviceId < Consts.MinDeviceIdTcp || Consts.MaxDeviceId < deviceId)
					throw new ArgumentOutOfRangeException(nameof(deviceId));

				if (startAddress < Consts.MinAddress || Consts.MaxAddress < startAddress + count)
					throw new ArgumentOutOfRangeException(nameof(startAddress));

				if (count < Consts.MinCount || Consts.MaxCoilCountRead < count)
					throw new ArgumentOutOfRangeException(nameof(count));

				logger?.LogDebug($"Reading discrete inputs from device #{deviceId} starting on {startAddress} for {count} inputs.");

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
					logger?.LogWarning(ex, $"Reading discrete inputs failed: {ex.GetMessage()}, reconnecting.");
					if (!isReconnecting)
						ConnectingTask = Task.Run(async () => await Reconnect());
				}
				catch (IOException ex)
				{
					logger?.LogWarning(ex, $"Reading discrete inputs failed: {ex.GetMessage()}, reconnecting.");
					if (!isReconnecting)
						ConnectingTask = Task.Run(async () => await Reconnect());
				}

				return list;
			}
			finally
			{
				logger?.LogTrace("ModbusClient.ReadDiscreteInputs leave");
			}
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
			try
			{
				logger?.LogTrace("ModbusClient.ReadHoldingRegisters enter");

				if (deviceId < Consts.MinDeviceIdTcp || Consts.MaxDeviceId < deviceId)
					throw new ArgumentOutOfRangeException(nameof(deviceId));

				if (startAddress < Consts.MinAddress || Consts.MaxAddress < startAddress + count)
					throw new ArgumentOutOfRangeException(nameof(startAddress));

				if (count < Consts.MinCount || Consts.MaxRegisterCountRead < count)
					throw new ArgumentOutOfRangeException(nameof(count));

				logger?.LogDebug($"Reading holding registers from device #{deviceId} starting on {startAddress} for {count} registers.");

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
					logger?.LogWarning(ex, $"Reading holding registers failed: {ex.GetMessage()}, reconnecting.");
					if (!isReconnecting)
						ConnectingTask = Task.Run(async () => await Reconnect());
				}
				catch (IOException ex)
				{
					logger?.LogWarning(ex, $"Reading holding registers failed: {ex.GetMessage()}, reconnecting.");
					if (!isReconnecting)
						ConnectingTask = Task.Run(async () => await Reconnect());
				}

				return list;
			}
			finally
			{
				logger?.LogTrace("ModbusClient.ReadHoldingRegisters leave");
			}
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
			try
			{
				logger?.LogTrace("ModbusClient.ReadInputRegisters enter");

				if (deviceId < Consts.MinDeviceIdTcp || Consts.MaxDeviceId < deviceId)
					throw new ArgumentOutOfRangeException(nameof(deviceId));

				if (startAddress < Consts.MinAddress || Consts.MaxAddress < startAddress + count)
					throw new ArgumentOutOfRangeException(nameof(startAddress));

				if (count < Consts.MinCount || Consts.MaxRegisterCountRead < count)
					throw new ArgumentOutOfRangeException(nameof(count));

				logger?.LogDebug($"Reading input registers from device #{deviceId} starting on {startAddress} for {count} registers.");

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
					logger?.LogWarning(ex, $"Reading input registers failed: {ex.GetMessage()}, reconnecting.");
					if (!isReconnecting)
						ConnectingTask = Task.Run(async () => await Reconnect());
				}
				catch (IOException ex)
				{
					logger?.LogWarning(ex, $"Reading input registers failed: {ex.GetMessage()}, reconnecting.");
					if (!isReconnecting)
						ConnectingTask = Task.Run(async () => await Reconnect());
				}

				return list;
			}
			finally
			{
				logger?.LogTrace("ModbusClient.ReadInputRegisters leave");
			}
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
			try
			{
				logger?.LogTrace("ModbusClient.ReadDeviceInformation enter");

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
			finally
			{
				logger?.LogTrace("ModbusClient.ReadDeviceInformation leave");
			}
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
			try
			{
				logger?.LogTrace("ModbusClient.ReadDeviceInformationRaw enter");

				if (deviceId < Consts.MinDeviceIdTcp || Consts.MaxDeviceId < deviceId)
					throw new ArgumentOutOfRangeException(nameof(deviceId));

				logger?.LogDebug($"Reading device information from device #{deviceId}. category: {categoryId}, object: {objectId}");

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
					logger?.LogWarning(ex, $"Reading device information failed: {ex.GetMessage()}, reconnecting.");
					if (!isReconnecting)
						ConnectingTask = Task.Run(async () => await Reconnect());
				}
				catch (IOException ex)
				{
					logger?.LogWarning(ex, $"Reading device information failed: {ex.GetMessage()}, reconnecting.");
					if (!isReconnecting)
						ConnectingTask = Task.Run(async () => await Reconnect());
				}

				return null;
			}
			finally
			{
				logger?.LogTrace("ModbusClient.ReadDeviceInformationRaw leave");
			}
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
			try
			{
				logger?.LogTrace("ModbusClient.WriteSingleCoil enter");

				if (coil == null)
					throw new ArgumentNullException(nameof(coil));

				if (coil.Type != ModbusObjectType.Coil)
					throw new ArgumentException("Invalid coil type set");

				if (deviceId < Consts.MinDeviceIdTcp || Consts.MaxDeviceId < deviceId)
					throw new ArgumentOutOfRangeException(nameof(deviceId));

				if (coil.Address < Consts.MinAddress || Consts.MaxAddress < coil.Address)
					throw new ArgumentOutOfRangeException(nameof(coil.Address));

				logger?.LogDebug($"Writing a coil to device #{deviceId}: {coil}.");

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
					logger?.LogWarning(ex, $"Writing a coil failed: {ex.GetMessage()}, reconnecting.");
					if (!isReconnecting)
						ConnectingTask = Task.Run(async () => await Reconnect());
				}
				catch (IOException ex)
				{
					logger?.LogWarning(ex, $"Writing a coil failed: {ex.GetMessage()}, reconnecting.");
					if (!isReconnecting)
						ConnectingTask = Task.Run(async () => await Reconnect());
				}

				return false;
			}
			finally
			{
				logger?.LogTrace("ModbusClient.WriteSingleCoil leave");
			}
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
			try
			{
				logger?.LogTrace("ModbusClient.WriteSingleRegister enter");

				if (register == null)
					throw new ArgumentNullException(nameof(register));

				if (register.Type != ModbusObjectType.HoldingRegister)
					throw new ArgumentException("Invalid register type set");

				if (deviceId < Consts.MinDeviceIdTcp || Consts.MaxDeviceId < deviceId)
					throw new ArgumentOutOfRangeException(nameof(deviceId));

				if (register.Address < Consts.MinAddress || Consts.MaxAddress < register.Address)
					throw new ArgumentOutOfRangeException(nameof(register.Address));

				logger?.LogDebug($"Writing a register to device #{deviceId}: {register}.");

				try
				{
					var request = new Request
					{
						DeviceId = deviceId,
						Function = FunctionCode.WriteSingleRegister,
						Address = register.Address,
						Data = new DataBuffer(new[] { register.HiByte, register.LoByte })
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
						request.DeviceId == response.DeviceId &&
						request.Function == response.Function &&
						request.Address == response.Address &&
						request.Data.Equals(response.Data);
				}
				catch (SocketException ex)
				{
					logger?.LogWarning(ex, $"Writing a register failed: {ex.GetMessage()}, reconnecting.");
					if (!isReconnecting)
						ConnectingTask = Task.Run(async () => await Reconnect());
				}
				catch (IOException ex)
				{
					logger?.LogWarning(ex, $"Writing a register failed: {ex.GetMessage()}, reconnecting.");
					if (!isReconnecting)
						ConnectingTask = Task.Run(async () => await Reconnect());
				}

				return false;
			}
			finally
			{
				logger?.LogTrace("ModbusClient.WriteSingleRegister leave");
			}
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
			try
			{
				logger?.LogTrace("ModbusClient.WriteCoils enter");

				if (coils?.Any() != true)
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

				logger?.LogDebug($"Writing coils to device #{deviceId} starting on {firstAddress} for {orderedList.Count} coils.");

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
					logger?.LogWarning(ex, $"Writing coils failed: {ex.GetMessage()}, reconnecting.");
					if (!isReconnecting)
						ConnectingTask = Task.Run(async () => await Reconnect());
				}
				catch (IOException ex)
				{
					logger?.LogWarning(ex, $"Writing coils failed: {ex.GetMessage()}, reconnecting.");
					if (!isReconnecting)
						ConnectingTask = Task.Run(async () => await Reconnect());
				}

				return false;
			}
			finally
			{
				logger?.LogTrace("ModbusClient.WriteCoils leave");
			}
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
			try
			{
				logger?.LogTrace("ModbusClient.WriteRegisters enter");

				if (registers?.Any() != true)
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

				logger?.LogDebug($"Writing registers to device #{deviceId} starting on {firstAddress} for {orderedList.Count} registers.");

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
					logger?.LogWarning(ex, $"Writing registers failed: {ex.GetMessage()}, reconnecting.");
					if (!isReconnecting)
						ConnectingTask = Task.Run(async () => await Reconnect());
				}
				catch (IOException ex)
				{
					logger?.LogWarning(ex, $"Writing registers failed: {ex.GetMessage()}, reconnecting.");
					if (!isReconnecting)
						ConnectingTask = Task.Run(async () => await Reconnect());
				}

				return false;
			}
			finally
			{
				logger?.LogTrace("ModbusClient.WriteRegisters leave");
			}
		}

		#endregion Write Methods

		#endregion Public methods

		#region Private methods

		private async Task Reconnect()
		{
			try
			{
				logger?.LogTrace($"ModbusClient.Reconnect enter");
				await connectLock.WaitAsync(stopCts.Token);

				if (isReconnecting || stopCts.IsCancellationRequested)
					return;

				isReconnecting = true;
				IsConnected = false;
				
				logger?.LogInformation($"{(wasConnected ? "Reconnect" : "Connect")} starting.");

				if (wasConnected)
				{
					receiveCts?.Cancel();
					Task.Run(() => Disconnected?.Invoke(this, EventArgs.Empty)).Forget();
				}

				var timeout = TimeSpan.FromSeconds(2);
				var startTime = DateTime.UtcNow;

				var address = await GetAddress(Host);
				while (!stopCts.IsCancellationRequested)
				{
					try
					{
						stream?.Dispose();
						stream = null;
						tcpClient?.Dispose();
						tcpClient = new TcpClient(address.AddressFamily);
						if (address.AddressFamily == AddressFamily.InterNetworkV6)
							tcpClient.Client.DualMode = true;

						var connectTask = tcpClient.ConnectAsync(address, Port);
						if (await Task.WhenAny(connectTask, Task.Delay(timeout, stopCts.Token)) == connectTask && tcpClient.Connected)
						{
							stream = tcpClient.GetStream();

							receiveCts = new CancellationTokenSource();
							Task.Run(async () => await ReceiveLoop(receiveCts.Token)).Forget();

							IsConnected = true;
							logger?.LogInformation($"{(wasConnected ? "Reconnect" : "Connect")}ed successfully.");
							Task.Run(() => Connected?.Invoke(this, EventArgs.Empty)).Forget();
							wasConnected = true;
							return;
						}
						else if (stopCts.IsCancellationRequested)
						{
							logger?.LogInformation($"{(wasConnected ? "Reconnect" : "Connect")} cancelled.");
							return;
						}
						else
						{
							if (timeout < MaxConnectTimeout)
							{
								logger?.LogWarning($"{(wasConnected ? "Reconnect" : "Connect")} failed within {timeout}.");

								timeout = timeout.Add(TimeSpan.FromSeconds(2));
								if (timeout > MaxConnectTimeout)
									timeout = MaxConnectTimeout;
							}
							throw new SocketException((int)SocketError.TimedOut);
						}
					}
					catch (SocketException) when (ReconnectTimeSpan == TimeSpan.MaxValue || DateTime.UtcNow - startTime <= ReconnectTimeSpan)
					{
						await Task.Delay(1000, stopCts.Token);
						continue;
					}
					catch (Exception ex)
					{
						logger?.LogError(ex, $"{(wasConnected ? "Reconnect" : "Connect")} failed: {ex.GetMessage()}");
					}
				}
			}
			catch (OperationCanceledException) when (stopCts.IsCancellationRequested)
			{
				// Client shutting down
				return;
			}
			finally
			{
				isReconnecting = false;
				logger?.LogTrace($"ModbusClient.Reconnect leave");
				connectLock.Release();
			}
		}

		private async Task<Response> SendRequest(Request request, CancellationToken cancellationToken)
		{
			try
			{
				logger?.LogTrace("ModbusClient.SendRequest enter");
				CheckDisposed();

				if (!IsConnected)
				{
					if (!isReconnecting)
						ConnectingTask = Task.Run(async () => await Reconnect());

					throw new InvalidOperationException("Modbus client is not connected.");
				}

				var tcs = new TaskCompletionSource<Response>();
				lock (sendLock)
				{
					request.TransactionId = transactionId;
					transactionId++;
				}
				awaitingResponses[request.TransactionId] = tcs;

				try
				{
					int retry = 2;
					while (retry-- > 0)
					{
						using (var cts = new CancellationTokenSource())
						using (stopCts.Token.Register(() => cts.Cancel()))
						using (cancellationToken.Register(() => cts.Cancel()))
						using (cts.Token.Register(() => tcs.TrySetCanceled()))
						{
							try
							{
								logger?.LogDebug($"Sending {request}");
								byte[] bytes = request.Serialize();
								var sendTask = stream.WriteAsync(bytes, 0, bytes.Length, cts.Token);
								if (await Task.WhenAny(sendTask, Task.Delay(SendTimeout, cts.Token)) == sendTask && !cts.IsCancellationRequested)
								{
									if (retry == 0)
									{
										logger?.LogWarning($"Request for Transaction #{request.TransactionId} re-sent.");
									}
									else
									{
										logger?.LogDebug($"Request for Transaction #{request.TransactionId} sent.");
									}

									if (await Task.WhenAny(tcs.Task, Task.Delay(ReceiveTimeout, cts.Token)) == tcs.Task && !cts.IsCancellationRequested)
									{
										logger?.LogTrace($"ModbusClient.SendRequest response received");
										return await tcs.Task;
									}
								}
							}
							catch (OperationCanceledException)
							{
								throw;
							}
							catch (Exception)
							{
								await Task.Delay(ReceiveTimeout, cts.Token);
							}
						}
					}
				}
				catch (OperationCanceledException) when (stopCts.IsCancellationRequested || cancellationToken.IsCancellationRequested)
				{
					tcs.TrySetCanceled();
				}
				catch (Exception ex)
				{
					logger?.LogError(ex, $"Unexpected error ({ex.GetType().Name}) on send: {ex.GetMessage()}");
					//tcs.TrySetResult(new Response(new byte[] { 0, 0, 0, 0, 0, 0 }));
					tcs.TrySetException(ex);
				}

				return await tcs.Task;
			}
			finally
			{
				logger?.LogTrace("ModbusClient.SendRequest leave");
			}
		}

		private async Task ReceiveLoop(CancellationToken cancellationToken)
		{
			try
			{
				logger?.LogTrace("ModbusClient.ReceiveLoop enter");
				logger?.LogInformation("Receiving responses started.");

				while (!cancellationToken.IsCancellationRequested)
				{
					try
					{
						if (stream == null)
						{
							await Task.Delay(200, cancellationToken);
							continue;
						}

						while (!cancellationToken.IsCancellationRequested)
						{
							using var responseStream = new MemoryStream();

							using (var cts = new CancellationTokenSource(ReceiveTimeout))
							using (cancellationToken.Register(() => cts.Cancel()))
							{
								try
								{
									byte[] header = await stream.ReadExpectedBytes(6, cts.Token);
									await responseStream.WriteAsync(header, 0, header.Length, cts.Token);

									byte[] bytes = header.Skip(4).Take(2).ToArray();
									if (BitConverter.IsLittleEndian)
										Array.Reverse(bytes);

									int following = BitConverter.ToUInt16(bytes, 0);
									byte[] payload = await stream.ReadExpectedBytes(following, cts.Token);
									await responseStream.WriteAsync(payload, 0, payload.Length, cts.Token);
								}
								catch (OperationCanceledException) when (cts.IsCancellationRequested)
								{
									continue;
								}
							}

							try
							{
								var response = new Response(responseStream.GetBuffer());
								if (awaitingResponses.TryRemove(response.TransactionId, out var tcs))
								{
									logger?.LogDebug($"Received response for transaction #{response.TransactionId}.");
									tcs.TrySetResult(response);
								}
								else
								{
									logger?.LogWarning($"Received response for NOT REQUESTED transaction #{response.TransactionId}.");
								}
							}
							catch (ArgumentException ex)
							{
								logger?.LogError(ex, $"Invalid data received: {ex.Message}");
							}
							catch (NotImplementedException ex)
							{
								logger?.LogError(ex, $"Invalid data received: {ex.Message}");
							}
						}
					}
					catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
					{
						// Receive loop stopping
						throw;
					}
					catch (IOException)
					{
						if (!isReconnecting)
							ConnectingTask = Task.Run(async () => await Reconnect());

						await Task.Delay(1, cancellationToken);   // make sure the reconnect task has time to start.
					}
					catch (Exception ex)
					{
						logger?.LogError(ex, $"Unexpected error ({ex.GetType().Name}) on receive: {ex.GetMessage()}");
					}
				}
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				// Receive loop stopping
				return;
			}
			catch (Exception ex)
			{
				logger?.LogError(ex, $"Unexpected error ({ex.GetType().Name}) on receive: {ex.GetMessage()}");
			}
			finally
			{
				logger?.LogTrace("ModbusClient.ReceiveLoop leave");
			}
		}

		private async Task<IPAddress> GetAddress(string host)
		{
			if (IPAddress.TryParse(host, out var ipAddress))
				return ipAddress;

			var ipAddresses = await Dns.GetHostAddressesAsync(host);

			ipAddress = ipAddresses
				.Where(ip => ip.AddressFamily == AddressFamily.InterNetworkV6)
				.FirstOrDefault();
			if (ipAddress == null)
			{
				ipAddress = ipAddresses
					.Where(ip => ip.AddressFamily == AddressFamily.InterNetwork)
					.FirstOrDefault();
			}
			return ipAddress;
		}

		#endregion Private methods

		#region IDisposable implementation

		private bool isDisposed;

		/// <inheritdoc/>
		public void Dispose()
		{
			if (isDisposed)
				return;

			Disconnect()
				.ConfigureAwait(false)
				.GetAwaiter()
				.GetResult();

			isDisposed = true;
		}

		private void CheckDisposed()
		{
			if (isDisposed)
				throw new ObjectDisposedException(GetType().FullName);
		}

		#endregion IDisposable implementation

		#region Overrides

		/// <inheritdoc/>
		public override string ToString()
		{
			CheckDisposed();
			return $"Modbus TCP {Host}:{Port} - Connected: {IsConnected}";
		}

		#endregion Overrides
	}
}
