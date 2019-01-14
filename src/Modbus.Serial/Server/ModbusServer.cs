using AMWD.Modbus.Common;
using AMWD.Modbus.Common.Interfaces;
using AMWD.Modbus.Common.Structures;
using AMWD.Modbus.Common.Util;
using AMWD.Modbus.Serial.Protocol;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Threading.Tasks;

namespace AMWD.Modbus.Serial.Server
{
	/// <summary>
	/// A server to communicate via Modbus RTU.
	/// </summary>
	public class ModbusServer : IModbusServer
	{
		#region Fields

		private SerialPort serialPort;

		private BaudRate baudRate = BaudRate.Baud38400;
		private int dataBits = 8;
		private Parity parity = Parity.None;
		private StopBits stopBits = StopBits.None;
		private Handshake handshake = Handshake.None;
		private int bufferSize = 4096;
		private int sendTimeout = 1000;
		private int receiveTimeout = 1000;

		private readonly FunctionCode[] availableFunctionCodes = Enum.GetValues(typeof(FunctionCode))
			.Cast<FunctionCode>()
			.ToArray();

		private ConcurrentDictionary<byte, ModbusDevice> modbusDevices = new ConcurrentDictionary<byte, ModbusDevice>();

		#endregion Fields

		#region Events

		/// <summary>
		/// Raised when a coil was written.
		/// </summary>
		public event EventHandler<WriteEventArgs> CoilWritten;

		/// <summary>
		/// Raised when a register was written.
		/// </summary>
		public event EventHandler<WriteEventArgs> RegisterWritten;

		#endregion Events

		#region Constructors

		/// <summary>
		/// Initializes a new instance of the <see cref="ModbusServer"/> class.
		/// </summary>
		/// <param name="portName">The serial port name.</param>
		public ModbusServer(string portName)
		{
			if (string.IsNullOrWhiteSpace(portName))
			{
				throw new ArgumentNullException(nameof(portName));
			}

			PortName = portName;

			Initialization = Task.Run((Action)Initialize);
		}

		#endregion Constructors

		#region Properties

		/// <summary>
		/// Gets the result of the asynchronous initialization of this instance.
		/// </summary>
		public Task Initialization { get; }

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
		/// Gets the UTC timestamp of the server start.
		/// </summary>
		public DateTime StartTime { get; private set; }

		/// <summary>
		/// Gets a value indicating whether the server is running.
		/// </summary>
		public bool IsRunning => serialPort?.IsOpen ?? false;

		/// <summary>
		/// Gets a list of device ids the server handles.
		/// </summary>
		public List<byte> DeviceIds => modbusDevices.Keys.ToList();

		#endregion Properties

		#region Public methods

		#region Coils

		/// <summary>
		/// Returns a coil of a device.
		/// </summary>
		/// <param name="deviceId">The device id.</param>
		/// <param name="coilNumber">The address of the coil.</param>
		/// <returns>The coil.</returns>
		public Coil GetCoil(byte deviceId, ushort coilNumber)
		{
			if (!modbusDevices.TryGetValue(deviceId, out ModbusDevice device))
			{
				throw new ArgumentException($"Device #{deviceId} does not exist");
			}

			return device.GetCoil(coilNumber);
		}

		/// <summary>
		/// Sets the status of a coild to a device.
		/// </summary>
		/// <param name="deviceId">The device id.</param>
		/// <param name="coilNumber">The address of the coil.</param>
		/// <param name="value">The status of the coil.</param>
		public void SetCoil(byte deviceId, ushort coilNumber, bool value)
		{
			if (!modbusDevices.TryGetValue(deviceId, out ModbusDevice device))
			{
				throw new ArgumentException($"Device #{deviceId} does not exist");
			}

			device.SetCoil(coilNumber, value);
		}

		/// <summary>
		/// Sets the status of a coild to a device.
		/// </summary>
		/// <param name="deviceId">The device id.</param>
		/// <param name="coil">The coil.</param>
		public void SetCoil(byte deviceId, Coil coil)
		{
			SetCoil(deviceId, coil.Address, coil.Value);
		}

		#endregion Coils

		#region Discrete Inputs

		/// <summary>
		/// Returns a discrete input of a device.
		/// </summary>
		/// <param name="deviceId">The device id.</param>
		/// <param name="inputNumber">The discrete input address.</param>
		/// <returns>The discrete input.</returns>
		public DiscreteInput GetDiscreteInput(byte deviceId, ushort inputNumber)
		{
			if (!modbusDevices.TryGetValue(deviceId, out ModbusDevice device))
			{
				throw new ArgumentException($"Device #{deviceId} does not exist");
			}

			return device.GetInput(inputNumber);
		}

