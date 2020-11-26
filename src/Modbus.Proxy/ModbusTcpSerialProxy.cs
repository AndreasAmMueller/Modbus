using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AMWD.Modbus.Common;
using AMWD.Modbus.Common.Structures;
using AMWD.Modbus.Common.Util;
using AMWD.Modbus.Serial;
using AMWD.Modbus.Tcp.Protocol;
using Microsoft.Extensions.Logging;
using ModbusClient = AMWD.Modbus.Serial.Client.ModbusClient;
using ModbusServer = AMWD.Modbus.Tcp.Server.ModbusServer;

namespace AMWD.Modbus.Proxy
{
	/// <summary>
	/// This proxy acceppts incoming TCP requests and forwards them to a TCP device.
	/// </summary>
	public class ModbusTcpSerialProxy : IDisposable
	{
		private readonly ILogger logger;
		private readonly ModbusTcpSerialSettings settings;
		private readonly ReaderWriterLockSlim syncLock = new ReaderWriterLockSlim();

		private bool isStarted;
		private ModbusClient client;
		private ModbusServer server;

		private readonly Dictionary<byte, ProxyDevice> devices = new Dictionary<byte, ProxyDevice>();

		/// <summary>
		/// Initializes a new instance of the <see cref="ModbusTcpSerialProxy"/> class.
		/// </summary>
		/// <param name="settings">The settings to configure this proxy.</param>
		/// <param name="logger">An <see cref="ILogger"/> implementation.</param>
		public ModbusTcpSerialProxy(ModbusTcpSerialSettings settings, ILogger logger = null)
		{
			this.logger = logger;
			this.settings = settings;
		}

		/// <summary>
		/// Starts the proxy.
		/// </summary>
		/// <returns></returns>
		public async Task StartAsync()
		{
			try
			{
				logger?.LogTrace("ModbusTcpSerialProxy.StartAsync enter");

				CheckDisposed();
				if (isStarted)
					return;

				isStarted = true;

				client = new ModbusClient(settings.PortName, logger)
				{
					BaudRate = settings.BaudRate,
					DataBits = settings.DataBits,
					Handshake = settings.Handshake,
					Parity = settings.Parity,
					StopBits = settings.StopBits
				};
				await client.Connect();

				server = new ModbusServer(settings.ListenPort, settings.ListenAddress, logger, HandleRequest);
				await server.Initialization;
			}
			finally
			{
				logger?.LogTrace("ModbusTcpSerialProxy.StartAsync leave");
			}
		}

		/// <summary>
		/// Stops the proxy.
		/// </summary>
		/// <returns></returns>
		public Task StopAsync()
		{
			try
			{
				logger?.LogTrace("ModbusTcpSerialProxy.StopAsync enter");
				CheckDisposed();
				if (!isStarted)
					return Task.CompletedTask;

				isStarted = false;

				server?.Dispose();
				server = null;

				client?.Dispose();
				client = null;

				return Task.CompletedTask;
			}
			finally
			{
				logger?.LogTrace("ModbusTcpSerialProxy.StopAsync leave");
			}
		}

		private Response HandleRequest(Request request, CancellationToken cancellationToken)
		{
			return request.Function switch
			{
				FunctionCode.ReadCoils => HandleReadCoils(request, cancellationToken),
				FunctionCode.ReadDiscreteInputs => HandleReadDiscreteInputs(request, cancellationToken),
				FunctionCode.ReadHoldingRegisters => HandleReadHoldingRegisters(request, cancellationToken),
				FunctionCode.ReadInputRegisters => HandleReadInputRegisters(request, cancellationToken),
				FunctionCode.WriteSingleCoil => HandleWriteSingleCoil(request, cancellationToken),
				FunctionCode.WriteSingleRegister => HandleWritSingleRegister(request, cancellationToken),
				FunctionCode.WriteMultipleCoils => HandleWriteMultipleCoils(request, cancellationToken),
				FunctionCode.WriteMultipleRegisters => HandleWriteMultipleRegisters(request, cancellationToken),
				FunctionCode.EncapsulatedInterface => HandleEncapsulatedInterface(request, cancellationToken),
				_ => new Response(request)
				{
					ErrorCode = ErrorCode.IllegalFunction
				},
			};
		}

		#region Read

