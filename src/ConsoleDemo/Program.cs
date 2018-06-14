using AMWD.Modbus.Common.Structures;
using AMWD.Modbus.Common.Util;
using AMWD.Modbus.Tcp.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ConsoleDemo
{
	internal class Program
	{
		private static bool run = true;

		private static void Main(string[] args)
		{
			Console.CancelKeyPress += Console_CancelKeyPress;
			MainAsync(args).Wait();
		}

		private static async Task MainAsync(string[] args)
		{
			Console.WriteLine("Console Demo Modbus TCP (Read Holding Registers)");
			Console.WriteLine();
			Console.WriteLine("Please enter the connection parameters:");

			Console.Write("Hostname : ");
			var host = Console.ReadLine();
			Console.Write("Port     : ");
			var port = Convert.ToInt32(Console.ReadLine());
			Console.Write("Device ID: ");
			var id = Convert.ToByte(Console.ReadLine());

			var client = new ModbusClient(host, port);

			while (run)
			{
				try
				{
					Console.WriteLine();
					Console.Write("Address : ");
					var address = Convert.ToUInt16(Console.ReadLine());
					Console.Write("DataType: ");
					var type = Console.ReadLine();
					Console.WriteLine();

					Console.Write("Result  : ");
					List<Register> result = null;
					switch (type.Trim().ToLower())
					{
						case "byte":
							result = await client.ReadHoldingRegisters(id, address, 1);
							Console.WriteLine(result.First().GetByte());
							break;
						case "ushort":
							result = await client.ReadHoldingRegisters(id, address, 1);
							Console.WriteLine(result.First().GetUInt16());
							break;
						case "uint":
							result = await client.ReadHoldingRegisters(id, address, 2);
							Console.WriteLine(result.GetUInt32());
							break;
						case "ulong":
							result = await client.ReadHoldingRegisters(id, address, 4);
							Console.WriteLine(result.GetUInt64());
							break;
						case "sbyte":
							result = await client.ReadHoldingRegisters(id, address, 1);
							Console.WriteLine(result.First().GetSByte());
							break;
						case "short":
							result = await client.ReadHoldingRegisters(id, address, 1);
							Console.WriteLine(result.First().GetInt16());
							break;
						case "int":
							result = await client.ReadHoldingRegisters(id, address, 2);
							Console.WriteLine(result.GetInt32());
							break;
						case "long":
							result = await client.ReadHoldingRegisters(id, address, 4);
							Console.WriteLine(result.GetInt64());
							break;
						case "float":
							result = await client.ReadHoldingRegisters(id, address, 2);
							Console.WriteLine(result.GetSingle());
							break;
						case "double":
							result = await client.ReadHoldingRegisters(id, address, 4);
							Console.WriteLine(result.GetDouble());
							break;
						default:
							Console.Write("DataType unknown");
							break;
					}
				}
				catch (Exception ex)
				{
					Console.WriteLine();
					Console.WriteLine("ERROR: " + ex.Message);
					Console.WriteLine();
				}
			}

			client.Dispose();
		}

		private static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
		{
			run = false;
		}
	}
}