		/// <summary>
		/// Sets a discrete input of a device.
		/// </summary>
		/// <param name="deviceId">The device id.</param>
		/// <param name="inputNumber">The discrete input address.</param>
		/// <param name="value">A value inidcating whether the input is set.</param>
		public void SetDiscreteInput(byte deviceId, ushort inputNumber, bool value)
		{
			if (!modbusDevices.TryGetValue(deviceId, out ModbusDevice device))
			{
				throw new ArgumentException($"Device #{deviceId} does not exist");
			}

			device.SetInput(inputNumber, value);
		}

		/// <summary>
		/// Sets a discrete input of a device.
		/// </summary>
		/// <param name="deviceId">The device id.</param>
		/// <param name="discreteInput">The discrete input to set.</param>
		public void SetDiscreteInput(byte deviceId, DiscreteInput discreteInput)
		{
			SetDiscreteInput(deviceId, discreteInput.Address, discreteInput.Value);
		}

		#endregion Discrete Inputs

		#region Input Registers

		/// <summary>
		/// Returns an input register of a device.
		/// </summary>
		/// <param name="deviceId">The device id.</param>
		/// <param name="registerNumber">The input register address.</param>
		/// <returns>The input register.</returns>
		public Register GetInputRegister(byte deviceId, ushort registerNumber)
		{
			if (!modbusDevices.TryGetValue(deviceId, out ModbusDevice device))
			{
				throw new ArgumentException($"Device #{deviceId} does not exist");
			}

			return device.GetInputRegister(registerNumber);
		}

		/// <summary>
		/// Sets an input register of a device.
		/// </summary>
		/// <param name="deviceId">The device id.</param>
		/// <param name="registerNumber">The input register address.</param>
		/// <param name="value">The register value.</param>
		public void SetInputRegister(byte deviceId, ushort registerNumber, ushort value)
		{
			if (!modbusDevices.TryGetValue(deviceId, out ModbusDevice device))
			{
				throw new ArgumentException($"Device #{deviceId} does not exist");
			}
			device.SetInputRegister(registerNumber, value);
		}

		/// <summary>
		/// Sets an input register of a device.
		/// </summary>
		/// <param name="deviceId">The device id.</param>
		/// <param name="registerNumber">The input register address.</param>
		/// <param name="highByte">The High-Byte value.</param>
		/// <param name="lowByte">The Low-Byte value.</param>
		public void SetInputRegister(byte deviceId, ushort registerNumber, byte highByte, byte lowByte)
		{
			SetInputRegister(deviceId, new Register { Address = registerNumber, HiByte = highByte, LoByte = lowByte });
		}

		/// <summary>
		/// Sets an input register of a device.
		/// </summary>
		/// <param name="deviceId">The device id.</param>
		/// <param name="register">The input register.</param>
		public void SetInputRegister(byte deviceId, Register register)
		{
			SetInputRegister(deviceId, register.Address, register.Value);
		}

		#endregion Input Registers

		#region Holding Registers

		/// <summary>
		/// Returns a holding register of a device.
		/// </summary>
		/// <param name="deviceId">The device id.</param>
		/// <param name="registerNumber">The holding register address.</param>
		/// <returns>The holding register.</returns>
		public Register GetHoldingRegister(byte deviceId, ushort registerNumber)
		{
			if (!modbusDevices.TryGetValue(deviceId, out ModbusDevice device))
			{
				throw new ArgumentException($"Device #{deviceId} does not exist");
			}

			return device.GetHoldingRegister(registerNumber);
		}

		/// <summary>
		/// Sets a holding register of a device.
		/// </summary>
		/// <param name="deviceId">The device id.</param>
		/// <param name="registerNumber">The holding register address.</param>
		/// <param name="value">The register value.</param>
		public void SetHoldingRegister(byte deviceId, ushort registerNumber, ushort value)
		{
			if (!modbusDevices.TryGetValue(deviceId, out ModbusDevice device))
			{
				throw new ArgumentException($"Device #{deviceId} does not exist");
			}
			device.SetHoldingRegister(registerNumber, value);
		}

