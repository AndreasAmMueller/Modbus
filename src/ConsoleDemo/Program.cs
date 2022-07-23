using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AMWD.Modbus.Common;
using AMWD.Modbus.Common.Interfaces;
using AMWD.Modbus.Common.Structures;
using AMWD.Modbus.Common.Util;
using AMWD.Modbus.Serial;
using ConsoleDemo.Logger;
using Microsoft.Extensions.Logging;
using SerialClient = AMWD.Modbus.Serial.Client.ModbusClient;
using SerialServer = AMWD.Modbus.Serial.Server.ModbusServer;
using TcpClient = AMWD.Modbus.Tcp.Client.ModbusClient;
using TcpServer = AMWD.Modbus.Tcp.Server.ModbusServer;
using SerialOverTcpClient = AMWD.Modbus.SerialOverTCP.Client.ModbusClient;
namespace ConsoleDemo
{
	internal class Program
	{
		private static readonly string[] yesList = new[] { "y", "j", "yes", "ja" };

		private static async Task<int> Main(string[] _)
		{
			var cts = new CancellationTokenSource();
			var logger = new ConsoleLogger
			{
				//MinLevel = LogLevel.Trace,
				MinLevel = LogLevel.Information,
				TimestampFormat = "HH:mm:ss.fff"
			};
			Console.CancelKeyPress += (s, a) =>
			{
				cts.Cancel();
				a.Cancel = true;
			};

			Console.WriteLine("Modbus Console Demo");
			Console.WriteLine();

			try
			{
				Console.Write("What to start? [1] Client, [2] Server: ");
				int type = Convert.ToInt32(Console.ReadLine().Trim());

				switch (type)
				{
					case 1:
						return await RunClientAsync(logger, cts.Token);
					case 2:
						return await RunServerAsync(logger, cts.Token);
					default:
						Console.Error.WriteLine($"Unknown option: {type}");
						return 1;
				}
			}
			catch (Exception ex)
			{
				logger.LogError(ex, $"App terminated unexpected: {ex.InnerException?.Message ?? ex.Message}");
				return 1;
			}
		}

