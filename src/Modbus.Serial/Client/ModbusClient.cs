using AMWD.Modbus.Common;
using AMWD.Modbus.Common.Interfaces;
using AMWD.Modbus.Common.Structures;
using AMWD.Modbus.Common.Util;
using AMWD.Modbus.Serial.Protocol;
using AMWD.Modbus.Serial.Util;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AMWD.Modbus.Serial.Client
{
	/// <summary>
	/// A client to communicate with modbus devices via serial port.
	/// </summary>
	public class ModbusClient : IModbusClient
	{
		#region Fields

		// Optional logger for all actions
		private readonly ILogger<ModbusClient> logger;

		// The serial port to connect to the remote.
		private SerialPort serialPort;
		// And the settings.
		private BaudRate baudRate = BaudRate.Baud38400;
		private int dataBits = 8;
		private Parity parity = Parity.None;
		private StopBits stopBits = StopBits.None;
		private Handshake handshake = Handshake.None;
		private int sendTimeout = 1000;
		private int receiveTimeout = 1000;
		private int bufferSize = 4096;

		// driver switch
		private RS485Flags serialDriverFlags;
		private bool driverModified;

		// Connection handling
		private CancellationTokenSource mainCts;
		private bool isStarted = false;
		private bool wasConnected = false;
		private bool isReconnecting = false;
		private TaskCompletionSource<bool> reconnectTcs;
		private readonly SemaphoreSlim sendMutex = new SemaphoreSlim(1, 1);

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
		public ModbusClient(string portName, ILogger<ModbusClient> logger = null)
		{
			this.logger = logger;

			if (string.IsNullOrWhiteSpace(portName))
			{
				throw new ArgumentNullException(nameof(portName), "Portname has to be set");
			}
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
			get
			{
				return baudRate;
			}
			set
			{
				baudRate = value;
				if (serialPort != null)
				{
					serialPort.BaudRate = (int)value;
				}
			}
		}

		/// <summary>
		/// Gets or sets the number of data bits. Default: 8.
		/// </summary>
		public int DataBits
		{
			get
			{
				return dataBits;
			}
			set
			{
				dataBits = value;
				if (serialPort != null)
				{
					serialPort.DataBits = value;
				}
			}
		}

		/// <summary>
		/// Gets or sets the parity. Default: None.
		/// </summary>
		public Parity Parity
		{
			get
			{
				return parity;
			}
			set
			{
				parity = value;
				if (serialPort != null)
				{
					serialPort.Parity = value;
				}
			}
		}

		/// <summary>
		/// Gets or sets the number of stop bits. Default: None.
		/// </summary>
		public StopBits StopBits
		{
			get
			{
				return stopBits;
			}
			set
			{
				stopBits = value;
				if (serialPort != null)
				{
					serialPort.StopBits = value;
				}
			}
		}

		/// <summary>
		/// Gets or sets the handshake. Default: None.
		/// </summary>
		public Handshake Handshake
		{
			get
			{
				return handshake;
			}
			set
			{
				handshake = value;
				if (serialPort != null)
				{
					serialPort.Handshake = value;
				}
			}
		}

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
		public int SendTimeout
		{
			get
			{
				return sendTimeout;
			}
			set
			{
				sendTimeout = value;
				if (serialPort != null)
				{
					serialPort.WriteTimeout = value;
				}
			}
		}

		/// <summary>
		/// Gets or sets the receive timeout in milliseconds. Default 1000 (recommended).
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
				if (serialPort != null)
				{
					serialPort.ReadTimeout = value;
				}
			}
		}

		/// <summary>
		/// Gets or sets buffer size in bytes.
		/// </summary>
		public int BufferSize
		{
			get
			{
				return bufferSize;
			}
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
		/// <returns>An awaitable task.</returns>
		public Task Connect()
		{
			logger?.LogTrace("ModbusClient.Connect");
			if (isDisposed)
			{
				throw new ObjectDisposedException(GetType().FullName);
			}

			if (isStarted)
			{
				return ConnectingTask;
			}

			isStarted = true;
			logger?.LogInformation("ModbusClient starting");

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
					logger.LogError(ex, "ModbusClient.Connect faild to set RS485 serial driver state.");
					throw;
				}
			}

			wasConnected = false;
			mainCts = new CancellationTokenSource();

			Task.Run(() => Reconnect(mainCts.Token));
			ConnectingTask = GetWaitTask(mainCts.Token);

			return ConnectingTask;
		}

		/// <summary>
		/// Disconnects the client.
		/// </summary>
		/// <returns>An awaitable task.</returns>
		public Task Disconnect()
		{
			DisconnectInternal(false);
			return Task.CompletedTask;
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
			if (deviceId < Consts.MinDeviceIdRtu || Consts.MaxDeviceId < deviceId)
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
				var response = await SendRequest(request, mainCts.Token);
				if (response.IsTimeout)
				{
					throw new IOException("Request timed out");
				}
				if (response.IsError)
				{
					throw new ModbusException(response.ErrorMessage);
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
			catch (IOException ex)
			{
				logger?.LogWarning(ex, "Reading coils. Reconnecting.");
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
		/// <returns>A list of discrete inputs or null on error.</returns>
		public async Task<List<DiscreteInput>> ReadDiscreteInputs(byte deviceId, ushort startAddress, ushort count)
		{
			logger?.LogTrace($"ModbusClient.ReadDiscreteInputs({deviceId}, {startAddress}, {count})");
			if (deviceId < Consts.MinDeviceIdRtu || Consts.MaxDeviceId < deviceId)
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
				var response = await SendRequest(request, mainCts.Token);
				if (response.IsTimeout)
				{
					throw new IOException("Request timed out");
				}
				if (response.IsError)
				{
					throw new ModbusException(response.ErrorMessage);
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
			catch (IOException ex)
			{
				logger?.LogWarning(ex, "Reading discrete inputs. Reconnecting.");
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
		/// <returns>A list of registers or null on error.</returns>
		public async Task<List<Register>> ReadHoldingRegisters(byte deviceId, ushort startAddress, ushort count)
		{
			logger?.LogTrace($"ModbusClient.ReadHoldingRegisters({deviceId}, {startAddress}, {count})");
			if (deviceId < Consts.MinDeviceIdRtu || Consts.MaxDeviceId < deviceId)
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
				var response = await SendRequest(request, mainCts.Token);
				if (response.IsTimeout)
				{
					throw new IOException("Request timed out");
				}
				if (response.IsError)
				{
					throw new ModbusException(response.ErrorMessage);
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
			catch (IOException ex)
			{
				logger?.LogWarning(ex, "Reading holding registers. Reconnecting.");
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
		/// <returns>A list of registers or null on error.</returns>
		public async Task<List<Register>> ReadInputRegisters(byte deviceId, ushort startAddress, ushort count)
		{
			logger?.LogTrace($"ModbusClient.ReadInputRegisters({deviceId}, {startAddress}, {count})");
			if (deviceId < Consts.MinDeviceIdRtu || Consts.MaxDeviceId < deviceId)
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
				var response = await SendRequest(request, mainCts.Token);
				if (response.IsTimeout)
				{
					throw new IOException("Request timed out");
				}
				if (response.IsError)
				{
					throw new ModbusException(response.ErrorMessage);
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
			catch (IOException ex)
			{
				logger?.LogWarning(ex, "Reading input registers. Reconnecting.");
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
			if (deviceId < Consts.MinDeviceIdRtu || Consts.MaxDeviceId < deviceId)
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
				var response = await SendRequest(request, mainCts.Token);

				if (response.IsTimeout)
				{
					throw new IOException("Request timed out");
				}
				if (response.IsError)
				{
					throw new ModbusException(response.ErrorMessage);
				}

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
				logger?.LogWarning(ex, "Reading device information. Reconnecting.");
				Task.Run(() => Reconnect(mainCts.Token)).Forget();
				ConnectingTask = GetWaitTask(mainCts.Token);
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
			logger?.LogTrace($"ModbusClient.WriteSingleRegister({deviceId}, {coil})");
			if (coil == null)
			{
				throw new ArgumentNullException(nameof(coil));
			}
			if (deviceId < Consts.MinDeviceIdRtu || Consts.MaxDeviceId < deviceId)
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
				var response = await SendRequest(request, mainCts.Token);
				if (response.IsTimeout)
				{
					throw new ModbusException("Response timed out. Device id invalid?");
				}
				if (response.IsError)
				{
					throw new ModbusException(response.ErrorMessage);
				}

				return request.DeviceId == response.DeviceId &&
					request.Function == response.Function &&
					request.Address == response.Address &&
					request.Data.Equals(response.Data);
			}
			catch (IOException ex)
			{
				logger?.LogWarning(ex, "Writing single coil. Reconnecting.");
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
		/// <returns>true on success, otherwise false.</returns>
		public async Task<bool> WriteSingleRegister(byte deviceId, Register register)
		{
			logger?.LogTrace($"ModbusClient.WriteSingleRegister({deviceId}, {register})");
			if (register == null)
			{
				throw new ArgumentNullException(nameof(register));
			}
			if (deviceId < Consts.MinDeviceIdRtu || Consts.MaxDeviceId < deviceId)
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
				var response = await SendRequest(request, mainCts.Token);
				if (response.IsTimeout)
				{
					throw new ModbusException("Response timed out. Device id invalid?");
				}
				if (response.IsError)
				{
					throw new ModbusException(response.ErrorMessage);
				}

				return request.DeviceId == response.DeviceId &&
					request.Function == response.Function &&
					request.Address == response.Address &&
					request.Data.Equals(response.Data);
			}
			catch (IOException ex)
			{
				logger?.LogWarning(ex, "Writing single register. Reconnecting.");
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
		/// <returns>true on success, otherwise false.</returns>
		public async Task<bool> WriteCoils(byte deviceId, IEnumerable<Coil> coils)
		{
			logger?.LogTrace($"ModbusClient.WriteCoils({deviceId}, Length: {coils.Count()})");
			if (coils == null || !coils.Any())
			{
				throw new ArgumentNullException(nameof(coils));
			}
			if (deviceId < Consts.MinDeviceIdRtu || Consts.MaxDeviceId < deviceId)
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
				var response = await SendRequest(request, mainCts.Token);
				if (response.IsTimeout)
				{
					throw new ModbusException("Response timed out. Device id invalid?");
				}
				if (response.IsError)
				{
					throw new ModbusException(response.ErrorMessage);
				}

				return request.Address == response.Address &&
					request.Count == response.Count;
			}
			catch (IOException ex)
			{
				logger?.LogWarning(ex, "Writing coils. Reconnecting.");
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
		/// <returns>true on success, otherwise false.</returns>
		public async Task<bool> WriteRegisters(byte deviceId, IEnumerable<Register> registers)
		{
			logger?.LogTrace($"ModbusClient.WriteRegisters({deviceId}, Length: {registers.Count()})");
			if (registers == null || !registers.Any())
			{
				throw new ArgumentNullException(nameof(registers));
			}
			if (deviceId < Consts.MinDeviceIdRtu || Consts.MaxDeviceId < deviceId)
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
					request.Data.SetUInt16(i * 2 + 1, orderedList[i].Value);
				}
				var response = await SendRequest(request, mainCts.Token);
				if (response.IsTimeout)
				{
					throw new ModbusException("Response timed out. Device id invalid?");
				}
				if (response.IsError)
				{
					throw new ModbusException(response.ErrorMessage);
				}

				return request.Address == response.Address &&
					request.Count == response.Count;
			}
			catch (IOException ex)
			{
				logger?.LogWarning(ex, "Writing registers. Reconnecting.");
				Task.Run(() => Reconnect(mainCts.Token)).Forget();
				ConnectingTask = GetWaitTask(mainCts.Token);
			}

			return false;
		}

		#endregion Write methods

		#endregion Public Methods

		#region Private Methods

		private async Task<Response> SendRequest(Request request, CancellationToken ct)
		{
			if (isDisposed)
			{
				throw new ObjectDisposedException(GetType().FullName);
			}

			logger?.LogTrace("ModbusClient.SendRequest");

			if (!IsConnected)
			{
				if (!isReconnecting)
				{
					Task.Run(() => Reconnect(mainCts.Token)).Forget();
					ConnectingTask = GetWaitTask(mainCts.Token);
				}
				throw new InvalidOperationException("Client is not connected");
			}

			try
			{
				await sendMutex.WaitAsync(ct);

				logger?.LogTrace(request.ToString());

				var bytes = request.Serialize();
				serialPort.Write(bytes, 0, bytes.Length);

				var responseBytes = new List<byte>
				{
					// Device/Slave ID
					ReadByte()
				};

				// Function number
				var fn = ReadByte();
				responseBytes.Add(fn);

				byte expectedBytes = 0;
				var function = (FunctionCode)fn;
				switch (function)
				{
					case FunctionCode.ReadCoils:
					case FunctionCode.ReadDiscreteInputs:
					case FunctionCode.ReadHoldingRegisters:
					case FunctionCode.ReadInputRegisters:
						expectedBytes = ReadByte();
						responseBytes.Add(expectedBytes);
						break;
					case FunctionCode.WriteSingleCoil:
					case FunctionCode.WriteSingleRegister:
					case FunctionCode.WriteMultipleCoils:
					case FunctionCode.WriteMultipleRegisters:
						expectedBytes = 4;
						break;
					case FunctionCode.EncapsulatedInterface:
						responseBytes.AddRange(ReadBytes(6));
						var count = responseBytes.Last();
						for (var i = 0; i < count; i++)
						{
							// id
							responseBytes.Add(ReadByte());
							// length
							expectedBytes = ReadByte();
							responseBytes.Add(expectedBytes);
							// value
							responseBytes.AddRange(ReadBytes(expectedBytes));
						}
						expectedBytes = 0;
						break;
					default:
						if ((fn & Consts.ErrorMask) == 0)
						{
							throw new NotImplementedException();
						}

						expectedBytes = 1;
						break;
				}

				expectedBytes += 2; // CRC Check

				responseBytes.AddRange(ReadBytes(expectedBytes));

				logger?.LogTrace($"Response received");

				await Task.CompletedTask;
				return new Response(responseBytes.ToArray());
			}
			catch (Exception ex)
			{
				logger?.LogError(ex, "Sending Request");
			}
			finally
			{
				sendMutex.Release();
			}

			return new Response(new byte[] { 0, 0, 0, 0, 0, 0 });
		}

		private async void Reconnect(CancellationToken ct)
		{
			if (isReconnecting || ct.IsCancellationRequested)
			{
				return;
			}

			isReconnecting = true;
			reconnectTcs = new TaskCompletionSource<bool>();

			if (wasConnected)
			{
				Task.Run(() => Disconnected?.Invoke(this, EventArgs.Empty)).Forget();
			}

			ConnectingTask = GetWaitTask(ct);
			int timeout = 2;
			int maxTimeout = 30;
			var startTime = DateTime.UtcNow;

			using (ct.Register(() => reconnectTcs.TrySetCanceled()))
			{
				try
				{
					while (!ct.IsCancellationRequested)
					{
						try
						{
							serialPort?.Dispose();
							serialPort = new SerialPort
							{
								PortName = PortName,
								BaudRate = (int)BaudRate,
								DataBits = DataBits,
								Parity = Parity,
								StopBits = StopBits,
								Handshake = Handshake,
								ReadTimeout = ReceiveTimeout,
								WriteTimeout = SendTimeout,
								ReadBufferSize = bufferSize,
								WriteBufferSize = bufferSize
							};

							var task = Task.Run(() => serialPort.Open());
							if (await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(timeout), ct)) == task && serialPort.IsOpen)
							{
								logger?.LogInformation("ModbusClient.Reconnect connected");
								Task.Run(() => Connected?.Invoke(this, EventArgs.Empty)).Forget();
								reconnectTcs.TrySetResult(true);
								reconnectTcs = null;
								wasConnected = true;
								return;
							}
							else if (ct.IsCancellationRequested)
							{
								logger?.LogWarning("ModbusClient.Reconnect was cancelled");
								return;
							}
							else
							{
								logger?.LogWarning($"ModbusClient.Reconnect failed to connect withing {timeout} seconds");
								timeout += 2;
								if (timeout > maxTimeout)
								{
									timeout = maxTimeout;
								}

								throw new IOException();
							}
						}
						catch (IOException) when (ReconnectTimeSpan == TimeSpan.MaxValue || DateTime.UtcNow <= startTime + ReconnectTimeSpan)
						{
							await Task.Delay(1000, ct);
							continue;
						}
						catch (Exception ex)
						{
							logger?.LogError(ex, "ModbusClient.Reconnect failed");
							reconnectTcs.TrySetException(ex);
						}
					}
				}
				finally
				{
					isReconnecting = false;
				}
			}
		}

		private async Task GetWaitTask(CancellationToken ct)
		{
			var rTcs = reconnectTcs;
			if (rTcs != null)
			{
				await rTcs.Task;
			}
			else
			{
				await Task.Run(() => SpinWait.SpinUntil(() => IsConnected || ct.IsCancellationRequested));
			}
		}

		private SerialRS485 GetDriverState()
		{
			var rs485 = new SerialRS485();
			SafeUnixHandle handle = null;
			try
			{
				handle = UnsafeNativeMethods.Open(PortName, UnsafeNativeMethods.O_RDWR | UnsafeNativeMethods.O_NOCTTY);
				if (UnsafeNativeMethods.IoCtl(handle, UnsafeNativeMethods.TIOCGRS485, ref rs485) == -1)
				{
					throw new UnixIOException();
				}
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
				{
					throw new UnixIOException();
				}
			}
			finally
			{
				handle?.Close();
			}
		}

		private void DisconnectInternal(bool disposing)
		{
			logger?.LogTrace("ModbusClient.Disconnect");
			if (isDisposed && !disposing)
			{
				throw new ObjectDisposedException(GetType().FullName);
			}

			if (!isStarted)
			{
				return;
			}
			isStarted = false;

			bool wasConnected = IsConnected;

			try
			{
				reconnectTcs?.TrySetResult(false);
				mainCts?.Cancel();
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
			{
				Task.Run(() => Disconnected?.Invoke(this, EventArgs.Empty)).Forget();
			}

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

		private byte ReadByte()
		{
			return ReadBytes(1)[0];
		}

		private byte[] ReadBytes(int length)
		{
			if (!IsConnected)
			{
				throw new InvalidOperationException("No connection");
			}

			var bytes = new List<byte>(length);
			do
			{
				var buffer = new byte[length];
				var count = serialPort.Read(buffer, 0, buffer.Length);
				bytes.AddRange(buffer.Take(count));
				length -= count;
			}
			while (length > 0);

			return bytes.ToArray();
		}

		#endregion Private Methods

		#region IDisposable implementation

		/// <summary>
		/// Releases all managed and unmanaged resources used by the <see cref="ModbusClient"/>.
		/// </summary>
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		private bool isDisposed;

		private void Dispose(bool disposing)
		{
			if (isDisposed)
			{
				return;
			}
			isDisposed = true;
			DisconnectInternal(true);
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

			return $"Modbus Serial {PortName} - Connected: {IsConnected}";
		}

		#endregion Overrides
	}
}