		/// <summary>
		/// Sets a holding register of a device.
		/// </summary>
		/// <param name="deviceId">The device id.</param>
		/// <param name="registerNumber">The holding register address.</param>
		/// <param name="highByte">The high byte value.</param>
		/// <param name="lowByte">The low byte value.</param>
		public void SetHoldingRegister(byte deviceId, ushort registerNumber, byte highByte, byte lowByte)
		{
			SetHoldingRegister(deviceId, new Register { Address = registerNumber, HiByte = highByte, LoByte = lowByte });
		}

		/// <summary>
		/// Sets a holding register of a device.
		/// </summary>
		/// <param name="deviceId">The device id.</param>
		/// <param name="register">The register.</param>
		public void SetHoldingRegister(byte deviceId, Register register)
		{
			SetHoldingRegister(deviceId, register.Address, register.Value);
		}

		#endregion Holding Registers

		#region Devices

		/// <summary>
		/// Adds a new device to the server.
		/// </summary>
		/// <param name="deviceId">The id of the new device.</param>
		/// <returns>true on success, otherwise false.</returns>
		public bool AddDevice(byte deviceId)
		{
			return modbusDevices.TryAdd(deviceId, new ModbusDevice(deviceId));
		}

		/// <summary>
		/// Removes a device from the server.
		/// </summary>
		/// <param name="deviceId">The device id to remove.</param>
		/// <returns>true on success, otherwise false.</returns>
		public bool RemoveDevice(byte deviceId)
		{
			return modbusDevices.TryRemove(deviceId, out ModbusDevice device);
		}

		#endregion Devices

		#endregion Public methods

		#region Private methods

		#region Server

		private void Initialize()
		{
			if (isDisposed)
			{
				throw new ObjectDisposedException(GetType().FullName);
			}

			try
			{
				serialPort = new SerialPort
				{
					PortName = PortName,
					BaudRate = (int)BaudRate,
					DataBits = DataBits,
					Parity = Parity,
					StopBits = StopBits,
					Handshake = Handshake,
					ReadBufferSize = BufferSize,
					WriteBufferSize = BufferSize,
					ReadTimeout = ReceiveTimeout,
					WriteTimeout = SendTimeout
				};

				serialPort.DataReceived += OnDataReceived;
				serialPort.Open();

				StartTime = DateTime.UtcNow;
			}
			catch
			{
				serialPort?.Dispose();
				serialPort = null;
				throw;
			}
		}

		private void OnDataReceived(object sender, SerialDataReceivedEventArgs args)
		{
			var requestBytes = new List<byte>();
			do
			{
				var buffer = new byte[BufferSize];
				var count = serialPort.Read(buffer, 0, buffer.Length);
				requestBytes.AddRange(buffer.Take(count));
			}
			while (serialPort.BytesToRead > 0);

			var request = new Request(requestBytes.ToArray());
			var response = HandleRequest(request);

			if (response != null)
			{
				var bytes = response.Serialize();
				serialPort.Write(bytes, 0, bytes.Length);
			}
		}

		private Response HandleRequest(Request request)
		{
			// The device is not known => no response to send.
			if (!modbusDevices.ContainsKey(request.DeviceId))
			{
				return null;
			}

			Response response;
			switch (request.Function)
			{
				case FunctionCode.ReadCoils:
					response = HandleReadCoils(request);
					break;
				case FunctionCode.ReadDiscreteInputs:
					response = HandleReadDiscreteInputs(request);
					break;
				case FunctionCode.ReadHoldingRegisters:
					response = HandleReadHoldingRegisters(request);
					break;
				case FunctionCode.ReadInputRegisters:
					response = HandleReadInputRegisters(request);
					break;
				case FunctionCode.WriteSingleCoil:
					response = HandleWriteSingleCoil(request);
					break;
				case FunctionCode.WriteSingleRegister:
					response = HandleWritSingleRegister(request);
					break;
				case FunctionCode.WriteMultipleCoils:
					response = HandleWriteMultipleCoils(request);
					break;
				case FunctionCode.WriteMultipleRegisters:
					response = HandleWriteMultipleRegisters(request);
					break;
				default:
					response = new Response(request)
					{
						ErrorCode = ErrorCode.IllegalFunction
					};
					break;
			}

			return response;
		}

		#endregion Server

		#region Function Implementations

		#region Read requests