		private Response HandleReadCoils(Request request, CancellationToken cancellationToken)
		{
			var requestTime = DateTime.UtcNow;
			var response = new Response(request);
			try
			{
				if (request.Count < Consts.MinCount || request.Count > Consts.MaxCoilCountRead)
				{
					response.ErrorCode = ErrorCode.IllegalDataValue;
					logger?.LogDebug("Request count is out of range.");
				}
				else if (request.Address < Consts.MinAddress || request.Address + request.Count > Consts.MaxAddress)
				{
					response.ErrorCode = ErrorCode.IllegalDataAddress;
					logger?.LogDebug("Request address is out of range.");
				}
				else
				{
					var coils = new List<Coil>();
					using (syncLock.GetReadLock())
					{
						if (devices.TryGetValue(request.DeviceId, out var device))
						{
							for (ushort i = request.Address; i < request.Address + request.Count; i++)
							{
								var (timestamp, value) = device.GetCoil(i);
								if (timestamp + settings.MinimumRequestWaitTimeOnDestinantion >= requestTime)
								{
									coils.Add(new Coil
									{
										Address = i,
										BoolValue = value
									});
								}
							}
						}
					}

					if (coils.Count < request.Count)
					{
						using (syncLock.GetWriteLock())
						{
							if (devices.TryGetValue(request.DeviceId, out var device))
							{
								for (ushort i = request.Address; i < request.Address + request.Count; i++)
								{
									var (timestamp, value) = device.GetCoil(i);
									if (timestamp + settings.MinimumRequestWaitTimeOnDestinantion >= requestTime)
									{
										coils.Add(new Coil
										{
											Address = i,
											BoolValue = value
										});
									}
								}
							}
							else
							{
								devices.Add(request.DeviceId, new ProxyDevice());
							}

							if (coils.Count < request.Count)
							{
								try
								{
									var task = client.ReadCoils(request.DeviceId, request.Address, request.Count, cancellationToken);
									coils = task.GetAwaiter().GetResult();

									foreach (var coil in coils)
									{
										devices[request.DeviceId].SetCoil(coil.Address, coil.BoolValue);
									}
								}
								catch (Exception ex)
								{
									logger?.LogError(ex, $"Requesting coils {request.DeviceId}#{request.Address}({request.Count}) failed");
									response.ErrorCode = ErrorCode.SlaveDeviceFailure;
									return response;
								}
							}
						}
					}

					int len = (int)Math.Ceiling(coils.Count / 8.0);
					response.Data = new DataBuffer(len);
					for (int i = 0; i < coils.Count; i++)
					{
						ushort addr = (ushort)(request.Address + i);
						if (coils[i].BoolValue)
						{
							int posByte = i / 8;
							int posBit = i % 8;

							byte mask = (byte)Math.Pow(2, posBit);
							response.Data[posByte] = (byte)(response.Data[posByte] | mask);
						}
					}
				}
			}
			catch
			{
				return null;
			}

			return response;
		}

		private Response HandleReadDiscreteInputs(Request request, CancellationToken cancellationToken)
		{
			var requestTime = DateTime.UtcNow;
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
					var discreteInputs = new List<DiscreteInput>();
					using (syncLock.GetReadLock())
					{
						if (devices.TryGetValue(request.DeviceId, out var device))
						{
							for (ushort i = request.Address; i < request.Address + request.Count; i++)
							{
								var (timestamp, value) = device.GetDiscreteInput(i);
								if (timestamp + settings.MinimumRequestWaitTimeOnDestinantion >= requestTime)
								{
									discreteInputs.Add(new DiscreteInput
									{
										Address = i,
										BoolValue = value
									});
								}
							}
						}
					}

					if (discreteInputs.Count < request.Count)
					{
						using (syncLock.GetWriteLock())
						{
							if (devices.TryGetValue(request.DeviceId, out var device))
							{
								for (ushort i = request.Address; i < request.Address + request.Count; i++)
								{
									var (timestamp, value) = device.GetDiscreteInput(i);
									if (timestamp + settings.MinimumRequestWaitTimeOnDestinantion >= requestTime)
									{
										discreteInputs.Add(new DiscreteInput
										{
											Address = i,
											BoolValue = value
										});
									}
								}
							}
							else
							{
								devices.Add(request.DeviceId, new ProxyDevice());
							}

							if (discreteInputs.Count < request.Count)
							{
								try
								{
									var task = client.ReadDiscreteInputs(request.DeviceId, request.Address, request.Count, cancellationToken);
									discreteInputs = task.GetAwaiter().GetResult();

									foreach (var discreteInput in discreteInputs)
									{
										devices[request.DeviceId].SetDiscreteInput(discreteInput.Address, discreteInput.BoolValue);
									}
								}
								catch (Exception ex)
								{
									logger?.LogError(ex, $"Requesting discrete inputs {request.DeviceId}#{request.Address}({request.Count}) failed");
									response.ErrorCode = ErrorCode.SlaveDeviceFailure;
									return response;
								}
							}
						}
					}

					int len = (int)Math.Ceiling(discreteInputs.Count / 8.0);
					response.Data = new DataBuffer(len);
					for (int i = 0; i < discreteInputs.Count; i++)
					{
						ushort addr = (ushort)(request.Address + i);
						if (discreteInputs[i].BoolValue)
						{
							int posByte = i / 8;
							int posBit = i % 8;

							byte mask = (byte)Math.Pow(2, posBit);
							response.Data[posByte] = (byte)(response.Data[posByte] | mask);
						}
					}
				}
			}
			catch
			{
				return null;
			}

