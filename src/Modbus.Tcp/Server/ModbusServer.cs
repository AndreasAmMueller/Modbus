using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using AMWD.Modbus.Common;
using AMWD.Modbus.Common.Interfaces;
using AMWD.Modbus.Common.Structures;
using AMWD.Modbus.Common.Util;
using AMWD.Modbus.Tcp.Protocol;
using AMWD.Modbus.Tcp.Util;
using Microsoft.Extensions.Logging;

namespace AMWD.Modbus.Tcp.Server
{
	/// <summary>
	/// A server to communicate via Modbus TCP.
	/// </summary>
	public class ModbusServer : IModbusServer
	{
		#region Fields

		private readonly ILogger logger;

		private TcpListener tcpListener;
		private List<TcpClient> tcpClients = new List<TcpClient>();

		private readonly FunctionCode[] availableFunctionCodes = Enum.GetValues(typeof(FunctionCode))
			.Cast<FunctionCode>()
			.ToArray();

		private ConcurrentDictionary<byte, ModbusDevice> modbusDevices = new ConcurrentDictionary<byte, ModbusDevice>();

		#endregion Fields

		#region Events

		/// <summary>
		/// Raised when a client has connected to the server.
		/// </summary>
		public event EventHandler<ClientEventArgs> ClientConnected;

		/// <summary>
		/// Raised when a client has disconnected from the server.
		/// </summary>
		public event EventHandler<ClientEventArgs> ClientDisconnected;

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
		/// <param name="port">The port to listen. (Default: 502)</param>
		/// <param name="logger"><see cref="ILogger"/> instance to write log entries.</param>
		public ModbusServer(int port = 502, ILogger logger = null)
		{
			this.logger = logger;
			Initialization = Task.Run(() => Initialize(port));
		}

		#endregion Constructors

		#region Properties

		/// <summary>
		/// Gets the result of the asynchronous initialization of this instance.
		/// </summary>
		public Task Initialization { get; }

		/// <summary>
		/// Gets the UTC timestamp of the server start.
		/// </summary>
		public DateTime StartTime { get; private set; }

		/// <summary>
		/// Gets a value indicating whether the server is running.
		/// </summary>
		public bool IsRunning { get; private set; }

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
				throw new ArgumentException($"Device #{deviceId} does not exist");

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
				throw new ArgumentException($"Device #{deviceId} does not exist");

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
				throw new ArgumentException($"Device #{deviceId} does not exist");

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
				throw new ArgumentException($"Device #{deviceId} does not exist");

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
				throw new ArgumentException($"Device #{deviceId} does not exist");

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
				throw new ArgumentException($"Device #{deviceId} does not exist");

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
				throw new ArgumentException($"Device #{deviceId} does not exist");

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
				throw new ArgumentException($"Device #{deviceId} does not exist");

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

		private void Initialize(int port)
		{
			if (isDisposed)
				throw new ObjectDisposedException(GetType().FullName);

			if (port < 1 || port > 65535)
				throw new ArgumentOutOfRangeException(nameof(port));

			tcpListener = new TcpListener(IPAddress.IPv6Any, port);
			tcpListener.Server.DualMode = true;
			tcpListener.Start();

			StartTime = DateTime.UtcNow;
			IsRunning = true;

			logger?.LogInformation($"Modbus server started to listen on {port}/tcp.");

			Task.Run((Action)WaitForClient);
		}

		private async void WaitForClient()
		{
			TcpClient client = null;
			try
			{
				client = await tcpListener.AcceptTcpClientAsync();
				Task.Run((Action)WaitForClient).Forget();
			}
			catch (NullReferenceException)
			{
				// Server stopping
				return;
			}
			catch (AggregateException)
			{
				// Server stopping
				return;
			}
			catch (ObjectDisposedException)
			{
				// Server stopping
				return;
			}

			lock (tcpClients)
			{
				tcpClients.Add(client);
			}

			HandleClient(client);
		}

		private static async Task<byte[]> ExpectBytesFromNetwork(NetworkStream stream, int size)
		{
			byte[] buffer = new byte[size];
			for (int offset = 0; offset < buffer.Length;)
			{
				int count = await stream.ReadAsync(buffer, offset, buffer.Length - offset); // CancellationToken?
				if (count < 1)
					throw new EndOfStreamException($"Expected to read {buffer.Length - offset} more bytes, but end of stream is reached");

				offset += count;
			}
			return buffer;
		}