		private static async Task<int> RunClientAsync(ILogger logger, CancellationToken cancellationToken)
		{
			Console.Write("Connection Type [1] TCP, [2] RS485 [3] DTUoverTCP: ");
			int cType = Convert.ToInt32(Console.ReadLine().Trim());

			IModbusClient client = null;
			try
			{
				switch (cType)
				{
					case 3:
					case 1:
						{
							Console.Write("Hostname: ");
							string host = Console.ReadLine().Trim();
							Console.Write("Port: ");
							int port = Convert.ToInt32(Console.ReadLine().Trim());
							if (cType == 3)
							{
								client = new SerialOverTcpClient(host, port, logger);
							}
							else
							{
								client = new TcpClient(host, port, logger);
							}
						}
						break;
					case 2:
						{
							Console.Write("Interface: ");
							string port = Console.ReadLine().Trim();

							Console.Write("Baud: ");
							int baud = Convert.ToInt32(Console.ReadLine().Trim());

							Console.Write("Stop-Bits [0|1|2|3=1.5]: ");
							int stopBits = Convert.ToInt32(Console.ReadLine().Trim());

							Console.Write("Parity [0] None [1] Odd [2] Even [3] Mark [4] Space: ");
							int parity = Convert.ToInt32(Console.ReadLine().Trim());

							Console.Write("Handshake [0] None [1] X-On/Off [2] RTS [3] RTS+X-On/Off: ");
							int handshake = Convert.ToInt32(Console.ReadLine().Trim());

							Console.Write("Timeout (ms): ");
							int timeout = Convert.ToInt32(Console.ReadLine().Trim());

							Console.Write("Set Driver to RS485 [0] No [1] Yes: ");
							int setDriver = Convert.ToInt32(Console.ReadLine().Trim());

							client = new SerialClient(port)
							{
								BaudRate = (BaudRate)baud,
								DataBits = 8,
								StopBits = (StopBits)stopBits,
								Parity = (Parity)parity,
								Handshake = (Handshake)handshake,
								SendTimeout = TimeSpan.FromMilliseconds(timeout),
								ReceiveTimeout = TimeSpan.FromMilliseconds(timeout)
							};

							if (setDriver == 1)
							{
								((SerialClient)client).DriverEnableRS485 = true;
							}
						}
						break;
					default:
						Console.Error.WriteLine($"Unknown type: {cType}");
						return 1;
				}

				await Task.WhenAny(client.Connect(), Task.Delay(Timeout.Infinite, cancellationToken));
				if (cancellationToken.IsCancellationRequested)
					return 0;

				while (!cancellationToken.IsCancellationRequested)
				{
					Console.Write("Device ID: ");
					byte id = Convert.ToByte(Console.ReadLine().Trim());

					Console.Write("Function [1] Read Register, [2] Device Info, [9] Write Register : ");
					int fn = Convert.ToInt32(Console.ReadLine().Trim());

					switch (fn)
					{
						case 1:
							{
								ushort address = 0;
								ushort count = 0;
								string type = "";

								Console.WriteLine();
								Console.Write("Address : ");
								address = Convert.ToUInt16(Console.ReadLine().Trim());
								Console.Write("DataType: ");
								type = Console.ReadLine().Trim();
								if (type == "string")
								{
									Console.Write("Register Count: ");
									count = Convert.ToUInt16(Console.ReadLine().Trim());
								}

								Console.WriteLine();
								Console.Write("Run as loop? [y/N]: ");
								string loop = Console.ReadLine().Trim().ToLower();
								int interval = 0;
								if (yesList.Contains(loop))
								{
									Console.Write("Loop interval (milliseconds): ");
									interval = Convert.ToInt32(Console.ReadLine().Trim());
								}

								Console.WriteLine();
								do
								{
									try
									{
										Console.Write("Result  : ");
										List<Register> result = null;
										switch (type.Trim().ToLower())
										{
											case "byte":
												result = await client.ReadHoldingRegisters(id, address, 1);
												Console.WriteLine(result?.First().GetByte());
												break;
											case "ushort":
												result = await client.ReadHoldingRegisters(id, address, 1);
												Console.WriteLine(result?.First().GetUInt16());
												break;
											case "uint":
												result = await client.ReadHoldingRegisters(id, address, 2);
												Console.WriteLine(result?.GetUInt32());
												break;
											case "ulong":
												result = await client.ReadHoldingRegisters(id, address, 4);
												Console.WriteLine(result?.GetUInt64());
												break;
											case "sbyte":
												result = await client.ReadHoldingRegisters(id, address, 1);
												Console.WriteLine(result?.First().GetSByte());
												break;
											case "short":
												result = await client.ReadHoldingRegisters(id, address, 1);
												Console.WriteLine(result?.First().GetInt16());
												break;
											case "int":
												result = await client.ReadHoldingRegisters(id, address, 2);
												Console.WriteLine(result?.GetInt32());
												break;
											case "long":
												result = await client.ReadHoldingRegisters(id, address, 4);
												Console.WriteLine(result?.GetInt64());
												break;
											case "float":
												result = await client.ReadHoldingRegisters(id, address, 2);
												Console.WriteLine(result?.GetSingle());
												break;
											case "double":
												result = await client.ReadHoldingRegisters(id, address, 4);
												Console.WriteLine(result?.GetDouble());
												break;
											case "string":
												result = await client.ReadHoldingRegisters(id, address, count);
												Console.WriteLine();
												Console.WriteLine("UTF8:             " + result?.GetString(count));
												Console.WriteLine("Unicode:          " + result?.GetString(count, 0, Encoding.Unicode));
												Console.WriteLine("BigEndianUnicode: " + result?.GetString(count, 0, Encoding.BigEndianUnicode));
												break;
											default:
												Console.Write("DataType unknown");
												break;
										}
									}
									catch
									{ }
									await Task.Delay(TimeSpan.FromMilliseconds(interval), cancellationToken);
								}
								while (interval > 0 && !cancellationToken.IsCancellationRequested);
							}
							break;
						case 2:
							{
								Console.Write("[1] Basic, [2] Regular, [3] Extended: ");
								int cat = Convert.ToInt32(Console.ReadLine().Trim());

								Dictionary<DeviceIDObject, string> info = null;
								switch (cat)
								{
									case 1:
										info = await client.ReadDeviceInformation(id, DeviceIDCategory.Basic);
										break;
									case 2:
										info = await client.ReadDeviceInformation(id, DeviceIDCategory.Regular);
										break;
									case 3:
										info = await client.ReadDeviceInformation(id, DeviceIDCategory.Extended);
										break;
								}
								if (info != null)
								{
									foreach (var kvp in info)
									{
										Console.WriteLine($"{kvp.Key}: {kvp.Value}");
									}
								}
							}
							break;
						case 9:
							{
								Console.Write("Address: ");
								ushort address = Convert.ToUInt16(Console.ReadLine().Trim());

								Console.Write("Bytes (HEX): ");
								string byteStr = Console.ReadLine().Trim();
								byteStr = byteStr.Replace(" ", "").ToLower();

								byte[] bytes = Enumerable.Range(0, byteStr.Length)
									.Where(i => i % 2 == 0)
									.Select(i => Convert.ToByte(byteStr.Substring(i, 2), 16))
									.ToArray();

								var registers = Enumerable.Range(0, bytes.Length)
									.Where(i => i % 2 == 0)
									.Select(i =>
									{
										return new Register
										{
											Type = ModbusObjectType.HoldingRegister,
											Address = address++,
											HiByte = bytes[i],
											LoByte = bytes[i + 1]
										};
									})
									.ToList();

								if (!await client.WriteRegisters(id, registers))
									throw new Exception($"Writing '{byteStr}' to address {address} failed");
							}
							break;
					}

					Console.Write("New Request? [y/N]: ");
					string again = Console.ReadLine().Trim().ToLower();
					if (!yesList.Contains(again))
						return 0;
				}

				return 0;
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				return 0;
			}
			finally
			{
				Console.WriteLine("Disposing");
				client?.Dispose();
				Console.WriteLine("Disposed");
			}
		}

