using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Threading.Tasks;
using AMWD.Modbus.Common;
using AMWD.Modbus.Common.Interfaces;
using AMWD.Modbus.Common.Structures;
using AMWD.Modbus.Common.Util;
using AMWD.Modbus.Serial.Protocol;
using AMWD.Modbus.Serial.Util;
using Microsoft.Extensions.Logging;

namespace AMWD.Modbus.Serial.Server
{
	/// <summary>
	/// A handler to process the modbus requests.
	/// </summary>
	/// <param name="request">The request to process.</param>
	/// <returns>The response.</returns>
	public delegate Response ModbusSerialRequestHandler(Request request);

	/// <summary>
	/// A server to communicate via Modbus RTU.
	/// </summary>
	public class ModbusServer : IModbusServer
	{
		#region Fields

		private readonly ILogger logger;

		private SerialPort serialPort;

		private BaudRate baudRate = BaudRate.Baud38400;
		private int dataBits = 8;
		private Parity parity = Parity.None;
		private StopBits stopBits = StopBits.None;
		private Handshake handshake = Handshake.None;
		private int bufferSize = 4096;
		private TimeSpan timeout = TimeSpan.FromSeconds(1);

		private readonly ModbusSerialRequestHandler requestHandler;

		private readonly ConcurrentDictionary<byte, ModbusDevice> modbusDevices = new ConcurrentDictionary<byte, ModbusDevice>();

		#endregion Fields

		#region Events