		private async void HandleClient(TcpClient client)
		{
			var ipEp = (IPEndPoint)client.Client.RemoteEndPoint;
			ClientConnected?.Invoke(this, new ClientEventArgs(ipEp));
			logger?.LogInformation($"Client connected: {ipEp.Address}.");

			try
			{
				var stream = client.GetStream();
				while (true)
				{
					var requestBytes = new MemoryStream();

					byte[] header = await ExpectBytesFromNetwork(stream, 6);
					requestBytes.Write(header, 0, header.Length);
					int following = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(4, 2));

					byte[] payload = await ExpectBytesFromNetwork(stream, following);
					requestBytes.Write(payload, 0, payload.Length);

					Response response = null;
					try
					{
						var request = new Request(requestBytes.GetBuffer().AsSpan(0, (int)requestBytes.Length));
						response = HandleRequest(request);
					}
					catch (ArgumentException ae)
					{
						logger?.LogWarning(ae, $"Parsing request from {ipEp.Address} failed: {ae.Message}.");
					}
					catch (NotImplementedException nie)
					{
						logger?.LogWarning(nie, $"Request from {ipEp.Address} has an invalid function part.");
					}

					if (response != null)
					{
						try
						{
							byte[] bytes = response.Serialize();
							await stream.WriteAsync(bytes, 0, bytes.Length);
						}
						catch (NotImplementedException nie)
						{
							logger?.LogError(nie, $"Response for {ipEp.Address} has an invalid function part. Sending Message failed.");
						}
					}
				}
			}
			catch (EndOfStreamException)
			{
				// client closed connection (connecting)
			}
			catch (ArgumentOutOfRangeException)
			{
				// client closed connection (request parsing)
			}
			catch (IOException)
			{
				// server stopped
			}

			if (!isDisposed)
			{
				ClientDisconnected?.Invoke(this, new ClientEventArgs(ipEp));
				logger?.LogInformation($"Client disconnected: {ipEp.Address}.");
			}

			lock (tcpClients)
			{
				tcpClients.Remove(client);
			}
		}

