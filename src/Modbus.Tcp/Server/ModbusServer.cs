using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
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

namespace AMWD.Modbus.Tcp.Server
{
	/// <summary>
	/// A handler to process the modbus requests.
	/// </summary>
	/// <param name="request">The request to process.</param>
	/// <param name="cancellationToken">The cancellation token fired on <see cref="ModbusServer.Dispose"/>.</param>
	/// <returns>The response.</returns>
	public delegate Response ModbusTcpRequestHandler(Request request, CancellationToken cancellationToken);

	/// <summary>
	/// A server to communicate via Modbus TCP.
	/// </summary>
	public class ModbusServer : IModbusServer
	{
		#region Fields

		private readonly ILogger logger;

		private readonly CancellationTokenSource stopCts = new CancellationTokenSource();
		private TcpListener tcpListener;
		private readonly ConcurrentDictionary<byte, ModbusDevice> modbusDevices = new ConcurrentDictionary<byte, ModbusDevice>();

		private Task clientConnect;
		private readonly ConcurrentDictionary<TcpClient, bool> tcpClients = new ConcurrentDictionary<TcpClient, bool>();
		private readonly List<Task> clientTasks = new List<Task>();
		private readonly ModbusTcpRequestHandler requestHandler;

		#endregion Fields

		#region Constructors

		/// <summary>
		/// Initializes a new instance of the <see cref="ModbusServer"/> class.
		/// </summary>
		/// <param name="port">The port to listen. (Default: 502)</param>
		/// <param name="listenAddress">The ip address to bind on. (Default: <see cref="IPAddress.IPv6Any"/>)</param>
		/// <param name="logger"><see cref="ILogger"/> instance to write log entries. (Default: no logger)</param>
		/// <param name="requestHandler">Set this request handler to override the default implemented handling. (Default: serving the data provided by Set* methods)</param>
		public ModbusServer(int port = 502, IPAddress listenAddress = null, ILogger logger = null, ModbusTcpRequestHandler requestHandler = null)
		{
			ListenAddress = listenAddress;
			if (ListenAddress == null)
				ListenAddress = IPAddress.IPv6Any;

			if (port < 0 || port > 65535)
				throw new ArgumentOutOfRangeException(nameof(port));

			try
			{
				var listener = new TcpListener(ListenAddress, port);
				listener.Start();
				Port = ((IPEndPoint)listener.LocalEndpoint).Port;
				listener.Stop();
			}
			catch (Exception ex)
			{
				throw new ArgumentException(nameof(port), ex);
			}

			this.logger = logger;
			this.requestHandler = requestHandler ?? HandleRequest;

			Initialization = Task.Run(() => Initialize());
		}

		#endregion Constructors

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
		public event EventHandler<WriteEventArgs> InputWritten;

		/// <summary>
		/// Raised when a register was written.
		/// </summary>
		public event EventHandler<WriteEventArgs> RegisterWritten;

		#endregion Events

		#region Properties

		/// <summary>
		/// Gets the result of the asynchronous initialization of this instance.
		/// </summary>
		public Task Initialization { get; }

		/// <summary>
		/// Gets the UTC timestamp of the server start.
		/// </summary>
		public DateTime StartTime { get; private set; } = DateTime.MinValue;

		/// <summary>
		/// Gets a value indicating whether the server is running.
		/// </summary>
		public bool IsRunning { get; private set; }

		/// <summary>
		/// Gets the binding address.
		/// </summary>
		public IPAddress ListenAddress { get; }

		/// <summary>
		/// Gets the port listening on.
		/// </summary>
		public int Port { get; }

		/// <summary>
		/// Gets or sets read/write timeout. (Default: 1 second)
		/// </summary>
		public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(1);

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

		private Task Initialize()
		{
			try
			{
				logger?.LogTrace("ModbusServer.Initialize enter");
				CheckDisposed();

				tcpListener?.Stop();
				tcpListener = null;
				tcpListener = new TcpListener(ListenAddress, Port);

				if (ListenAddress.AddressFamily == AddressFamily.InterNetworkV6)
					tcpListener.Server.DualMode = true;

				tcpListener.Start();
				StartTime = DateTime.UtcNow;
				IsRunning = true;

				clientConnect = Task.Run(async () => await WaitForClient());
				logger?.LogInformation($"Modbus server started. Listening on {ListenAddress}:{Port}/tcp.");

				return Task.CompletedTask;
			}
			finally
			{
				logger?.LogTrace("ModbusServer.Initialize leave");
			}
		}

