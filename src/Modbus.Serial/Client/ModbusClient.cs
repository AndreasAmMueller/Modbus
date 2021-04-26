using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AMWD.Modbus.Common;
using AMWD.Modbus.Common.Interfaces;
using AMWD.Modbus.Common.Structures;
using AMWD.Modbus.Common.Util;
using AMWD.Modbus.Serial.Protocol;
using AMWD.Modbus.Serial.Util;
using Microsoft.Extensions.Logging;

namespace AMWD.Modbus.Serial.Client
{
	/// <summary>
	/// A client to communicate with modbus devices via serial port.
	/// </summary>
	public class ModbusClient : IModbusClient
	{
		#region Fields

		// Optional logger for all actions
		private readonly ILogger logger;

		// The serial port to connect to the remote.
		private SerialPort serialPort;
		// And the settings.
		private BaudRate baudRate = BaudRate.Baud38400;
		private int dataBits = 8;
		private Parity parity = Parity.None;
		private StopBits stopBits = StopBits.None;
		private Handshake handshake = Handshake.None;
		private TimeSpan sendTimeout = TimeSpan.FromSeconds(1);
		private TimeSpan receiveTimeout = TimeSpan.FromSeconds(1);
		private int bufferSize = 0;

		// driver switch
		private RS485Flags serialDriverFlags;
		private bool driverModified;

		// Connection handling
		private readonly object reconnectLock = new object();
		private CancellationTokenSource stopCts;
		private bool isStarted = false;
		private bool wasConnected = false;
		private bool isReconnecting = false;
		private TaskCompletionSource<bool> reconnectTcs;
		private readonly SemaphoreSlim sendLock = new SemaphoreSlim(1, 1);

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
		/// <param name="portName">The serial port name.</param>
		/// <param name="logger"><see cref="ILogger"/> instance to write log entries.</param>
		public ModbusClient(string portName, ILogger logger = null)
		{
			this.logger = logger;

			if (string.IsNullOrWhiteSpace(portName))
				throw new ArgumentNullException(nameof(portName), "Portname has to be set");

			PortName = portName;
		}

		#endregion Constructors

		#region Properties

		/// <summary>
		/// Gets the serial port name.
		/// </summary>
		public string PortName { get; private set; }

		/// <summary>
		/// Gets or sets the baud rate. Default: 38400.
		/// </summary>
		public BaudRate BaudRate
		{
			get => baudRate;
			set
			{
				baudRate = value;

				if (serialPort != null)
					serialPort.BaudRate = (int)value;
			}
		}

		/// <summary>
		/// Gets or sets the number of data bits. Default: 8.
		/// </summary>
		public int DataBits
		{
			get => dataBits;
			set
			{
				if (value < 5 || 8 < value)
					throw new ArgumentOutOfRangeException("Only DataBits from 5 to 8 are allowed.");

				dataBits = value;

				if (serialPort != null)
					serialPort.DataBits = value;
			}
		}

		/// <summary>
		/// Gets or sets the parity. Default: None.
		/// </summary>
		public Parity Parity
		{
			get => parity;
			set
			{
				parity = value;

				if (serialPort != null)
					serialPort.Parity = value;
			}
		}

		/// <summary>
		/// Gets or sets the number of stop bits. Default: None.
		/// </summary>
		public StopBits StopBits
		{
			get => stopBits;
			set
			{
				if (value == StopBits.None)
					throw new ArgumentOutOfRangeException("StopBits.None is not a valid value.");

				stopBits = value;

				if (serialPort != null)
					serialPort.StopBits = value;
			}
		}

		/// <summary>
		/// Gets or sets the handshake. Default: None.
		/// </summary>
		public Handshake Handshake
		{
			get => handshake;
			set
			{
				handshake = value;

				if (serialPort != null)
					serialPort.Handshake = value;
			}
		}

		/// <summary>
		/// Gets or sets a value indicating whether the remote is delivering the bytes in little-endian order.
		/// </summary>
		public bool IsLittleEndianRemote { get; set; }

		/// <summary>
		/// Gets the result of the asynchronous initialization of this instance.
		/// </summary>
		public Task ConnectingTask { get; private set; }

		/// <summary>
		/// Gets a value indicating whether the connection is established.
		/// </summary>
		public bool IsConnected => serialPort?.IsOpen ?? false;

		/// <summary>
		/// Gets or sets the max reconnect timespan until the reconnect is aborted.
		/// </summary>
		public TimeSpan ReconnectTimeSpan { get; set; } = TimeSpan.MaxValue;