		private Response HandleRequest(Request request)
		{
			// The device is not known => no response to send.
			if (!modbusDevices.ContainsKey(request.DeviceId))
				return null;

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
				case FunctionCode.EncapsulatedInterface:
					response = HandleEncapsulatedInterface(request);
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
						int len = (int)Math.Ceiling(request.Count / 8.0);
						response.Data = new DataBuffer(len);
						for (int i = 0; i < request.Count; i++)
						{
							ushort addr = (ushort)(request.Address + i);
							if (GetCoil(request.DeviceId, addr).Value)
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
							if (GetDiscreteInput(request.DeviceId, addr).Value)
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
							ushort addr = (ushort)(request.Address + i);
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

		private Response HandleEncapsulatedInterface(Request request)
		{
			var response = new Response(request);
			if (request.MEIType != MEIType.ReadDeviceInformation)
			{
				response.ErrorCode = ErrorCode.IllegalFunction;
				return response;
			}

			if ((byte)request.MEIObject < 0x00 ||
				(byte)request.MEIObject > 0xFF ||
				((byte)request.MEIObject > 0x06 && (byte)request.MEIObject < 0x80))
			{
				response.ErrorCode = ErrorCode.IllegalDataAddress;
				return response;
			}

			if (request.MEICategory < DeviceIDCategory.Basic || request.MEICategory > DeviceIDCategory.Individual)
			{
				response.ErrorCode = ErrorCode.IllegalDataValue;
				return response;
			}

			string version = Assembly.GetAssembly(typeof(ModbusServer))
				.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
				.InformationalVersion;

			response.MEIType = request.MEIType;
			response.MEICategory = request.MEICategory;

			var dict = new Dictionary<DeviceIDObject, string>();
			switch (request.MEICategory)
			{
				case DeviceIDCategory.Basic:
					response.ConformityLevel = 0x01;
					dict.Add(DeviceIDObject.VendorName, "AM.WD");
					dict.Add(DeviceIDObject.ProductCode, "AM.WD-MBS-TCP");
					dict.Add(DeviceIDObject.MajorMinorRevision, version);
					break;
				case DeviceIDCategory.Regular:
					response.ConformityLevel = 0x02;
					dict.Add(DeviceIDObject.VendorName, "AM.WD");
					dict.Add(DeviceIDObject.ProductCode, "AM.WD-MBS-TCP");
					dict.Add(DeviceIDObject.MajorMinorRevision, version);
					dict.Add(DeviceIDObject.VendorUrl, "https://github.com/AndreasAmMueller/Modbus");
					dict.Add(DeviceIDObject.ProductName, "AM.WD Modbus");
					dict.Add(DeviceIDObject.ModelName, "TCP Server");
					dict.Add(DeviceIDObject.UserApplicationName, "Modbus TCP Server");
					break;
				case DeviceIDCategory.Extended:
					response.ConformityLevel = 0x03;
					dict.Add(DeviceIDObject.VendorName, "AM.WD");
					dict.Add(DeviceIDObject.ProductCode, "AM.WD-MBS-TCP");
					dict.Add(DeviceIDObject.MajorMinorRevision, version);
					dict.Add(DeviceIDObject.VendorUrl, "https://github.com/AndreasAmMueller/Modbus");
					dict.Add(DeviceIDObject.ProductName, "AM.WD Modbus");
					dict.Add(DeviceIDObject.ModelName, "TCP Server");
					dict.Add(DeviceIDObject.UserApplicationName, "Modbus TCP Server");
					break;
				case DeviceIDCategory.Individual:
					switch (request.MEIObject)
					{
						case DeviceIDObject.VendorName:
							response.ConformityLevel = 0x81;
							dict.Add(DeviceIDObject.VendorName, "AM.WD");
							break;
						case DeviceIDObject.ProductCode:
							response.ConformityLevel = 0x81;
							dict.Add(DeviceIDObject.ProductCode, "AM.WD-MBS-TCP");
							break;
						case DeviceIDObject.MajorMinorRevision:
							response.ConformityLevel = 0x81;
							dict.Add(DeviceIDObject.MajorMinorRevision, version);
							break;
						case DeviceIDObject.VendorUrl:
							response.ConformityLevel = 0x82;
							dict.Add(DeviceIDObject.VendorUrl, "https://github.com/AndreasAmMueller/Modbus");
							break;
						case DeviceIDObject.ProductName:
							response.ConformityLevel = 0x82;
							dict.Add(DeviceIDObject.ProductName, "AM.WD Modbus");
							break;
						case DeviceIDObject.ModelName:
							response.ConformityLevel = 0x82;
							dict.Add(DeviceIDObject.ModelName, "TCP Server");
							break;
						case DeviceIDObject.UserApplicationName:
							response.ConformityLevel = 0x82;
							dict.Add(DeviceIDObject.UserApplicationName, "Modbus TCP Server");
							break;
						default:
							response.ConformityLevel = 0x83;
							dict.Add(request.MEIObject, "Custom Data for " + request.MEIObject);
							break;
					}
					break;
			}

			response.MoreRequestsNeeded = false;
			response.NextObjectId = 0x00;
			response.ObjectCount = (byte)dict.Count;
			response.Data = new DataBuffer();

			foreach (var kvp in dict)
			{
				byte[] bytes = Encoding.ASCII.GetBytes(kvp.Value);

				response.Data.AddByte((byte)kvp.Key);
				response.Data.AddByte((byte)bytes.Length);
				response.Data.AddBytes(bytes);
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
				ushort val = request.Data.GetUInt16(0);

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

				//request.Data contains [byte count] [data]..[data]
				if (request.Count < Consts.MinCount || request.Count > Consts.MaxRegisterCountWrite || request.Count * 2 != request.Data.Length - 1)
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
							ushort val = request.Data.GetUInt16(i * 2 + 1);

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
				tcpListener?.Stop();

				lock (tcpClients)
				{
					foreach (var client in tcpClients)
					{
						client?.GetStream()?.Dispose();
						client?.Dispose();
					}
					tcpClients.Clear();
				}

				tcpListener = null;
			}

			isDisposed = true;
		}

		#endregion IDisposable implementation
	}

	/// <summary>
	/// Provides connection information of a client.
	/// </summary>
	public class ClientEventArgs : EventArgs
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="ClientEventArgs"/> class.
		/// </summary>
		/// <param name="ep">The client end point.</param>
		public ClientEventArgs(IPEndPoint ep)
		{
			EndPoint = ep;
		}

		/// <summary>
		/// Gets the endpoint information of the client.
		/// </summary>
		public IPEndPoint EndPoint { get; private set; }
	}
}