		private async Task WaitForClient()
		{
			try
			{
				logger?.LogTrace("ModbusServer.WaitForClient enter");
				while (!stopCts.IsCancellationRequested)
				{
					try
					{
						var client = await tcpListener.AcceptTcpClientAsync();
						if (tcpClients.TryAdd(client, true))
						{
							var clientTask = Task.Run(async () => await HandleClient(client));
							clientTasks.Add(clientTask);
						}
					}
					catch
					{
						// keep things quiet
					}
				}
			}
			finally
			{
				logger?.LogTrace("ModbusServer.WaitForClient leave");
			}
		}

		private async Task HandleClient(TcpClient client)
		{
			logger?.LogTrace("ModbusServer.HandleClient enter");
			var endpoint = (IPEndPoint)client.Client.RemoteEndPoint;
			try
			{
				ClientConnected?.Invoke(this, new ClientEventArgs(endpoint));
				logger?.LogInformation($"Client connected: {endpoint.Address}.");

				var stream = client.GetStream();
				while (!stopCts.IsCancellationRequested)
				{
					using var requestStream = new MemoryStream();

					using (var cts = new CancellationTokenSource(Timeout))
					using (stopCts.Token.Register(() => cts.Cancel()))
					{
						try
						{
							byte[] header = await stream.ReadExpectedBytes(6, cts.Token);
							await requestStream.WriteAsync(header, 0, header.Length, cts.Token);

							byte[] bytes = header.Skip(4).Take(2).ToArray();
							if (BitConverter.IsLittleEndian)
								Array.Reverse(bytes);

							int following = BitConverter.ToUInt16(bytes, 0);
							byte[] payload = await stream.ReadExpectedBytes(following, cts.Token);
							await requestStream.WriteAsync(payload, 0, payload.Length, cts.Token);
						}
						catch (OperationCanceledException) when (cts.IsCancellationRequested)
						{
							continue;
						}
					}

					try
					{
						var request = new Request(requestStream.GetBuffer());
						var response = requestHandler?.Invoke(request, stopCts.Token);
						if (response != null)
						{
							using var cts = new CancellationTokenSource(Timeout);
							using var reg = stopCts.Token.Register(() => cts.Cancel());
							try
							{
								byte[] bytes = response.Serialize();
								await stream.WriteAsync(bytes, 0, bytes.Length, cts.Token);
							}
							catch (OperationCanceledException) when (cts.IsCancellationRequested)
							{
								continue;
							}
						}
					}
					catch (ArgumentException ex)
					{
						logger?.LogWarning(ex, $"Invalid data received from {endpoint.Address}: {ex.Message}");
					}
					catch (NotImplementedException ex)
					{
						logger?.LogWarning(ex, $"Invalid data received from {endpoint.Address}: {ex.Message}");
					}
				}
			}
			catch (EndOfStreamException)
			{
				// client connection closed
				return;
			}
			catch (Exception ex)
			{
				logger?.LogError(ex, $"Unexpected error ({ex.GetType().Name}) occurred: {ex.GetMessage()}");
			}
			finally
			{
				ClientDisconnected?.Invoke(this, new ClientEventArgs(endpoint));
				logger?.LogInformation($"Client disconnected: {endpoint.Address}");

				client.Dispose();
				tcpClients.TryRemove(client, out _);

				logger?.LogTrace("ModbusServer.HandleClient leave");
			}
		}

		private Response HandleRequest(Request request, CancellationToken cancellationToken)
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
				FunctionCode.EncapsulatedInterface => HandleEncapsulatedInterface(request),
				_ => new Response(request)
				{
					ErrorCode = ErrorCode.IllegalFunction
				}
			};
		}

		#endregion Server

		#region Function implementation

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

		#endregion Function implementation

		#endregion Private methods

		#region IDisposable implementation

		private bool isDisposed;

		/// <inheritdoc/>
		public void Dispose()
		{
			if (isDisposed)
				return;

			isDisposed = true;
			stopCts.Cancel();

			tcpListener.Stop();
			foreach (var client in tcpClients.Keys)
			{
				client.Dispose();
			}

			Task.WaitAll(clientTasks.ToArray());
			Task.WaitAll(clientConnect);

			IsRunning = false;
		}

		/// <summary>
		/// Checks whether the object is already disposed.
		/// </summary>
		protected void CheckDisposed()
		{
			if (isDisposed)
				throw new ObjectDisposedException(GetType().FullName);
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
		/// <param name="endpoint">The client end point.</param>
		public ClientEventArgs(IPEndPoint endpoint)
		{
			EndPoint = endpoint;
		}

		/// <summary>
		/// Gets the endpoint information of the client.
		/// </summary>
		public IPEndPoint EndPoint { get; }
	}
}
