using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AMWD.Modbus.Common;
using AMWD.Modbus.Common.Interfaces;
using AMWD.Modbus.Common.Structures;
using AMWD.Modbus.Common.Util;
using AMWD.Modbus.Serial;
using SerialClient = AMWD.Modbus.Serial.Client.ModbusClient;
using TcpClient = AMWD.Modbus.Tcp.Client.ModbusClient;

namespace ConsoleDemo
{
	internal class Program
	{
		private static bool run = true;

		private static void Main(string[] args)
		{
			try
			{
				MainAsync(args).GetAwaiter().GetResult();
			}
			catch (Exception ex)
			{
				Console.Error.WriteLine(ex.Message);
			}
		}

		private static async Task MainAsync(string[] args)
		{
			Console.WriteLine("Console Demo Modbus Client");
			Console.WriteLine();

			Console.Write("Connection Type [1] TCP, [2] RS485: ");
			int cType = Convert.ToInt32(Console.ReadLine().Trim());

			IModbusClient client = null;
			try
			{
				switch (cType)
				{
					case 1:
						{
							Console.Write("Hostname: ");
							string host = Console.ReadLine().Trim();
							Console.Write("Port: ");
							int port = Convert.ToInt32(Console.ReadLine().Trim());

							client = new TcpClient(host, port);
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

							Console.Write("Timeout: ");
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
								SendTimeout = timeout,
								ReceiveTimeout = timeout
							};

							if (setDriver == 1)
							{
								((SerialClient)client).DriverEnableRS485 = true;
							}
						}
						break;
					default:
						throw new ArgumentException("Type unknown");
				}

				await client.Connect();

				while (run)
				{
					Console.Write("Device ID: ");
					byte id = Convert.ToByte(Console.ReadLine().Trim());

					Console.Write("Function [1] Read Register, [2] Device Info, [9] Write Register : ");
					int fn = Convert.ToInt32(Console.ReadLine().Trim());

					try
					{
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
					}
					catch (Exception ex)
					{
						Console.WriteLine();
						Console.WriteLine("ERROR: " + ex.Message);
					}

					Console.Write("New Request? [y/N]: ");
					string again = Console.ReadLine().Trim().ToLower();
					if (again == "y" || again == "yes" || again == "j" || again == "ja")
					{
						run = true;
					}
					else
					{
						run = false;
					}
				}
			}
			finally
			{
				client?.Dispose();
			}
		}
	}
}