		private static async Task<int> RunServerAsync(ILogger logger, CancellationToken cancellationToken)
		{
			Console.Write("Connection Type [1] TCP, [2] RS485: ");
			int cType = Convert.ToInt32(Console.ReadLine().Trim());

			IModbusServer server = null;
			try
			{
				switch (cType)
				{
					case 1:
						{
							Console.Write("Bind IP address: ");
							var ip = IPAddress.Parse(Console.ReadLine().Trim());

							Console.Write("Port: ");
							int port = Convert.ToInt32(Console.ReadLine().Trim());

							var tcp = new TcpServer(port, ip, logger)
							{
								Timeout = TimeSpan.FromSeconds(3)
							};

							server = tcp;
						}
						break;
					case 2:
						{
							Console.Write("Interface: ");
							string port = Console.ReadLine().Trim();

							Console.Write("Baud: ");
							int baud = Convert.ToInt32(Console.ReadLine().Trim());

							Console.Write("Stop-Bits [0|1|2|3=1.5]: ");
							int stopBits = Convert.ToInt32(Console.ReadLine().Trim());

							Console.Write("Parity [0] None [1] Odd [2] Even [3] Mark [4] Space: ");
							int parity = Convert.ToInt32(Console.ReadLine().Trim());

							Console.Write("Handshake [0] None [1] X-On/Off [2] RTS [3] RTS+X-On/Off: ");
							int handshake = Convert.ToInt32(Console.ReadLine().Trim());

							Console.Write("Timeout (ms): ");
							int timeout = Convert.ToInt32(Console.ReadLine().Trim());

							server = new SerialServer(port)
							{
								BaudRate = (BaudRate)baud,
								DataBits = 8,
								StopBits = (StopBits)stopBits,
								Parity = (Parity)parity,
								Handshake = (Handshake)handshake,
								Timeout = TimeSpan.FromMilliseconds(timeout)
							};
						}
						break;
					default:
						throw new ArgumentException("Type unknown");
				}

				server.AddDevice(1);
				server.AddDevice(5);
				server.AddDevice(10);

				Register.Create(123.45f, 100, false).ForEach(r => server.SetHoldingRegister(1, r));

				Console.WriteLine("Server is running... press CTRL+C to exit.");
				await Task.Delay(Timeout.Infinite, cancellationToken);
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{ }
			finally
			{
				server?.Dispose();
			}
			return 0;
		}
	}
}
