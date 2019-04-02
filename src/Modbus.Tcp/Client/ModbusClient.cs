using AMWD.Modbus.Common;
using AMWD.Modbus.Common.Interfaces;
using AMWD.Modbus.Common.Structures;
using AMWD.Modbus.Common.Util;
using AMWD.Modbus.Tcp.Protocol;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AMWD.Modbus.Tcp.Client
{
	/// <summary>
	/// A client to communicate with modbus devices via TCP.
	/// </summary>
	public class ModbusClient : IModbusClient
	{
		#region Fields

		private readonly ILogger<ModbusClient> logger;

		private volatile bool isReconnecting;
		private volatile bool isStarted;

		private TcpClient tcpClient;
		private bool reconnectFailed = false;
		private bool wasConnected = false;

		private int sendTimeout = 1000;
		private int receiveTimeout = 1000;

		private CancellationTokenSource cts;

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
		public ModbusClient(string host, int port = 502, ILogger<ModbusClient> logger = null)
		{
			this.logger = logger;

			if (string.IsNullOrWhiteSpace(host))
			{
				throw new ArgumentNullException(nameof(host));
			}
			if (port < 1 || port > 65535)
			{
				throw new ArgumentOutOfRangeException(nameof(port));
			}

			Host = host;
			Port = port;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ModbusClient"/> class.
		/// </summary>
		/// <param name="address">The remote ip address.</param>
		/// <param name="port">The remote port.</param>
		/// <param name="logger"><see cref="ILogger"/> instance to write log entries.</param>
		public ModbusClient(IPAddress address, int port = 502, ILogger<ModbusClient> logger = null)
			: this(address.ToString(), port, logger)
		{ }

		#endregion Constructors

		#region Properties

		/// <summary>
		/// Gets the result of the asynchronous initialization of this instance.
		/// </summary>
		public Task ConnectingTask { get; private set; }

		/// <summary>
		/// Gets or sets the host name.
		/// </summary>
		public string Host { get; private set; }

		/// <summary>
		/// Gets or sets the port.
		/// </summary>
		public int Port { get; private set; }

		/// <summary>
		/// Gets a value indicating whether the connection is established.
		/// </summary>
		public bool IsConnected => tcpClient?.Connected ?? false;

		/// <summary>
		/// Gets or sets the max. reconnect timespan until the reconnect is aborted.
		/// </summary>
		public TimeSpan ReconnectTimeSpan { get; set; } = TimeSpan.MaxValue;

		/// <summary>
		/// Gets or sets the send timeout in milliseconds. Default: 1000.
		/// </summary>
		public int SendTimeout
		{
			get
			{
				return sendTimeout;
			}
			set
			{
				sendTimeout = value;
				if (tcpClient != null)
				{
					tcpClient.SendTimeout = value;
				}
			}
		}

		/// <summary>
		/// Gets ors sets the receive timeout in milliseconds. Default: 1000;
		/// </summary>
		public int ReceiveTimeout
		{
			get
			{
				return receiveTimeout;
			}
			set
			{
				receiveTimeout = value;
				if (tcpClient != null)
				{
					tcpClient.ReceiveTimeout = value;
				}
			}
		}

		#endregion Properties

		#region Public methods

		#region Control

		/// <summary>
		/// Connects the client to the server.
		/// </summary>
		/// <returns>An awaitable task.</returns>
		public Task Connect()
		{
			logger?.LogTrace("ModbusClient.Connect");
			if (isDisposed)
			{
				throw new ObjectDisposedException(GetType().FullName);
			}

			if (!isStarted)
			{
				logger?.LogInformation("ModbusClient starting");
				isStarted = true;
				cts = new CancellationTokenSource();
				ConnectingTask = Task.Run(() => Reconnect());
			}
			return ConnectingTask;
		}

		/// <summary>
		/// Disconnects the client.
		/// </summary>
		/// <returns>An awaitable task.</returns>
		public async Task Disconnect()
		{
			logger?.LogTrace("ModbusClient.Disconnect");
			if (isDisposed)
			{
				throw new ObjectDisposedException(GetType().FullName);
			}

			if (isStarted)
			{
				logger?.LogInformation("ModbusClient stopping");
				var connected = IsConnected;
				try
				{
					cts.Cancel();

					await ConnectingTask;

					tcpClient?.Dispose();
					tcpClient = null;

					if (connected)
					{
						logger?.LogTrace("ModbusClient.Disconnect fire disconnected event.");
						Task.Run(() => Disconnected?.Invoke(this, EventArgs.Empty)).Forget();
					}
				}
				catch (OperationCanceledException ex)
				{
					logger?.LogDebug(ex, "ModbusClient.Disconnect was (re)connecting?");
				}

				isStarted = false;
			}
		}

		#endregion Control

		#region Read methods

		/// <summary>
		/// Reads one or more coils of a device. (Modbus function 1).
		/// </summary>
		/// <param name="deviceId">The id to address the device (slave).</param>
		/// <param name="startAddress">The first coil number to read.</param>
		/// <param name="count">The number of coils to read.</param>
		/// <returns>A list of coils or null on error.</returns>
		public async Task<List<Coil>> ReadCoils(byte deviceId, ushort startAddress, ushort count)
		{
			logger?.LogTrace($"ModbusClient.ReadCoils({deviceId}, {startAddress}, {count})");
			if (isDisposed)
			{
				throw new ObjectDisposedException(GetType().FullName);
			}

			if (deviceId < Consts.MinDeviceIdTcp || Consts.MaxDeviceId < deviceId)
			{
				throw new ArgumentOutOfRangeException(nameof(deviceId));
			}
			if (startAddress < Consts.MinAddress || Consts.MaxAddress < startAddress + count)
			{
				throw new ArgumentOutOfRangeException(nameof(startAddress));
			}
			if (count < Consts.MinCount || Consts.MaxCoilCountRead < count)
			{
				throw new ArgumentOutOfRangeException(nameof(count));
			}

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
				var response = await SendRequest(request);
				if (response.IsTimeout)
				{
					throw new SocketException((int)SocketError.TimedOut);
				}
				if (response.IsError)
				{
					throw new ModbusException(response.ErrorMessage)
					{
						ErrorCode = response.ErrorCode
					};
				}
				if (request.TransactionId != response.TransactionId)
				{
					throw new ModbusException(nameof(response.TransactionId) + " does not match");
				}

				list = new List<Coil>();
				for (int i = 0; i < count; i++)
				{
					var posByte = i / 8;
					var posBit = i % 8;

					var val = response.Data[posByte] & (byte)Math.Pow(2, posBit);

					list.Add(new Coil
					{
						Address = (ushort)(startAddress + i),
						Value = val > 0
					});
				}
			}
			catch (SocketException sex)
			{
				logger?.LogWarning(sex, "Reading coils. Reconnecting.");
				ConnectingTask = Task.Run(() => Reconnect());
			}
			catch (IOException ioex)
			{
				logger?.LogWarning(ioex, "Reading coils. Reconnecting.");
				ConnectingTask = Task.Run(() => Reconnect());
			}

			return list;
		}

		/// <summary>
		/// Reads one or more discrete inputs of a device. (Modbus function 2).
		/// </summary>
		/// <param name="deviceId">The id to address the device (slave).</param>
		/// <param name="startAddress">The first discrete input number to read.</param>
		/// <param name="count">The number of discrete inputs to read.</param>
		/// <returns>A list of discrete inputs or null on error.</returns>
		public async Task<List<DiscreteInput>> ReadDiscreteInputs(byte deviceId, ushort startAddress, ushort count)
		{
			logger?.LogTrace($"ModbusClient.ReadDiscreteInputs({deviceId}, {startAddress}, {count})");
			if (isDisposed)
			{
				throw new ObjectDisposedException(GetType().FullName);
			}

			if (deviceId < Consts.MinDeviceIdTcp || Consts.MaxDeviceId < deviceId)
			{
				throw new ArgumentOutOfRangeException(nameof(deviceId));
			}
			if (startAddress < Consts.MinAddress || Consts.MaxAddress < startAddress + count)
			{
				throw new ArgumentOutOfRangeException(nameof(startAddress));
			}
			if (count < Consts.MinCount || Consts.MaxCoilCountRead < count)
			{
				throw new ArgumentOutOfRangeException(nameof(count));
			}

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
				var response = await SendRequest(request);
				if (response.IsTimeout)
				{
					throw new SocketException((int)SocketError.TimedOut);
				}
				if (response.IsError)
				{
					throw new ModbusException(response.ErrorMessage)
					{
						ErrorCode = response.ErrorCode
					};
				}
				if (request.TransactionId != response.TransactionId)
				{
					throw new ModbusException(nameof(response.TransactionId) + " does not match");
				}

				list = new List<DiscreteInput>();
				for (int i = 0; i < count; i++)
				{
					var posByte = i / 8;
					var posBit = i % 8;

					var val = response.Data[posByte] & (byte)Math.Pow(2, posBit);

					list.Add(new DiscreteInput
					{
						Address = (ushort)(startAddress + i),
						Value = val > 0
					});
				}
			}
			catch (SocketException sex)
			{
				logger?.LogWarning(sex, "Reading discrete inputs. Reconnecting.");
				ConnectingTask = Task.Run(() => Reconnect());
			}
			catch (IOException ioex)
			{
				logger?.LogWarning(ioex, "Reading discrete inputs. Reconnecting.");
				ConnectingTask = Task.Run(() => Reconnect());
			}

			return list;
		}

		/// <summary>
		/// Reads one or more holding registers of a device. (Modbus function 3).
		/// </summary>
		/// <param name="deviceId">The id to address the device (slave).</param>
		/// <param name="startAddress">The first register number to read.</param>
		/// <param name="count">The number of registers to read.</param>
		/// <returns>A list of registers or null on error.</returns>
		public async Task<List<Register>> ReadHoldingRegisters(byte deviceId, ushort startAddress, ushort count)
		{
			logger?.LogTrace($"ModbusClient.ReadHoldingRegisters({deviceId}, {startAddress}, {count})");
			if (isDisposed)
			{
				throw new ObjectDisposedException(GetType().FullName);
			}

			if (deviceId < Consts.MinDeviceIdTcp || Consts.MaxDeviceId < deviceId)
			{
				throw new ArgumentOutOfRangeException(nameof(deviceId));
			}
			if (startAddress < Consts.MinAddress || Consts.MaxAddress < startAddress + count)
			{
				throw new ArgumentOutOfRangeException(nameof(startAddress));
			}
			if (count < Consts.MinCount || Consts.MaxRegisterCountRead < count)
			{
				throw new ArgumentOutOfRangeException(nameof(count));
			}

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
				var response = await SendRequest(request);
				if (response.IsTimeout)
				{
					throw new SocketException((int)SocketError.TimedOut);
				}
				if (response.IsError)
				{
					throw new ModbusException(response.ErrorMessage)
					{
						ErrorCode = response.ErrorCode
					};
				}
				if (request.TransactionId != response.TransactionId)
				{
					throw new ModbusException(nameof(response.TransactionId) + " does not match");
				}

				list = new List<Register>();
				for (int i = 0; i < count; i++)
				{
					list.Add(new Register
					{
						Address = (ushort)(startAddress + i),
						HiByte = response.Data[i * 2],
						LoByte = response.Data[i * 2 + 1]
					});
				}
			}
			catch (SocketException sex)
			{
				logger?.LogWarning(sex, "Reading holding registers. Reconnecting.");
				ConnectingTask = Task.Run(() => Reconnect());
			}
			catch (IOException ioex)
			{
				logger?.LogWarning(ioex, "Reading holding registers. Reconnecting.");
				ConnectingTask = Task.Run(() => Reconnect());
			}

			return list;
		}

		/// <summary>
		/// Reads one or more input registers of a device. (Modbus function 4).
		/// </summary>
		/// <param name="deviceId">The id to address the device (slave).</param>
		/// <param name="startAddress">The first register number to read.</param>
		/// <param name="count">The number of registers to read.</param>
		/// <returns>A list of registers or null on error.</returns>
		public async Task<List<Register>> ReadInputRegisters(byte deviceId, ushort startAddress, ushort count)
		{
			logger?.LogTrace($"ModbusClient.ReadInputRegisters({deviceId}, {startAddress}, {count})");
			if (isDisposed)
			{
				throw new ObjectDisposedException(GetType().FullName);
			}

			if (deviceId < Consts.MinDeviceIdTcp || Consts.MaxDeviceId < deviceId)
			{
				throw new ArgumentOutOfRangeException(nameof(deviceId));
			}
			if (startAddress < Consts.MinAddress || Consts.MaxAddress < startAddress + count)
			{
				throw new ArgumentOutOfRangeException(nameof(startAddress));
			}
			if (count < Consts.MinCount || Consts.MaxRegisterCountRead < count)
			{
				throw new ArgumentOutOfRangeException(nameof(count));
			}

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
				var response = await SendRequest(request);
				if (response.IsTimeout)
				{
					throw new SocketException((int)SocketError.TimedOut);
				}
				if (response.IsError)
				{
					throw new ModbusException(response.ErrorMessage)
					{
						ErrorCode = response.ErrorCode
					};
				}
				if (request.TransactionId != response.TransactionId)
				{
					throw new ModbusException(nameof(response.TransactionId) + " does not match");
				}

				list = new List<Register>();
				for (int i = 0; i < count; i++)
				{
					list.Add(new Register
					{
						Address = (ushort)(startAddress + i),
						HiByte = response.Data[i * 2],
						LoByte = response.Data[i * 2 + 1]
					});
				}
			}
			catch (SocketException sex)
			{
				logger?.LogWarning(sex, "Reading input registers. Reconnecting.");
				ConnectingTask = Task.Run(() => Reconnect());
			}
			catch (IOException ioex)
			{
				logger?.LogWarning(ioex, "Reading input registers. Reconnecting.");
				ConnectingTask = Task.Run(() => Reconnect());
			}

			return list;
		}

		/// <summary>
		/// Reads device information. (Modbus function 43).
		/// </summary>
		/// <param name="deviceId">The id to address the device (slave).</param>
		/// <param name="categoryId">The category to read (basic, regular, extended, individual).</param>
		/// <param name="objectId">The first object id to read.</param>
		/// <returns>A map of device information and their content as string.</returns>
		public async Task<Dictionary<DeviceIDObject, string>> ReadDeviceInformation(byte deviceId, DeviceIDCategory categoryId, DeviceIDObject objectId = DeviceIDObject.VendorName)
		{
			var raw = await ReadDeviceInformationRaw(deviceId, categoryId, objectId);
			if (raw == null)
			{
				return null;
			}

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
		/// <returns>A map of device information and their content as raw bytes.</returns>>
		public async Task<Dictionary<byte, byte[]>> ReadDeviceInformationRaw(byte deviceId, DeviceIDCategory categoryId, DeviceIDObject objectId = DeviceIDObject.VendorName)
		{
			logger?.LogTrace($"ModbusClient.ReadDeviceInformation({deviceId}, {categoryId}, {objectId})");
			if (isDisposed)
			{
				throw new ObjectDisposedException(GetType().FullName);
			}

			if (deviceId < Consts.MinDeviceIdTcp || Consts.MaxDeviceId < deviceId)
			{
				throw new ArgumentOutOfRangeException(nameof(deviceId));
			}

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
				var response = await SendRequest(request);
				if (response.IsTimeout)
				{
					throw new SocketException((int)SocketError.TimedOut);
				}
				if (response.IsError)
				{
					throw new ModbusException(response.ErrorMessage)
					{
						ErrorCode = response.ErrorCode
					};
				}
				if (request.TransactionId != response.TransactionId)
				{
					throw new ModbusException(nameof(response.TransactionId) + " does not match");
				}

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
			catch (SocketException sex)
			{
				logger?.LogWarning(sex, "Reading device information. Reconnecting.");
				ConnectingTask = Task.Run(() => Reconnect());
			}
			catch (IOException ioex)
			{
				logger?.LogWarning(ioex, "Reading device information. Reconnecting.");
				ConnectingTask = Task.Run(() => Reconnect());
			}

			return null;
		}

		#endregion Read methods

		#region Write methods

		/// <summary>
		/// Writes a single coil status to the Modbus device. (Modbus function 5)
		/// </summary>
		/// <param name="deviceId">The id to address the device (slave).</param>
		/// <param name="coil">The coil to write.</param>
		/// <returns>true on success, otherwise false.</returns>
		public async Task<bool> WriteSingleCoil(byte deviceId, Coil coil)
		{
			logger?.LogTrace($"ModbusClient.WriteSingleCoil({deviceId}, {coil})");
			if (isDisposed)
			{
				throw new ObjectDisposedException(GetType().FullName);
			}

			if (coil == null)
			{
				throw new ArgumentNullException(nameof(coil));
			}
			if (deviceId < Consts.MinDeviceIdTcp || Consts.MaxDeviceId < deviceId)
			{
				throw new ArgumentOutOfRangeException(nameof(deviceId));
			}
			if (coil.Address < Consts.MinAddress || Consts.MaxAddress < coil.Address)
			{
				throw new ArgumentOutOfRangeException(nameof(coil.Address));
			}

			try
			{
				var request = new Request
				{
					DeviceId = deviceId,
					Function = FunctionCode.WriteSingleCoil,
					Address = coil.Address,
					Data = new DataBuffer(2)
				};
				var value = (ushort)(coil.Value ? 0xFF00 : 0x0000);
				request.Data.SetUInt16(0, value);
				var response = await SendRequest(request);
				if (response.IsTimeout)
				{
					throw new SocketException((int)SocketError.TimedOut);
				}
				if (response.IsError)
				{
					throw new ModbusException(response.ErrorMessage)
					{
						ErrorCode = response.ErrorCode
					};
				}
				if (request.TransactionId != response.TransactionId)
				{
					throw new ModbusException(nameof(response.TransactionId) + " does not match");
				}

				return request.TransactionId == response.TransactionId &&
					request.DeviceId == response.DeviceId &&
					request.Function == response.Function &&
					request.Address == response.Address &&
					request.Data.Equals(response.Data);
			}
			catch (SocketException sex)
			{
				logger?.LogWarning(sex, "Writing single coil. Reconnecting.");
				ConnectingTask = Task.Run(() => Reconnect());
			}
			catch (IOException ioex)
			{
				logger?.LogWarning(ioex, "Writing single coil. Reconnecting.");
				ConnectingTask = Task.Run(() => Reconnect());
			}

			return false;
		}

		/// <summary>
		/// Writes a single register to the Modbus device. (Modbus function 6)
		/// </summary>
		/// <param name="deviceId">The id to address the device (slave).</param>
		/// <param name="register">The register to write.</param>
		/// <returns>true on success, otherwise false.</returns>
		public async Task<bool> WriteSingleRegister(byte deviceId, Register register)
		{
			logger?.LogTrace($"ModbusClient.WriteSingleRegister({deviceId}, {register})");
			if (isDisposed)
			{
				throw new ObjectDisposedException(GetType().FullName);
			}

			if (register == null)
			{
				throw new ArgumentNullException(nameof(register));
			}
			if (deviceId < Consts.MinDeviceIdTcp || Consts.MaxDeviceId < deviceId)
			{
				throw new ArgumentOutOfRangeException(nameof(deviceId));
			}
			if (register.Address < Consts.MinAddress || Consts.MaxAddress < register.Address)
			{
				throw new ArgumentOutOfRangeException(nameof(register.Address));
			}

			try
			{
				var request = new Request
				{
					DeviceId = deviceId,
					Function = FunctionCode.WriteSingleRegister,
					Address = register.Address,
					Data = new DataBuffer(new[] { register.HiByte, register.LoByte })
				};
				var response = await SendRequest(request);
				if (response.IsTimeout)
				{
					throw new SocketException((int)SocketError.TimedOut);
				}
				if (response.IsError)
				{
					throw new ModbusException(response.ErrorMessage)
					{
						ErrorCode = response.ErrorCode
					};
				}
				if (request.TransactionId != response.TransactionId)
				{
					throw new ModbusException(nameof(response.TransactionId) + " does not match");
				}

				return request.TransactionId == response.TransactionId &&
					request.DeviceId == response.DeviceId &&
					request.Function == response.Function &&
					request.Address == response.Address &&
					request.Data.Equals(response.Data);
			}
			catch (SocketException sex)
			{
				logger?.LogWarning(sex, "Writing single register. Reconnecting.");
				ConnectingTask = Task.Run(() => Reconnect());
			}
			catch (IOException ioex)
			{
				logger?.LogWarning(ioex, "Writing single register. Reconnecting.");
				ConnectingTask = Task.Run(() => Reconnect());
			}

			return false;
		}

		/// <summary>
		/// Writes multiple coil status to the Modbus device. (Modbus function 15)
		/// </summary>
		/// <param name="deviceId">The id to address the device (slave).</param>
		/// <param name="coils">A list of coils to write.</param>
		/// <returns>true on success, otherwise false.</returns>
		public async Task<bool> WriteCoils(byte deviceId, IEnumerable<Coil> coils)
		{
			logger?.LogTrace($"ModbusClient.WriteCoils({deviceId}, Length: {coils.Count()})");
			if (isDisposed)
			{
				throw new ObjectDisposedException(GetType().FullName);
			}

			if (coils == null || !coils.Any())
			{
				throw new ArgumentNullException(nameof(coils));
			}
			if (deviceId < Consts.MinDeviceIdTcp || Consts.MaxDeviceId < deviceId)
			{
				throw new ArgumentOutOfRangeException(nameof(deviceId));
			}

			var orderedList = coils.OrderBy(c => c.Address).ToList();
			if (orderedList.Count < Consts.MinCount || Consts.MaxCoilCountWrite < orderedList.Count)
			{
				throw new ArgumentOutOfRangeException("Count");
			}

			var firstAddress = orderedList.First().Address;
			var lastAddress = orderedList.Last().Address;

			if (firstAddress + orderedList.Count - 1 != lastAddress)
			{
				throw new ArgumentException("No address gabs allowed within a request");
			}
			if (firstAddress < Consts.MinAddress || Consts.MaxAddress < lastAddress)
			{
				throw new ArgumentOutOfRangeException("Address");
			}

			var numBytes = (int)Math.Ceiling(orderedList.Count / 8.0);
			var coilBytes = new byte[numBytes];
			for (int i = 0; i < orderedList.Count; i++)
			{
				if (orderedList[i].Value)
				{
					var posByte = i / 8;
					var posBit = i % 8;

					var mask = (byte)Math.Pow(2, posBit);
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
				var response = await SendRequest(request);
				if (response.IsTimeout)
				{
					throw new SocketException((int)SocketError.TimedOut);
				}
				if (response.IsError)
				{
					throw new ModbusException(response.ErrorMessage)
					{
						ErrorCode = response.ErrorCode
					};
				}
				if (request.TransactionId != response.TransactionId)
				{
					throw new ModbusException(nameof(response.TransactionId) + " does not match");
				}

				return request.TransactionId == response.TransactionId &&
					request.Address == response.Address &&
					request.Count == response.Count;
			}
			catch (SocketException sex)
			{
				logger?.LogWarning(sex, "Writing multiple coils. Reconnecting.");
				ConnectingTask = Task.Run(() => Reconnect());
			}
			catch (IOException ioex)
			{
				logger?.LogWarning(ioex, "Writing multiple coils. Reconnecting.");
				ConnectingTask = Task.Run(() => Reconnect());
			}

			return false;
		}

		/// <summary>
		/// Writes multiple registers to the Modbus device. (Modbus function 16)
		/// </summary>
		/// <param name="deviceId">The id to address the device (slave).</param>
		/// <param name="registers">A list of registers to write.</param>
		/// <returns>true on success, otherwise false.</returns>
		public async Task<bool> WriteRegisters(byte deviceId, IEnumerable<Register> registers)
		{
			logger?.LogTrace($"ModbusClient.WriteRegisters({deviceId}, Length: {registers.Count()})");
			if (isDisposed)
			{
				throw new ObjectDisposedException(GetType().FullName);
			}

			if (registers == null || !registers.Any())
			{
				throw new ArgumentNullException(nameof(registers));
			}
			if (deviceId < Consts.MinDeviceIdTcp || Consts.MaxDeviceId < deviceId)
			{
				throw new ArgumentOutOfRangeException(nameof(deviceId));
			}

			var orderedList = registers.OrderBy(c => c.Address).ToList();
			if (orderedList.Count < Consts.MinCount || Consts.MaxRegisterCountWrite < orderedList.Count)
			{
				throw new ArgumentOutOfRangeException("Count");
			}

			var firstAddress = orderedList.First().Address;
			var lastAddress = orderedList.Last().Address;

			if (firstAddress + orderedList.Count - 1 != lastAddress)
			{
				throw new ArgumentException("No address gabs allowed within a request");
			}
			if (firstAddress < Consts.MinAddress || Consts.MaxAddress < lastAddress)
			{
				throw new ArgumentOutOfRangeException("Address");
			}

			var data = new DataBuffer(orderedList.Count * 2);
			for (int i = 0; i < orderedList.Count; i++)
			{
				data.SetUInt16(i * 2, orderedList[i].Value);
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
				var response = await SendRequest(request);
				if (response.IsTimeout)
				{
					throw new SocketException((int)SocketError.TimedOut);
				}
				if (response.IsError)
				{
					throw new ModbusException(response.ErrorMessage)
					{
						ErrorCode = response.ErrorCode
					};
				}
				if (request.TransactionId != response.TransactionId)
				{
					throw new ModbusException(nameof(response.TransactionId) + " does not match");
				}

				return request.TransactionId == response.TransactionId &&
					request.Address == response.Address &&
					request.Count == response.Count;
			}
			catch (SocketException sex)
			{
				logger?.LogWarning(sex, "Writing multiple registers. Reconnecting.");
				ConnectingTask = Task.Run(() => Reconnect());
			}
			catch (IOException ioex)
			{
				logger?.LogWarning(ioex, "Writing multiple registers. Reconnecting.");
				ConnectingTask = Task.Run(() => Reconnect());
			}

			return false;
		}

		#endregion Write methods

		#endregion Public methods

		#region Private methods

		private async Task Reconnect()
		{
			if (cts.Token.IsCancellationRequested)
			{
				return;
			}
			if (isReconnecting)
			{
				return;
			}
			isReconnecting = true;
			tcpClient?.Dispose();
			tcpClient = null;

			if (reconnectFailed)
			{
				throw new InvalidOperationException("Reconnect failed");
			}
			if (wasConnected)
			{
				logger?.LogDebug("ModbusClient.Reconnect fire disconnected event.");
				Task.Run(() => Disconnected?.Invoke(this, EventArgs.Empty)).Forget();
			}

			var timeout = 2;
			var maxTimeout = 20;
			var startTime = DateTime.UtcNow;

			while (!cts.Token.IsCancellationRequested)
			{
				try
				{
					tcpClient = new TcpClient(AddressFamily.InterNetworkV6);
					tcpClient.Client.DualMode = true;
					var connectTask = tcpClient.ConnectAsync(Host, Port);
					if (await Task.WhenAny(connectTask, Task.Delay(TimeSpan.FromSeconds(timeout), cts.Token)) == connectTask)
					{
						logger?.LogInformation("ModbusClient.Reconnect connected.");
						tcpClient.SendTimeout = SendTimeout;
						tcpClient.ReceiveTimeout = ReceiveTimeout;
					}
					else if (cts.Token.IsCancellationRequested)
					{
						logger?.LogWarning("ModbusClient.Reconnect was cancelled.");
						return;
					}
					else
					{
						logger?.LogWarning($"ModbusClient.Reconnect failed to connect within {timeout} seconds.");
						timeout += 2;
						if (timeout > maxTimeout)
						{
							timeout = maxTimeout;
						}
						throw new SocketException((int)SocketError.TimedOut);
					}
				}
				catch (SocketException) when (ReconnectTimeSpan == TimeSpan.MaxValue || DateTime.UtcNow <= startTime + ReconnectTimeSpan)
				{
					await Task.Delay(1000, cts.Token);
					continue;
				}
				catch (Exception ex)
				{
					logger?.LogError(ex, "ModbusClient.Reconnect failed.");
					reconnectFailed = true;
					if (isDisposed)
					{
						return;
					}
					if (wasConnected)
					{
						throw new IOException("Server connection lost, reconnect failed", ex);
					}
					else
					{
						throw new IOException("Could not connect to the server", ex);
					}
				}

				wasConnected = true;
				logger?.LogDebug("ModbusClient.Reconnect fire connected event.");
				Task.Run(() => Connected?.Invoke(this, EventArgs.Empty)).Forget();
				break;
			}

			isReconnecting = false;
		}

		private async Task<Response> SendRequest(Request request)
		{
			logger?.LogTrace("ModbusClient.SendRequest");
			if (!IsConnected)
			{
				throw new InvalidOperationException("No connection");
			}

			logger?.LogTrace(request.ToString());

			var stream = tcpClient.GetStream();
			var bytes = request.Serialize();
			var writeTask = stream.WriteAsync(bytes, 0, bytes.Length, cts.Token);
			if (await Task.WhenAny(writeTask, Task.Delay(SendTimeout, cts.Token)) == writeTask && !cts.Token.IsCancellationRequested)
			{
				logger?.LogTrace($"{bytes.Length} bytes sent");
				var responseBytes = new List<byte>();
				var buffer = new byte[6];
				var readTask = stream.ReadAsync(buffer, 0, buffer.Length, cts.Token);
				if (await Task.WhenAny(readTask, Task.Delay(ReceiveTimeout, cts.Token)) == readTask && !cts.Token.IsCancellationRequested)
				{
					var count = await readTask;
					responseBytes.AddRange(buffer.Take(count));
					logger?.LogTrace($"{count} bytes received at first");

					bytes = new byte[2];
					Array.Copy(buffer, 4, bytes, 0, 2);
					if (BitConverter.IsLittleEndian)
					{
						Array.Reverse(bytes);
					}
					int following = BitConverter.ToUInt16(bytes, 0);
					logger?.LogTrace($"{following} bytes following");

					do
					{
						buffer = new byte[following];
						count = await stream.ReadAsync(buffer, 0, buffer.Length, cts.Token);
						logger?.LogTrace($"{count} following bytes received");
						following -= count;
						responseBytes.AddRange(buffer.Take(count));
					}
					while (following > 0 && !cts.Token.IsCancellationRequested);

					logger?.LogTrace($"Response received");

					return new Response(responseBytes.ToArray());
				}
			}

			logger?.LogWarning("ModbusClient.SendRequest failed to send");
			return new Response(new byte[] { 0, 0, 0, 0, 0, 0 });
		}

		#endregion Private methods

		#region IDisposable implementation

		/// <summary>
		/// Releases all managed and unmanaged resources used by the <see cref="ModbusClient"/>.
		/// </summary>
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		private volatile bool isDisposed;

		private void Dispose(bool disposing)
		{
			if (isDisposed)
			{
				return;
			}
			Disconnect().Wait();
			isDisposed = true;
		}

		#endregion IDisposable implementation

		#region Overrides

		/// <inheritdoc/>
		public override string ToString()
		{
			if (isDisposed)
			{
				throw new ObjectDisposedException(GetType().FullName);
			}

			return $"Modbus TCP {Host}:{Port} - Connected: {IsConnected}";
		}

		#endregion Overrides
	}
}