		/// <summary>
		/// Gets or sets the send timeout in milliseconds. Default 1000 (recommended).
		/// </summary>
		public TimeSpan SendTimeout
		{
			get => sendTimeout;
			set
			{
				sendTimeout = value;

				if (serialPort != null)
					serialPort.WriteTimeout = (int)value.TotalMilliseconds;
			}
		}

		/// <summary>
		/// Gets or sets the receive timeout in milliseconds. Default 1000 (recommended).
		/// </summary>
		public TimeSpan ReceiveTimeout
		{
			get => receiveTimeout;
			set
			{
				receiveTimeout = value;

				if (serialPort != null)
					serialPort.ReadTimeout = (int)value.TotalMilliseconds;
			}
		}

		/// <summary>
		/// Gets or sets buffer size in bytes.
		/// </summary>
		public int BufferSize
		{
			get => serialPort?.ReadBufferSize ?? bufferSize;
			set
			{
				bufferSize = value;
				if (serialPort != null)
				{
					serialPort.ReadBufferSize = value;
					serialPort.WriteBufferSize = value;
				}
			}
		}

		/// <summary>
		/// Gets or sets a value indicating whether to indicate the driver to switch to RS485 mode.
		/// </summary>
		public bool DriverEnableRS485 { get; set; }

		#endregion Properties

		#region Public Methods

		#region Static

		/// <summary>
		/// Returns a list of available serial ports.
		/// </summary>
		/// <returns></returns>
		public static string[] AvailablePorts()
		{
			return SerialPort.GetPortNames();
		}

		#endregion Static

		#region Control