		private Response HandleReadCoils(Request request)
		{
			var response = new Response(request);

			try
			{
				if (request.Count < Consts.MinCount || request.Count > Consts.MaxCoilCountRead)
				{
					response.ErrorCode = ErrorCode.IllegalDataValue;
				}
				else if (request.Address < Consts.MinAddress || request.Address + request.Count > Consts.MaxAddress)
				{
					response.ErrorCode = ErrorCode.IllegalDataAddress;
				}
				else
				{
					try
					{
						var len = (int)Math.Ceiling(request.Count / 8.0);
						response.Data = new DataBuffer(len);
						for (int i = 0; i < request.Count; i++)
						{
							var addr = (ushort)(request.Address + i);
							if (GetCoil(request.DeviceId, addr).Value)
							{
								var posByte = i / 8;
								var posBit = i % 8;

								var mask = (byte)Math.Pow(2, posBit);
								response.Data[posByte] = (byte)(response.Data[posByte] | mask);
							}
						}
					}
					catch
					{
						response.ErrorCode = ErrorCode.SlaveDeviceFailure;
					}
				}
			}
			catch
			{
				return null;
			}

			return response;
		}

		private Response HandleReadDiscreteInputs(Request request)
		{
			var response = new Response(request);
			try
			{
				if (request.Count < Consts.MinCount || request.Count > Consts.MaxCoilCountRead)
				{
					response.ErrorCode = ErrorCode.IllegalDataValue;
				}
				else if (request.Address < Consts.MinAddress || request.Address + request.Count > Consts.MaxAddress)
				{
					response.ErrorCode = ErrorCode.IllegalDataAddress;
				}
				else
				{
					try
					{
						var len = (int)Math.Ceiling(request.Count / 8.0);
						response.Data = new DataBuffer(len);
						for (int i = 0; i < request.Count; i++)
						{
							var addr = (ushort)(request.Address + i);
							if (GetDiscreteInput(request.DeviceId, addr).Value)
							{
								var posByte = i / 8;
								var posBit = i % 8;

								var mask = (byte)Math.Pow(2, posBit);
								response.Data[posByte] = (byte)(response.Data[posByte] | mask);
							}
						}
					}
					catch
					{
						response.ErrorCode = ErrorCode.SlaveDeviceFailure;
					}
				}
			}
			catch
			{
				return null;
			}

			return response;
		}

		private Response HandleReadHoldingRegisters(Request request)
		{
			var response = new Response(request);
			try
			{
				if (request.Count < Consts.MinCount || request.Count > Consts.MaxRegisterCountRead)
				{
					response.ErrorCode = ErrorCode.IllegalDataValue;
				}
				else if (request.Address < Consts.MinAddress || request.Address + request.Count > Consts.MaxAddress)
				{
					response.ErrorCode = ErrorCode.IllegalDataAddress;
				}
				else
				{
					try
					{
						response.Data = new DataBuffer(request.Count * 2);
						for (int i = 0; i < request.Count; i++)
						{
							var addr = (ushort)(request.Address + i);
							var reg = GetHoldingRegister(request.DeviceId, addr);
							response.Data.SetUInt16(i * 2, reg.Value);
						}
					}
					catch
					{
						response.ErrorCode = ErrorCode.SlaveDeviceFailure;
					}
				}
			}
			catch
			{
				return null;
			}

			return response;
		}

		private Response HandleReadInputRegisters(Request request)
		{
			var response = new Response(request);

			try
			{
				if (request.Count < Consts.MinCount || request.Count > Consts.MaxRegisterCountRead)
				{
					response.ErrorCode = ErrorCode.IllegalDataValue;
				}
				else if (request.Address < Consts.MinAddress || request.Address + request.Count > Consts.MaxAddress)
				{
					response.ErrorCode = ErrorCode.IllegalDataAddress;
				}
				else
				{
					try
					{
						response.Data = new DataBuffer(request.Count * 2);
						for (int i = 0; i < request.Count; i++)
						{
							var addr = (ushort)(request.Address + i);
							var reg = GetInputRegister(request.DeviceId, addr);
							response.Data.SetUInt16(i * 2, reg.Value);
						}
					}
					catch
					{
						response.ErrorCode = ErrorCode.SlaveDeviceFailure;
					}
				}
			}
			catch
			{
				return null;
			}

			return response;
		}

		#endregion Read requests

		#region Write requests