		/// <summary>
		/// Raised when a coil was written.
		/// </summary>
		public event EventHandler<WriteEventArgs> InputWritten;

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
		/// <param name="logger">A logger.</param>
		/// <param name="requestHandler">Set this request handler to override the default implemented handling. (Default: serving the data provided by Set* methods)</param>
		public ModbusServer(string portName, ILogger logger = null, ModbusSerialRequestHandler requestHandler = null)
		{
			this.logger = logger;

			if (string.IsNullOrWhiteSpace(portName))
				throw new ArgumentNullException(nameof(portName));

			this.requestHandler = requestHandler ?? HandleRequest;

			PortName = portName;

			Initialization = Task.Run(Initialize);
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ModbusServer"/> class.
		/// </summary>
		/// <param name="portName">The serial port name.</param>
		/// <param name="requestHandler">Set this request handler to override the default implemented handling. (Default: serving the data provided by Set* methods)</param>
		public ModbusServer(string portName, ModbusSerialRequestHandler requestHandler)
			: this(portName, null, requestHandler)
		{ }

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
		/// Gets or sets buffer size in bytes.
		/// </summary>
		public int BufferSize
		{
			get => bufferSize;
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
		/// Gets or sets the send/receive timeout. Default: 1 second (recommended).
		/// </summary>
		public TimeSpan Timeout
		{
			get => timeout;
			set
			{
				timeout = value;
				if (serialPort != null)
				{
					serialPort.ReadTimeout = (int)value.TotalMilliseconds;
					serialPort.WriteTimeout = (int)value.TotalMilliseconds;
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
			try
			{
				logger?.LogTrace("ModbusServer.GetCoil enter");
				CheckDisposed();

				if (!modbusDevices.TryGetValue(deviceId, out ModbusDevice device))
					throw new ArgumentException($"Device #{deviceId} does not exist");

				return device.GetCoil(coilNumber);
			}
			finally
			{
				logger?.LogTrace("ModbusServer.GetCoil leave");
			}
		}

		/// <summary>
		/// Sets the status of a coild to a device.
		/// </summary>
		/// <param name="deviceId">The device id.</param>
		/// <param name="coilNumber">The address of the coil.</param>
		/// <param name="value">The status of the coil.</param>
		public void SetCoil(byte deviceId, ushort coilNumber, bool value)
		{
			try
			{
				logger?.LogTrace("ModbusServer.SetCoil(byte, ushort, bool) enter");
				CheckDisposed();

				if (!modbusDevices.TryGetValue(deviceId, out ModbusDevice device))
					throw new ArgumentException($"Device #{deviceId} does not exist");

				device.SetCoil(coilNumber, value);
			}
			finally
			{
				logger?.LogTrace("ModbusServer.SetCoil(byte, ushort, bool) leave");
			}
		}

		/// <summary>
		/// Sets the status of a coild to a device.
		/// </summary>
		/// <param name="deviceId">The device id.</param>
		/// <param name="coil">The coil.</param>
		public void SetCoil(byte deviceId, ModbusObject coil)
		{
			try
			{
				logger?.LogTrace("ModbusServer.SetCoil(byte, ModbusObject) enter");
				CheckDisposed();

				if (coil.Type != ModbusObjectType.Coil)
					throw new ArgumentException("Invalid coil type set");

				SetCoil(deviceId, coil.Address, coil.BoolValue);
			}
			finally
			{
				logger?.LogTrace("ModbusServer.SetCoil(byte, ModbusObject) leave");
			}
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
			try
			{
				logger?.LogTrace("ModbusServer.GetDiscreteInput enter");
				CheckDisposed();

				if (!modbusDevices.TryGetValue(deviceId, out ModbusDevice device))
					throw new ArgumentException($"Device #{deviceId} does not exist");

				return device.GetInput(inputNumber);
			}
			finally
			{
				logger?.LogTrace("ModbusServer.GetDiscreteInput leave");
			}
		}

		/// <summary>
		/// Sets a discrete input of a device.
		/// </summary>
		/// <param name="deviceId">The device id.</param>
		/// <param name="inputNumber">The discrete input address.</param>
		/// <param name="value">A value inidcating whether the input is set.</param>
		public void SetDiscreteInput(byte deviceId, ushort inputNumber, bool value)
		{
			try
			{
				logger?.LogTrace("ModbusServer.SetDiscreteInput(byte, ushort, bool) enter");
				CheckDisposed();

				if (!modbusDevices.TryGetValue(deviceId, out ModbusDevice device))
					throw new ArgumentException($"Device #{deviceId} does not exist");

				device.SetInput(inputNumber, value);
			}
			finally
			{
				logger?.LogTrace("ModbusServer.SetDiscreteInput(byte, ushort, bool) leave");
			}
		}

		/// <summary>
		/// Sets a discrete input of a device.
		/// </summary>
		/// <param name="deviceId">The device id.</param>
		/// <param name="discreteInput">The discrete input to set.</param>
		public void SetDiscreteInput(byte deviceId, ModbusObject discreteInput)
		{
			try
			{
				logger?.LogTrace("ModbusServer.SetDiscreteInput(byte, ModbusObject) enter");
				CheckDisposed();

				if (discreteInput.Type != ModbusObjectType.DiscreteInput)
					throw new ArgumentException("Invalid input type set");

				SetDiscreteInput(deviceId, discreteInput.Address, discreteInput.BoolValue);
			}
			finally
			{
				logger?.LogTrace("ModbusServer.SetDiscreteInput(byte, ModbusObject) leave");
			}
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
			try
			{
				logger?.LogTrace("ModbusServer.GetInputRegister enter");
				CheckDisposed();

				if (!modbusDevices.TryGetValue(deviceId, out ModbusDevice device))
					throw new ArgumentException($"Device #{deviceId} does not exist");

				return device.GetInputRegister(registerNumber);
			}
			finally
			{
				logger?.LogTrace("ModbusServer.GetInputRegister leave");
			}
		}

		/// <summary>
		/// Sets an input register of a device.
		/// </summary>
		/// <param name="deviceId">The device id.</param>
		/// <param name="registerNumber">The input register address.</param>
		/// <param name="value">The register value.</param>
		public void SetInputRegister(byte deviceId, ushort registerNumber, ushort value)
		{
			try
			{
				logger?.LogTrace("ModbusServer.SetInputRegister(byte, ushort, ushort) enter");
				CheckDisposed();

				if (!modbusDevices.TryGetValue(deviceId, out ModbusDevice device))
					throw new ArgumentException($"Device #{deviceId} does not exist");

				device.SetInputRegister(registerNumber, value);
			}
			finally
			{
				logger?.LogTrace("ModbusServer.SetInputRegister(byte, ushort, ushort) leave");
			}
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
			try
			{
				logger?.LogTrace("ModbusServer.SetInputRegister(byte, ushort, byte, byte) enter");
				CheckDisposed();

				SetInputRegister(deviceId, new Register { Address = registerNumber, HiByte = highByte, LoByte = lowByte, Type = ModbusObjectType.InputRegister });
			}
			finally
			{
				logger?.LogTrace("ModbusServer.SetInputRegister(byte, ushort, byte, byte) leave");
			}
		}

		/// <summary>
		/// Sets an input register of a device.
		/// </summary>
		/// <param name="deviceId">The device id.</param>
		/// <param name="register">The input register.</param>
		public void SetInputRegister(byte deviceId, ModbusObject register)
		{
			try
			{
				logger?.LogTrace("ModbusServer.SetInputRegister(byte, ModbusObject) enter");
				CheckDisposed();

				if (register.Type != ModbusObjectType.InputRegister)
					throw new ArgumentException("Invalid register type set");

				SetInputRegister(deviceId, register.Address, register.RegisterValue);
			}
			finally
			{
				logger?.LogTrace("ModbusServer.SetInputRegister(byte, ModbusObject) leave");
			}
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
			try
			{
				logger?.LogTrace("ModbusServer.GetHoldingRegister enter");
				CheckDisposed();

				if (!modbusDevices.TryGetValue(deviceId, out ModbusDevice device))
					throw new ArgumentException($"Device #{deviceId} does not exist");

				return device.GetHoldingRegister(registerNumber);
			}
			finally
			{
				logger?.LogTrace("ModbusServer.GetHoldingRegister leave");
			}
		}

		/// <summary>
		/// Sets a holding register of a device.
		/// </summary>
		/// <param name="deviceId">The device id.</param>
		/// <param name="registerNumber">The holding register address.</param>
		/// <param name="value">The register value.</param>
		public void SetHoldingRegister(byte deviceId, ushort registerNumber, ushort value)
		{
			try
			{
				logger?.LogTrace("ModbusServer.SetHoldingRegister(byte, ushort, ushort) enter");
				CheckDisposed();

				if (!modbusDevices.TryGetValue(deviceId, out ModbusDevice device))
					throw new ArgumentException($"Device #{deviceId} does not exist");

				device.SetHoldingRegister(registerNumber, value);
			}
			finally
			{
				logger?.LogTrace("ModbusServer.SetHoldingRegister(byte, ushort, ushort) leave");
			}
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
			try
			{
				logger?.LogTrace("ModbusServer.SetHoldingRegister(byte, ushort, byte, byte) enter");
				CheckDisposed();

				SetHoldingRegister(deviceId, new Register { Address = registerNumber, HiByte = highByte, LoByte = lowByte, Type = ModbusObjectType.HoldingRegister });
			}
			finally
			{
				logger?.LogTrace("ModbusServer.SetHoldingRegister(byte, ushort, byte, byte) leave");
			}
		}

		/// <summary>
		/// Sets a holding register of a device.
		/// </summary>
		/// <param name="deviceId">The device id.</param>
		/// <param name="register">The register.</param>
		public void SetHoldingRegister(byte deviceId, ModbusObject register)
		{
			try
			{
				logger?.LogTrace("ModbusServer.SetHoldingRegister(byte, ModbusObject) enter");
				CheckDisposed();

				if (register.Type != ModbusObjectType.HoldingRegister)
					throw new ArgumentException("Invalid register type set");

				SetHoldingRegister(deviceId, register.Address, register.RegisterValue);
			}
			finally
			{
				logger?.LogTrace("ModbusServer.SetHoldingRegister(byte, ModbusObject) leave");
			}
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
			try
			{
				logger?.LogTrace("ModbusServer.AddDevice enter");
				CheckDisposed();

				return modbusDevices.TryAdd(deviceId, new ModbusDevice(deviceId));
			}
			finally
			{
				logger?.LogTrace("ModbusServer.AddDevice leave");
			}
		}

		/// <summary>
		/// Removes a device from the server.
		/// </summary>
		/// <param name="deviceId">The device id to remove.</param>
		/// <returns>true on success, otherwise false.</returns>
		public bool RemoveDevice(byte deviceId)
		{
			try
			{
				logger?.LogTrace("ModbusServer.RemoveDevice enter");
				CheckDisposed();

				return modbusDevices.TryRemove(deviceId, out ModbusDevice _);
			}
			finally
			{
				logger?.LogTrace("ModbusServer.RemoveDevice leave");
			}
		}

		#endregion Devices

		#endregion Public methods

		#region Private methods

		#region Server

		private void Initialize()
		{
			try
			{
				logger?.LogTrace("ModbusServer.Initialize enter");
				CheckDisposed();

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
						ReadTimeout = (int)timeout.TotalMilliseconds,
						WriteTimeout = (int)timeout.TotalMilliseconds
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
			finally
			{
				logger?.LogTrace("ModbusServer.Initialize leave");
			}
		}

		private void OnDataReceived(object sender, SerialDataReceivedEventArgs args)
		{
			try
			{
				logger?.LogTrace("ModbusServer.OnDataReceived enter");

				using var requestStream = new MemoryStream();
				do
				{
					byte[] buffer = new byte[BufferSize];
					int count = serialPort.Read(buffer, 0, buffer.Length);
					requestStream.Write(buffer, 0, count);
				}
				while (serialPort.BytesToRead > 0);

				try
				{
					var request = new Request(requestStream.GetBuffer());
					var response = requestHandler?.Invoke(request);
					if (response != null)
					{
						byte[] bytes = response.Serialize();
						serialPort.Write(bytes, 0, bytes.Length);
					}
				}
				catch (InvalidOperationException ex)
				{
					logger?.LogWarning(ex, $"Invalid data received: {ex.Message}");
				}
				catch (NotImplementedException ex)
				{
					logger?.LogWarning(ex, $"Invalid data received: {ex.Message}");
				}
			}
			catch (Exception ex)
			{
				logger?.LogError(ex, $"Unexpected error ({ex.GetType().Name}) on receive: {ex.GetMessage()}");
			}
			finally
			{
				logger?.LogTrace("ModbusServer.OnDataReceived leave");
			}
		}

		private Response HandleRequest(Request request)
		{
			// The device is not known => no response to send.
			if (!modbusDevices.ContainsKey(request.DeviceId))
				return null;

			return request.Function switch
			{
				FunctionCode.ReadCoils => HandleReadCoils(request),
				FunctionCode.ReadDiscreteInputs => HandleReadDiscreteInputs(request),
				FunctionCode.ReadHoldingRegisters => HandleReadHoldingRegisters(request),
				FunctionCode.ReadInputRegisters => HandleReadInputRegisters(request),
				FunctionCode.WriteSingleCoil => HandleWriteSingleCoil(request),
				FunctionCode.WriteSingleRegister => HandleWritSingleRegister(request),
				FunctionCode.WriteMultipleCoils => HandleWriteMultipleCoils(request),
				FunctionCode.WriteMultipleRegisters => HandleWriteMultipleRegisters(request),
				_ => new Response(request)
				{
					ErrorCode = ErrorCode.IllegalFunction
				},
			};
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
						int len = (int)Math.Ceiling(request.Count / 8.0);
						response.Data = new DataBuffer(len);
						for (int i = 0; i < request.Count; i++)
						{
							ushort addr = (ushort)(request.Address + i);
							if (GetCoil(request.DeviceId, addr).BoolValue)
							{
								int posByte = i / 8;
								int posBit = i % 8;

								byte mask = (byte)Math.Pow(2, posBit);
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
						int len = (int)Math.Ceiling(request.Count / 8.0);
						response.Data = new DataBuffer(len);
						for (int i = 0; i < request.Count; i++)
						{
							ushort addr = (ushort)(request.Address + i);
							if (GetDiscreteInput(request.DeviceId, addr).BoolValue)
							{
								int posByte = i / 8;
								int posBit = i % 8;

								byte mask = (byte)Math.Pow(2, posBit);
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
							ushort addr = (ushort)(request.Address + i);
							var reg = GetHoldingRegister(request.DeviceId, addr);
							response.Data.SetUInt16(i * 2, reg.RegisterValue);
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
							ushort addr = (ushort)(request.Address + i);
							var reg = GetInputRegister(request.DeviceId, addr);
							response.Data.SetUInt16(i * 2, reg.RegisterValue);
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
				ushort val = request.Data.GetUInt16(0);
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
						var coil = new Coil { Address = request.Address, BoolValue = (val > 0) };

						SetCoil(request.DeviceId, coil);
						response.Data = request.Data;

						InputWritten?.Invoke(this, new WriteEventArgs(request.DeviceId, coil));
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
				ushort val = request.Data.GetUInt16(0);

				if (request.Address < Consts.MinAddress || request.Address > Consts.MaxAddress)
				{
					response.ErrorCode = ErrorCode.IllegalDataAddress;
				}
				else
				{
					try
					{
						var register = new Register { Address = request.Address, RegisterValue = val, Type = ModbusObjectType.HoldingRegister };

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

				int numBytes = (int)Math.Ceiling(request.Count / 8.0);
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
							ushort addr = (ushort)(request.Address + i);

							int posByte = i / 8;
							int posBit = i % 8;

							byte mask = (byte)Math.Pow(2, posBit);
							int val = request.Data[posByte] & mask;

							var coil = new Coil { Address = addr, BoolValue = (val > 0) };
							SetCoil(request.DeviceId, coil);
							list.Add(coil);
						}
						InputWritten?.Invoke(this, new WriteEventArgs(request.DeviceId, list));
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
							ushort addr = (ushort)(request.Address + i);
							ushort val = request.Data.GetUInt16(i * 2);

							var register = new Register { Address = addr, RegisterValue = val, Type = ModbusObjectType.HoldingRegister };
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

		private bool isDisposed;

		/// <inheritdoc/>
		public void Dispose()
		{
			if (isDisposed)
				return;

			isDisposed = true;

			serialPort?.Close();
			serialPort?.Dispose();
			serialPort = null;
		}

		private void CheckDisposed()
		{
			if (isDisposed)
				throw new ObjectDisposedException(GetType().FullName);
		}

		#endregion IDisposable implementation
	}
}