		/// <summary>
		/// Connects the client to the device.
		/// </summary>
		/// <param name="cancellationToken"></param>
		/// <returns>An awaitable task.</returns>
		public async Task Connect(CancellationToken cancellationToken = default)
		{
			var cancelTask = ReconnectTimeSpan == TimeSpan.MaxValue
				? Task.Delay(Timeout.Infinite, cancellationToken)
				: Task.Delay(ReconnectTimeSpan, cancellationToken);

			try
			{
				logger?.LogTrace("ModbusClient.Connect enter");
				CheckDisposed();

				if (isStarted)
				{
					await Task.WhenAny(ConnectingTask, cancelTask);
					return;
				}
				isStarted = true;
				stopCts = new CancellationTokenSource();

				logger?.LogInformation("ModbusClient starting.");

				if (DriverEnableRS485 && !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				{
					try
					{
						var rs485 = GetDriverState();
						serialDriverFlags = rs485.Flags;
						rs485.Flags |= RS485Flags.Enabled;
						rs485.Flags &= ~RS485Flags.RxDuringTx;
						SetDriverState(rs485);
						driverModified = true;
					}
					catch (Exception ex)
					{
						logger?.LogError(ex, $"Set driver state to RS485 failed: {ex.GetMessage()}");
						throw;
					}
				}

				wasConnected = false;
				ConnectingTask = GetReconnectTask();

				logger?.LogInformation("Modbus client started.");
				await Task.WhenAny(ConnectingTask, cancelTask);
			}
			finally
			{
				if (cancelTask.Status != TaskStatus.WaitingForActivation)
					cancelTask?.Dispose();
				logger?.LogTrace("ModbusClient.Connect leave");
			}
		}

		/// <summary>
		/// Disconnects the client.
		/// </summary>
		/// <param name="cancellationToken"></param>
		/// <returns>An awaitable task.</returns>
		public Task Disconnect(CancellationToken cancellationToken = default)
		{
			try
			{
				logger?.LogTrace("ModbusClient.Disconnect enter");
				CheckDisposed();

				DisconnectInternal();
				return Task.CompletedTask;
			}
			finally
			{
				logger?.LogTrace("ModbusClient.Disconnect leave");
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
		/// <param name="cancellationToken">A cancellation token to abort the action.</param>
		/// <returns>A list of coils or null on error.</returns>
		public async Task<List<Coil>> ReadCoils(byte deviceId, ushort startAddress, ushort count, CancellationToken cancellationToken = default)
		{
			try
			{
				logger?.LogTrace("ModbusClient.ReadCoils enter");

				if (deviceId < Consts.MinDeviceIdRtu || Consts.MaxDeviceId < deviceId)
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
						throw new IOException("Request timed out");

					if (response.IsError)
						throw new ModbusException(response.ErrorMessage);

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
				catch (IOException ex)
				{
					logger?.LogWarning(ex, $"Reading coils failed: {ex.GetMessage()}, reconnecting.");
					ConnectingTask = GetReconnectTask();
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

				if (deviceId < Consts.MinDeviceIdRtu || Consts.MaxDeviceId < deviceId)
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
						throw new IOException("Request timed out");

					if (response.IsError)
						throw new ModbusException(response.ErrorMessage);

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
				catch (IOException ex)
				{
					logger?.LogWarning(ex, $"Reading discrete inputs failed: {ex.GetMessage()}, reconnecting.");
					ConnectingTask = GetReconnectTask();
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

				if (deviceId < Consts.MinDeviceIdRtu || Consts.MaxDeviceId < deviceId)
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
						throw new IOException("Request timed out");

					if (response.IsError)
						throw new ModbusException(response.ErrorMessage);

					list = new List<Register>();
					for (int i = 0; i < count; i++)
					{
						var register = new Register
						{
							Type = ModbusObjectType.HoldingRegister,
							Address = (ushort)(startAddress + i)
						};

						if (IsLittleEndianRemote)
						{
							register.LoByte = response.Data[i * 2];
							register.HiByte = response.Data[i * 2 + 1];
						}
						else
						{
							register.HiByte = response.Data[i * 2];
							register.LoByte = response.Data[i * 2 + 1];
						}

						list.Add(register);
					}
				}
				catch (IOException ex)
				{
					logger?.LogWarning(ex, $"Reading holding registers failed: {ex.GetMessage()}, reconnecting.");
					ConnectingTask = GetReconnectTask();
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

				if (deviceId < Consts.MinDeviceIdRtu || Consts.MaxDeviceId < deviceId)
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
						throw new IOException("Request timed out");

					if (response.IsError)
						throw new ModbusException(response.ErrorMessage);

					list = new List<Register>();
					for (int i = 0; i < count; i++)
					{
						var register = new Register
						{
							Type = ModbusObjectType.InputRegister,
							Address = (ushort)(startAddress + i)
						};

						if (IsLittleEndianRemote)
						{
							register.LoByte = response.Data[i * 2];
							register.HiByte = response.Data[i * 2 + 1];
						}
						else
						{
							register.HiByte = response.Data[i * 2];
							register.LoByte = response.Data[i * 2 + 1];
						}

						list.Add(register);
					}
				}
				catch (IOException ex)
				{
					logger?.LogWarning(ex, $"Reading input registers failed: {ex.GetMessage()}, reconnecting.");
					ConnectingTask = GetReconnectTask();
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
		/// <returns>A map of device information and their content as raw bytes.</returns>>
		public async Task<Dictionary<byte, byte[]>> ReadDeviceInformationRaw(byte deviceId, DeviceIDCategory categoryId, DeviceIDObject objectId = DeviceIDObject.VendorName, CancellationToken cancellationToken = default)
		{
			try
			{
				logger?.LogTrace("ModbusClient.ReadDeviceInformationRaw enter");

				if (deviceId < Consts.MinDeviceIdRtu || Consts.MaxDeviceId < deviceId)
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
						throw new IOException("Request timed out");

					if (response.IsError)
						throw new ModbusException(response.ErrorMessage);

					var dict = new Dictionary<byte, byte[]>();
					for (int i = 0, idx = 0; i < response.ObjectCount && idx < response.Data.Length; i++)
					{
						byte objId = response.Data.GetByte(idx);
						idx++;
						byte len = response.Data.GetByte(idx);
						idx++;
						byte[] data = response.Data.GetBytes(idx, len);
						idx += len;

						dict.Add(objId, data);
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
				catch (IOException ex)
				{
					logger?.LogWarning(ex, $"Reading device information failed: {ex.GetMessage()}, reconnecting.");
					ConnectingTask = GetReconnectTask();
				}

				return null;
			}
			finally
			{
				logger?.LogTrace("ModbusClient.ReadDeviceInformationRaw leave");
			}
		}

		#endregion Read methods

		#region Write methods

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

				if (deviceId < Consts.MinDeviceIdRtu || Consts.MaxDeviceId < deviceId)
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
						throw new ModbusException("Response timed out. Device id invalid?");

					if (response.IsError)
						throw new ModbusException(response.ErrorMessage);

					return request.DeviceId == response.DeviceId &&
						request.Function == response.Function &&
						request.Address == response.Address &&
						request.Data.Equals(response.Data);
				}
				catch (IOException ex)
				{
					logger?.LogWarning(ex, $"Writing a coil failed: {ex.GetMessage()}, reconnecting.");
					ConnectingTask = GetReconnectTask();
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

				if (deviceId < Consts.MinDeviceIdRtu || Consts.MaxDeviceId < deviceId)
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
						Address = register.Address
					};

					if (IsLittleEndianRemote)
					{
						request.Data = new DataBuffer(new[] { register.LoByte, register.HiByte });
					}
					else
					{
						request.Data = new DataBuffer(new[] { register.HiByte, register.LoByte });
					}

					var response = await SendRequest(request, cancellationToken);
					if (response.IsTimeout)
						throw new ModbusException("Response timed out. Device id invalid?");

					if (response.IsError)
						throw new ModbusException(response.ErrorMessage);

					return request.DeviceId == response.DeviceId &&
						request.Function == response.Function &&
						request.Address == response.Address &&
						request.Data.Equals(response.Data);
				}
				catch (IOException ex)
				{
					logger?.LogWarning(ex, $"Writing a register failed: {ex.GetMessage()}, reconnecting.");
					ConnectingTask = GetReconnectTask();
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

				if (deviceId < Consts.MinDeviceIdRtu || Consts.MaxDeviceId < deviceId)
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
						throw new ModbusException("Response timed out. Device id invalid?");

					if (response.IsError)
						throw new ModbusException(response.ErrorMessage);

					return request.Address == response.Address &&
						request.Count == response.Count;
				}
				catch (IOException ex)
				{
					logger?.LogWarning(ex, $"Writing coils failed: {ex.GetMessage()}, reconnecting.");
					ConnectingTask = GetReconnectTask();
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

				if (deviceId < Consts.MinDeviceIdRtu || Consts.MaxDeviceId < deviceId)
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

				try
				{
					var request = new Request
					{
						DeviceId = deviceId,
						Function = FunctionCode.WriteMultipleRegisters,
						Address = firstAddress,
						Count = (ushort)orderedList.Count,
						Data = new DataBuffer(orderedList.Count * 2 + 1)
					};

					request.Data.SetByte(0, (byte)(orderedList.Count * 2));
					for (int i = 0; i < orderedList.Count; i++)
					{
						if (IsLittleEndianRemote)
						{
							request.Data.SetBytes(i * 2, new[] { orderedList[i].LoByte, orderedList[i].HiByte });
						}
						else
						{
							request.Data.SetBytes(i * 2, new[] { orderedList[i].HiByte, orderedList[i].LoByte });
						}
					}
					var response = await SendRequest(request, cancellationToken);
					if (response.IsTimeout)
						throw new ModbusException("Response timed out. Device id invalid?");

					if (response.IsError)
						throw new ModbusException(response.ErrorMessage);

					return request.Address == response.Address &&
						request.Count == response.Count;
				}
				catch (IOException ex)
				{
					logger?.LogWarning(ex, $"Writing registers failed: {ex.GetMessage()}, reconnecting.");
					ConnectingTask = GetReconnectTask();
				}

				return false;
			}
			finally
			{
				logger?.LogTrace("ModbusClient.WriteRegisters leave");
			}
		}

		#endregion Write methods

		#endregion Public Methods

		#region Private Methods

		private async Task<Response> SendRequest(Request request, CancellationToken ct)
		{
			try
			{
				logger?.LogTrace("ModbusClient.SendRequest enter");
				CheckDisposed();

				if (!IsConnected)
				{
					if (!isReconnecting)
						ConnectingTask = GetReconnectTask();

					throw new InvalidOperationException("Modbus client is not connected.");
				}

				using (var cts = new CancellationTokenSource())
				using (ct.Register(() => cts.Cancel()))
				using (stopCts.Token.Register(() => cts.Cancel()))
				{
					try
					{
						await sendLock.WaitAsync(cts.Token);
						logger?.LogTrace(request.ToString());

						// clear all data
						await serialPort.BaseStream.FlushAsync();
						serialPort.DiscardInBuffer();
						serialPort.DiscardOutBuffer();

						logger?.LogDebug($"Sending {request}");
						byte[] bytes = request.Serialize();
						await serialPort.WriteAsync(bytes, 0, bytes.Length, cts.Token);
						logger?.LogDebug("Request sent.");

						var responseBytes = new List<byte>
						{
							// Device/Slave ID
							await ReadByte(cts.Token)
						};

						// Function number
						byte fn = await ReadByte(cts.Token);
						responseBytes.Add(fn);

						byte expectedBytes = 0;
						var function = (FunctionCode)((fn & Consts.ErrorMask) > 0 ? fn ^ Consts.ErrorMask : fn);
						switch (function)
						{
							case FunctionCode.ReadCoils:
							case FunctionCode.ReadDiscreteInputs:
							case FunctionCode.ReadHoldingRegisters:
							case FunctionCode.ReadInputRegisters:
								expectedBytes = await ReadByte(cts.Token);
								responseBytes.Add(expectedBytes);
								break;
							case FunctionCode.WriteSingleCoil:
							case FunctionCode.WriteSingleRegister:
							case FunctionCode.WriteMultipleCoils:
							case FunctionCode.WriteMultipleRegisters:
								expectedBytes = 4;
								break;
							case FunctionCode.EncapsulatedInterface:
								responseBytes.AddRange(await ReadBytes(6, cts.Token));
								byte count = responseBytes.Last();
								for (int i = 0; i < count; i++)
								{
									// id
									responseBytes.Add(await ReadByte(cts.Token));
									// length
									expectedBytes = await ReadByte(cts.Token);
									responseBytes.Add(expectedBytes);
									// value
									responseBytes.AddRange(await ReadBytes(expectedBytes, cts.Token));
								}
								expectedBytes = 0;
								break;
							default:
								if ((fn & Consts.ErrorMask) == 0)
									throw new NotImplementedException();

								expectedBytes = 1;
								break;
						}

						expectedBytes += 2; // CRC Check

						responseBytes.AddRange(await ReadBytes(expectedBytes, cts.Token));
						logger?.LogDebug("Response received.");

						return new Response(responseBytes.ToArray());
					}
					catch (OperationCanceledException) when (stopCts.IsCancellationRequested)
					{
						// keep it quiet on shutdown
					}
					catch (TimeoutException)
					{
						// request timed out, will be logged in the requesting method.
						// no need to log here.
					}
					catch (Exception ex)
					{
						logger?.LogError(ex, $"Unexpected error ({ex.GetType().Name}) on send: {ex.GetMessage()}");
					}
					finally
					{
						sendLock.Release();
					}
				}

				return new Response(new byte[] { 0, 0, 0, 0, 0, 0 });
			}
			finally
			{
				logger?.LogTrace("ModbusClient.SendRequest leave");
			}
		}

		private async void Reconnect()
		{
			try
			{
				logger?.LogTrace("ModbusClient.Reconnect enter");
				lock (reconnectLock)
				{
					if (isReconnecting || stopCts.IsCancellationRequested)
						return;

					isReconnecting = true;
				}
				
				logger?.LogInformation($"{(wasConnected ? "Reconnect" : "Connect")} starting.");
				if (wasConnected)
					Task.Run(() => Disconnected?.Invoke(this, EventArgs.Empty)).Forget();

				int timeout = 2;
				int maxTimeout = 30;
				var startTime = DateTime.UtcNow;

				using (stopCts.Token.Register(() => reconnectTcs.TrySetCanceled()))
				{
					while (!stopCts.IsCancellationRequested)
					{
						try
						{
							serialPort?.Dispose();

							serialPort = new SerialPort(PortName)
							{
								BaudRate = (int)BaudRate,
								DataBits = DataBits,
								Parity = Parity,
								StopBits = StopBits,
								Handshake = Handshake,
								ReadTimeout = (int)ReceiveTimeout.TotalMilliseconds,
								WriteTimeout = (int)SendTimeout.TotalMilliseconds
							};

							if (bufferSize > 0)
							{
								serialPort.ReadBufferSize = bufferSize;
								serialPort.WriteBufferSize = bufferSize;
							}

							var task = Task.Run(() => serialPort.Open());
							if (await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(timeout), stopCts.Token)) == task)
							{
								if (serialPort.IsOpen)
								{
									logger?.LogInformation($"{(wasConnected ? "Reconnect" : "Connect")}ed successfully.");
									Task.Run(() => Connected?.Invoke(this, EventArgs.Empty)).Forget();

									reconnectTcs?.TrySetResult(true);
									reconnectTcs = null;
									wasConnected = true;
									return;
								}
								else
								{
									logger?.LogError($"{(wasConnected ? "Reconnect" : "Connect")} failed: Could not open serial port {serialPort.PortName}.");
									reconnectTcs?.TrySetException((Exception)task.Exception ?? new IOException("Serial port not opened."));
									return;
								}
							}
							else if (stopCts.IsCancellationRequested)
							{
								logger?.LogInformation($"{(wasConnected ? "Reconnect" : "Connect")} cancelled.");
								return;
							}
							else
							{
								logger?.LogWarning($"{(wasConnected ? "Reconnect" : "Connect")} failed within {timeout} seconds.");
								timeout += 2;
								if (timeout > maxTimeout)
									timeout = maxTimeout;

								throw new IOException();
							}
						}
						catch (IOException) when (ReconnectTimeSpan == TimeSpan.MaxValue || DateTime.UtcNow <= startTime + ReconnectTimeSpan)
						{
							await Task.Delay(1000, stopCts.Token);
							continue;
						}
						catch (Exception ex)
						{
							logger?.LogError(ex, "ModbusClient.Reconnect failed");
							reconnectTcs?.TrySetException(ex);
							return;
						}
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
				logger?.LogTrace("ModbusClient.Reconnect leave");
			}
		}

		private Task GetReconnectTask()
		{
			lock (reconnectLock)
			{
				if (reconnectTcs == null)
					reconnectTcs = new TaskCompletionSource<bool>();
			}
			Task.Run(() => Reconnect()).Forget();
			return reconnectTcs.Task;
		}

		private SerialRS485 GetDriverState()
		{
			var rs485 = new SerialRS485();
			SafeUnixHandle handle = null;
			try
			{
				handle = UnsafeNativeMethods.Open(PortName, UnsafeNativeMethods.O_RDWR | UnsafeNativeMethods.O_NOCTTY);
				if (UnsafeNativeMethods.IoCtl(handle, UnsafeNativeMethods.TIOCGRS485, ref rs485) == -1)
					throw new UnixIOException();
			}
			finally
			{
				handle?.Close();
			}

			return rs485;
		}

		private void SetDriverState(SerialRS485 rs485)
		{
			SafeUnixHandle handle = null;
			try
			{
				handle = UnsafeNativeMethods.Open(PortName, UnsafeNativeMethods.O_RDWR | UnsafeNativeMethods.O_NOCTTY);
				if (UnsafeNativeMethods.IoCtl(handle, UnsafeNativeMethods.TIOCSRS485, ref rs485) == -1)
					throw new UnixIOException();
			}
			finally
			{
				handle?.Close();
			}
		}

		private void DisconnectInternal()
		{
			try
			{
				logger?.LogTrace("ModbusClient.DisconnectInternal enter");

				if (!isStarted)
					return;

				isStarted = false;

				bool wasConnected = IsConnected;

				try
				{
					reconnectTcs?.TrySetResult(false);
					stopCts?.Cancel();
					reconnectTcs = null;
				}
				catch
				{ }

				try
				{
					serialPort?.Close();
					serialPort?.Dispose();
					serialPort = null;
				}
				catch
				{ }

				if (wasConnected)
					Task.Run(() => Disconnected?.Invoke(this, EventArgs.Empty)).Forget();

				if (driverModified)
				{
					try
					{
						var rs485 = GetDriverState();
						rs485.Flags = serialDriverFlags;
						SetDriverState(rs485);
						driverModified = false;
					}
					catch (Exception ex)
					{
						logger?.LogError(ex, "ModbusClient.Disconnect failed to reset the serial driver state.");
						throw;
					}
				}
			}
			finally
			{
				logger?.LogTrace("ModbusClient.DisconnectInternal leave");
			}
		}

		private async Task<byte> ReadByte(CancellationToken cancellationToken)
		{
			return (await ReadBytes(1, cancellationToken))[0];
		}

		private async Task<byte[]> ReadBytes(int length, CancellationToken cancellationToken)
		{
			if (!IsConnected)
				throw new InvalidOperationException("No connection");

			byte[] buffer = new byte[length];
			for (int offset = 0; offset < buffer.Length;)
			{
				int count = await serialPort.ReadAsync(buffer, offset, buffer.Length - offset, cancellationToken);
				if (count < 1)
					throw new EndOfStreamException($"Expected to read {buffer.Length - offset} more bytes but end of stream is reached.");
				offset += count;
			}

			return buffer;
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
			DisconnectInternal();
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
			if (isDisposed)
				throw new ObjectDisposedException(GetType().FullName);

			return $"Modbus Serial {PortName} - Connected: {IsConnected}";
		}

		#endregion Overrides
	}
}