		private Response HandleWriteSingleCoil(Request request)
		{
			var response = new Response(request);

			try
			{
				var val = request.Data.GetUInt16(0);
				if (val != 0x0000 && val != 0xFF00)
				{
					response.ErrorCode = ErrorCode.IllegalDataValue;
				}
				else if (request.Address < Consts.MinAddress || request.Address > Consts.MaxAddress)
				{
					response.ErrorCode = ErrorCode.IllegalDataAddress;
				}
				else
				{
					try
					{
						var coil = new Coil { Address = request.Address, Value = (val > 0) };

						SetCoil(request.DeviceId, coil);
						response.Data = request.Data;

						CoilWritten?.Invoke(this, new WriteEventArgs(request.DeviceId, coil));
					}
					catch
					{
						response.ErrorCode = ErrorCode.SlaveDeviceFailure;
					}
				}
			}
			catch
			{
				return null;
			}

			return response;
		}

		private Response HandleWritSingleRegister(Request request)
		{
			var response = new Response(request);

			try
			{
				var val = request.Data.GetUInt16(0);

				if (request.Address < Consts.MinAddress || request.Address > Consts.MaxAddress)
				{
					response.ErrorCode = ErrorCode.IllegalDataAddress;
				}
				else
				{
					try
					{
						var register = new Register { Address = request.Address, Value = val };

						SetHoldingRegister(request.DeviceId, register);
						response.Data = request.Data;

						RegisterWritten?.Invoke(this, new WriteEventArgs(request.DeviceId, register));
					}
					catch
					{
						response.ErrorCode = ErrorCode.SlaveDeviceFailure;
					}
				}
			}
			catch
			{
				return null;
			}

			return response;
		}

		private Response HandleWriteMultipleCoils(Request request)
		{
			try
			{
				var response = new Response(request);

				var numBytes = (int)Math.Ceiling(request.Count / 8.0);
				if (request.Count < Consts.MinCount || request.Count > Consts.MaxCoilCountWrite || numBytes != request.Data.Length)
				{
					response.ErrorCode = ErrorCode.IllegalDataValue;
				}
				else if (request.Address < Consts.MinAddress || request.Address + request.Count > Consts.MaxAddress)
				{
					response.ErrorCode = ErrorCode.IllegalDataAddress;
				}
				else
				{
					try
					{
						var list = new List<Coil>();
						for (int i = 0; i < request.Count; i++)
						{
							var addr = (ushort)(request.Address + i);

							var posByte = i / 8;
							var posBit = i % 8;

							var mask = (byte)Math.Pow(2, posBit);
							var val = request.Data[posByte] & mask;

							var coil = new Coil { Address = addr, Value = (val > 0) };
							SetCoil(request.DeviceId, coil);
							list.Add(coil);
						}
						CoilWritten?.Invoke(this, new WriteEventArgs(request.DeviceId, list));
					}
					catch
					{
						response.ErrorCode = ErrorCode.SlaveDeviceFailure;
					}
				}

				return response;
			}
			catch
			{
				return null;
			}
		}

		private Response HandleWriteMultipleRegisters(Request request)
		{
			try
			{
				var response = new Response(request);

				if (request.Count < Consts.MinCount || request.Count > Consts.MaxRegisterCountWrite || request.Count * 2 != request.Data.Length)
				{
					response.ErrorCode = ErrorCode.IllegalDataValue;
				}
				else if (request.Address < Consts.MinAddress || request.Address + request.Count > Consts.MaxAddress)
				{
					response.ErrorCode = ErrorCode.IllegalDataAddress;
				}
				else
				{
					try
					{
						var list = new List<Register>();
						for (int i = 0; i < request.Count; i++)
						{
							var addr = (ushort)(request.Address + i);
							var val = request.Data.GetUInt16(i * 2);

							var register = new Register { Address = addr, Value = val };
							SetHoldingRegister(request.DeviceId, register);
							list.Add(register);
						}
						RegisterWritten?.Invoke(this, new WriteEventArgs(request.DeviceId, list));
					}
					catch
					{
						response.ErrorCode = ErrorCode.SlaveDeviceFailure;
					}
				}

				return response;
			}
			catch
			{
				return null;
			}
		}

		#endregion Write requests

		#endregion Function Implementations

		#endregion Private methods

		#region IDisposable implementation

		/// <summary>
		/// Releases all managed and unmanaged resources used by the <see cref="ModbusServer"/>.
		/// </summary>
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		private bool isDisposed;

		private void Dispose(bool disposing)
		{
			if (disposing)
			{
				serialPort?.Close();
				serialPort?.Dispose();
				serialPort = null;
			}

			isDisposed = true;
		}

		#endregion IDisposable implementation
	}
}