			return response;
		}

		private Response HandleReadHoldingRegisters(Request request, CancellationToken cancellationToken)
		{
			var requestTime = DateTime.UtcNow;
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
					var registers = new List<Register>();
					using (syncLock.GetReadLock())
					{
						if (devices.TryGetValue(request.DeviceId, out var device))
						{
							for (ushort i = request.Address; i < request.Address + request.Count; i++)
							{
								var (timestamp, value) = device.GetHoldingRegister(i);
								if (timestamp + settings.MinimumRequestWaitTimeOnDestinantion >= requestTime)
								{
									registers.Add(new Register
									{
										Type = ModbusObjectType.HoldingRegister,
										Address = i,
										RegisterValue = value
									});
								}
							}
						}
					}

					if (registers.Count < request.Count)
					{
						using (syncLock.GetWriteLock())
						{
							if (devices.TryGetValue(request.DeviceId, out var device))
							{
								for (ushort i = request.Address; i < request.Address + request.Count; i++)
								{
									var (timestamp, value) = device.GetHoldingRegister(i);
									if (timestamp + settings.MinimumRequestWaitTimeOnDestinantion >= requestTime)
									{
										registers.Add(new Register
										{
											Type = ModbusObjectType.HoldingRegister,
											Address = i,
											RegisterValue = value
										});
									}
								}
							}
							else
							{
								devices.Add(request.DeviceId, new ProxyDevice());
							}

							if (registers.Count < request.Count)
							{
								try
								{
									var task = client.ReadHoldingRegisters(request.DeviceId, request.Address, request.Count, cancellationToken);
									registers = task.GetAwaiter().GetResult();

									foreach (var register in registers)
									{
										devices[request.DeviceId].SetHoldingRegister(register.Address, register.RegisterValue);
									}
								}
								catch (Exception ex)
								{
									logger?.LogError(ex, $"Requesting holding registers {request.DeviceId}#{request.Address}({request.Count}) failed");
									response.ErrorCode = ErrorCode.SlaveDeviceFailure;
									return response;
								}
							}
						}
					}

					response.Data = new DataBuffer(registers.Count * 2);
					for (int i = 0; i < registers.Count; i++)
					{
						response.Data.SetUInt16(i * 2, registers[i].RegisterValue);
					}
				}
			}
			catch
			{
				return null;
			}

			return response;
		}

		private Response HandleReadInputRegisters(Request request, CancellationToken cancellationToken)
		{
			var requestTime = DateTime.UtcNow;
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
					var registers = new List<Register>();
					using (syncLock.GetReadLock())
					{
						if (devices.TryGetValue(request.DeviceId, out var device))
						{
							for (ushort i = request.Address; i < request.Address + request.Count; i++)
							{
								var (timestamp, value) = device.GetInputRegister(i);
								if (timestamp + settings.MinimumRequestWaitTimeOnDestinantion >= requestTime)
								{
									registers.Add(new Register
									{
										Type = ModbusObjectType.InputRegister,
										Address = i,
										RegisterValue = value
									});
								}
							}
						}
					}

					if (registers.Count < request.Count)
					{
						using (syncLock.GetWriteLock())
						{
							if (devices.TryGetValue(request.DeviceId, out var device))
							{
								for (ushort i = request.Address; i < request.Address + request.Count; i++)
								{
									var (timestamp, value) = device.GetInputRegister(i);
									if (timestamp + settings.MinimumRequestWaitTimeOnDestinantion >= requestTime)
									{
										registers.Add(new Register
										{
											Type = ModbusObjectType.InputRegister,
											Address = i,
											RegisterValue = value
										});
									}
								}
							}
							else
							{
								devices.Add(request.DeviceId, new ProxyDevice());
							}

							if (registers.Count < request.Count)
							{
								try
								{
									var task = client.ReadInputRegisters(request.DeviceId, request.Address, request.Count, cancellationToken);
									registers = task.GetAwaiter().GetResult();

									foreach (var register in registers)
									{
										devices[request.DeviceId].SetInputRegister(register.Address, register.RegisterValue);
									}
								}
								catch (Exception ex)
								{
									logger?.LogError(ex, $"Requesting input registers {request.DeviceId}#{request.Address}({request.Count}) failed");
									response.ErrorCode = ErrorCode.SlaveDeviceFailure;
									return response;
								}
							}
						}
					}

					response.Data = new DataBuffer(registers.Count * 2);
					for (int i = 0; i < registers.Count; i++)
					{
						response.Data.SetUInt16(i * 2, registers[i].RegisterValue);
					}
				}
			}
			catch
			{
				return null;
			}

			return response;
		}

		private Response HandleEncapsulatedInterface(Request request, CancellationToken cancellationToken)
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

			response.MEIType = request.MEIType;
			response.MEICategory = request.MEICategory;

			try
			{
				var task = client.ReadDeviceInformation(request.DeviceId, request.MEICategory, request.MEIObject, cancellationToken);
				var dict = task.GetAwaiter().GetResult();

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
			}
			catch (Exception ex)
			{
				logger?.LogError(ex, $"Requesting input registers {request.DeviceId}#{request.Address}({request.Count}) failed");
				response.ErrorCode = ErrorCode.SlaveDeviceFailure;
			}

			return response;
		}

		#endregion Read

		#region Write

		private Response HandleWriteSingleCoil(Request request, CancellationToken cancellationToken)
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
						var task = client.WriteSingleCoil(request.DeviceId, coil, cancellationToken);
						bool success = task.GetAwaiter().GetResult();
						if (success)
						{
							using (syncLock.GetWriteLock())
							{
								if (!devices.TryGetValue(request.DeviceId, out var device))
								{
									device = new ProxyDevice();
									devices.Add(request.DeviceId, device);
								}

								device.SetCoil(coil.Address, coil.BoolValue);
							}
							response.Data = request.Data;
						}
						else
						{
							response.ErrorCode = ErrorCode.SlaveDeviceFailure;
						}
					}
					catch (Exception ex)
					{
						logger?.LogError(ex, $"Writing a single coil {request.DeviceId}#{request.Address} failed");
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

		private Response HandleWritSingleRegister(Request request, CancellationToken cancellationToken)
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
						var register = new ModbusObject { Address = request.Address, RegisterValue = val };
						var task = client.WriteSingleRegister(request.DeviceId, register, cancellationToken);
						bool success = task.GetAwaiter().GetResult();
						if (success)
						{
							using (syncLock.GetWriteLock())
							{
								if (!devices.TryGetValue(request.DeviceId, out var device))
								{
									device = new ProxyDevice();
									devices.Add(request.DeviceId, device);
								}

								device.SetInputRegister(register.Address, register.RegisterValue);
							}
							response.Data = request.Data;
						}
						else
						{
							response.ErrorCode = ErrorCode.SlaveDeviceFailure;
						}
					}
					catch (Exception ex)
					{
						logger?.LogError(ex, $"Writing a single register {request.DeviceId}#{request.Address} failed");
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

		private Response HandleWriteMultipleCoils(Request request, CancellationToken cancellationToken)
		{
			var response = new Response(request);
			try
			{
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
					var list = new List<Coil>();
					for (int i = 0; i < request.Count; i++)
					{
						ushort addr = (ushort)(request.Address + i);

						int posByte = i / 8;
						int posBit = i % 8;

						byte mask = (byte)Math.Pow(2, posBit);
						int val = request.Data[posByte] & mask;

						list.Add(new Coil { Address = addr, BoolValue = (val > 0) });
					}

					try
					{
						var task = client.WriteCoils(request.DeviceId, list, cancellationToken);
						bool success = task.GetAwaiter().GetResult();
						if (success)
						{
							using (syncLock.GetWriteLock())
							{
								if (!devices.TryGetValue(request.DeviceId, out var device))
								{
									device = new ProxyDevice();
									devices.Add(request.DeviceId, device);
								}
								foreach (var coil in list)
								{
									device.SetCoil(coil.Address, coil.BoolValue);
								}
							}
						}
						else
						{
							response.ErrorCode = ErrorCode.SlaveDeviceFailure;
						}
					}
					catch (Exception ex)
					{
						logger?.LogError(ex, $"Writing a multipe coils {request.DeviceId}#{request.Address}({request.Count}) failed");
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

		private Response HandleWriteMultipleRegisters(Request request, CancellationToken cancellationToken)
		{
			var response = new Response(request);
			try
			{
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
					var list = new List<Register>();
					for (int i = 0; i < request.Count; i++)
					{
						ushort addr = (ushort)(request.Address + i);
						ushort val = request.Data.GetUInt16(i * 2 + 1);
						list.Add(new Register { Address = addr, RegisterValue = val, Type = ModbusObjectType.HoldingRegister });
					}

					try
					{
						var task = client.WriteRegisters(request.DeviceId, list, cancellationToken);
						bool success = task.GetAwaiter().GetResult();
						if (success)
						{
							using (syncLock.GetWriteLock())
							{
								if (!devices.TryGetValue(request.DeviceId, out var device))
								{
									device = new ProxyDevice();
									devices.Add(request.DeviceId, device);
								}
								foreach (var register in list)
								{
									device.SetHoldingRegister(register.Address, register.RegisterValue);
								}
							}
						}
						else
						{
							response.ErrorCode = ErrorCode.SlaveDeviceFailure;
						}
					}
					catch (Exception ex)
					{
						logger?.LogError(ex, $"Writing a multipe registers {request.DeviceId}#{request.Address}({request.Count}) failed");
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

		#endregion Write

		#region IDisposable implementation

		private bool isDisposed;

		/// <summary>
		/// Releases all managed and unmanaged resources used.
		/// </summary>
		public void Dispose()
		{
			if (isDisposed)
				return;

			isDisposed = true;

			server?.Dispose();
			server = null;

			client?.Dispose();
			client = null;
		}

		private void CheckDisposed()
		{
			if (isDisposed)
				throw new ObjectDisposedException(GetType().FullName);
		}

		#endregion IDisposable implementation
	}

	/// <summary>
	/// Represents the settings for the TCP to TCP proxy.
	/// </summary>
	public class ModbusTcpSerialSettings
	{
		private TimeSpan minimumRequestTime = TimeSpan.FromMilliseconds(200);

		/// <summary>
		/// Gets or sets the address to bind the server to.
		/// </summary>
		public IPAddress ListenAddress { get; set; }

		/// <summary>
		/// Gets or sets the port to listen to.
		/// </summary>
		public int ListenPort { get; set; }

		/// <summary>
		/// Gets or sets the serial port name.
		/// </summary>
		public string PortName { get; set; }

		/// <summary>
		/// Gets or sets the baud rate. Default: 38400.
		/// </summary>
		public BaudRate BaudRate { get; set; } = BaudRate.Baud38400;

		/// <summary>
		/// Gets or sets the number of data bits. Default: 8.
		/// </summary>
		public int DataBits { get; set; } = 8;

		/// <summary>
		/// Gets or sets the parity. Default: None.
		/// </summary>
		public Parity Parity { get; set; } = Parity.None;

		/// <summary>
		/// Gets or sets the number of stop bits. Default: One.
		/// </summary>
		public StopBits StopBits { get; set; } = StopBits.One;

		/// <summary>
		/// Gets or sets the handshake. Default: None.
		/// </summary>
		public Handshake Handshake { get; set; } = Handshake.None;

		/// <summary>
		/// Gets or sets the minimum age of a read value before the proxy requests it again.
		/// </summary>
		/// <remarks>
		/// A value below 200ms will always be set to 200ms.
		/// </remarks>
		public TimeSpan MinimumRequestWaitTimeOnDestinantion
		{
			get => minimumRequestTime;
			set
			{
				minimumRequestTime = value < TimeSpan.FromMilliseconds(200)
					? TimeSpan.FromMilliseconds(200)
					: value;
			}
		}
	}
}
