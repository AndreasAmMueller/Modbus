using AMWD.Modbus.Common;
using AMWD.Modbus.Common.Structures;
using AMWD.Modbus.Common.Util;
using AMWD.Modbus.Tcp.Client;
using AMWD.Modbus.Tcp.Server;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace UnitTests
{
	[TestClass]
	public class ModbusTcpTests
	{
		#region Modbus Client

		#region Control

		[TestMethod]
		public async Task ClientConnectTest()
		{
			using (var server = new MiniTestServer())
			{
				server.Start();
				using (var client = new ModbusClient("localhost", server.Port))
				{
					await client.Connect();
					Assert.IsTrue(client.IsConnected);

					await client.Disconnect();
					Assert.IsFalse(client.IsConnected);
				}
			}
		}

		[TestMethod]
		public async Task ClientReconnectTest()
		{
			using (var server = new MiniTestServer())
			{
				server.Start();
				using (var client = new ModbusClient("localhost", server.Port))
				{
					await client.Connect();
					Assert.IsTrue(client.IsConnected);

					await server.Stop();

					await client.ReadHoldingRegisters(0, 0, 1);
					// Time for the scheduler to launch a thread to start the reconnect
					await Task.Delay(1);
					Assert.IsFalse(client.IsConnected);

					server.Start();
					await client.ConnectingTask;
					Assert.IsTrue(client.IsConnected);
				}
			}
		}

		[TestMethod]
		public async Task ClientEventsTest()
		{
			int connectEvents = 0;
			int disconnectEvents = 0;
			using (var server = new MiniTestServer())
			{
				server.Start();
				using (var client = new ModbusClient("localhost", server.Port))
				{
					client.Connected += (sender, args) =>
					{
						connectEvents++;
					};
					client.Disconnected += (sender, args) =>
					{
						disconnectEvents++;
					};

					Assert.AreEqual(0, connectEvents);
					Assert.AreEqual(0, disconnectEvents);

					await client.Connect();
					Assert.IsTrue(client.IsConnected);

					await Task.Delay(10);
					Assert.AreEqual(1, connectEvents);
					Assert.AreEqual(0, disconnectEvents);

					await server.Stop();

					await client.ReadHoldingRegisters(0, 0, 1);
					Assert.IsFalse(client.IsConnected);

					await Task.Delay(10);
					Assert.AreEqual(1, connectEvents);
					Assert.AreEqual(1, disconnectEvents);

					server.Start();
					await client.ConnectingTask;
					Assert.IsTrue(client.IsConnected);
				}

				await Task.Delay(10);
				Assert.AreEqual(2, connectEvents);
				Assert.AreEqual(2, disconnectEvents);
			}
		}

		#endregion Control

		#region Read

		[TestMethod]
		public async Task ClientReadExceptionTest()
		{
			var expectedRequest = new byte[] { 0, 0, 0, 6, 2, 1, 0, 24, 0, 2 };
			var expectedExceptionMessage = ErrorCode.GatewayTargetDevice.GetDescription();

			using (var server = new MiniTestServer())
			{
				server.RequestHandler = (request, clientIp) =>
				{
					CollectionAssert.AreEqual(expectedRequest, request.Skip(2).ToArray(), "Request is incorrect");
					Console.WriteLine("Server sending error response");
					return new byte[] { request[0], request[1], 0, 0, 0, 3, 2, 129, 11 };
				};
				server.Start();
				using (var client = new ModbusClient("localhost", server.Port))
				{
					await client.Connect();
					Assert.IsTrue(client.IsConnected);

					try
					{
						var response = await client.ReadCoils(2, 24, 2);
						Assert.IsTrue(string.IsNullOrWhiteSpace(server.LastError), server.LastError);
						Assert.Fail("Exception not thrown");
					}
					catch (ModbusException ex)
					{
						Assert.AreEqual(expectedExceptionMessage, ex.Message);
					}
				}
			}
		}

		[TestMethod]
		public async Task ClientReadCoilsTest()
		{
			// Function Code 0x01

			var expectedRequest = new byte[] { 0, 0, 0, 6, 12, 1, 0, 20, 0, 10 };
			var expectedResponse = new List<Coil>
					{
						new Coil { Address = 20, Value = true },
						new Coil { Address = 21, Value = false },
						new Coil { Address = 22, Value = true },
						new Coil { Address = 23, Value = true },
						new Coil { Address = 24, Value = false },
						new Coil { Address = 25, Value = false },
						new Coil { Address = 26, Value = true },
						new Coil { Address = 27, Value = true },
						new Coil { Address = 28, Value = true },
						new Coil { Address = 29, Value = false },
					};

			using (var server = new MiniTestServer())
			{
				server.RequestHandler = (request, clientIp) =>
				{
					CollectionAssert.AreEqual(expectedRequest, request.Skip(2).ToArray(), "Request is incorrect");
					Console.WriteLine("Server sending response");
					return new byte[] { request[0], request[1], 0, 0, 0, 5, 12, 1, 2, 205, 1 };
				};
				server.Start();
				using (var client = new ModbusClient("localhost", server.Port))
				{
					await client.Connect();
					Assert.IsTrue(client.IsConnected);

					var coils = await client.ReadCoils(12, 20, 10);
					Assert.IsTrue(string.IsNullOrWhiteSpace(server.LastError), server.LastError);
					CollectionAssert.AreEqual(expectedResponse, coils, "Response is incorrect");
				}
			}
		}

		[TestMethod]
		public async Task ClientReadDiscreteInputsTest()
		{
			// Function Code 0x02

			var expectedRequest = new byte[] { 0, 0, 0, 6, 1, 2, 0, 12, 0, 2 };
			var expectedResponse = new List<DiscreteInput>
			{
				new DiscreteInput { Address = 12, Value = true },
				new DiscreteInput { Address = 13, Value = true }
			};

			using (var server = new MiniTestServer())
			{
				server.RequestHandler = (request, clientIp) =>
				{
					CollectionAssert.AreEqual(expectedRequest, request.Skip(2).ToArray(), "Request is incorrect");
					Console.WriteLine("Server sending response");
					return new byte[] { request[0], request[1], 0, 0, 0, 4, 1, 2, 1, 3 };
				};
				server.Start();
				using (var client = new ModbusClient("localhost", server.Port))
				{
					await client.Connect();
					Assert.IsTrue(client.IsConnected);

					var inputs = await client.ReadDiscreteInputs(1, 12, 2);
					Assert.IsTrue(string.IsNullOrWhiteSpace(server.LastError), server.LastError);
					CollectionAssert.AreEqual(expectedResponse, inputs, "Response is incorrect");
				}
			}
		}

		[TestMethod]
		public async Task ClientReadHoldingRegisterTest()
		{
			// Function Code 0x03

			var expectedRequest = new byte[] { 0, 0, 0, 6, 5, 3, 0, 10, 0, 2 };
			var expectedResponse = new List<Register>
			{
				new Register { Address = 10, Value = 3 },
				new Register { Address = 11, Value = 7 }
			};

			using (var server = new MiniTestServer())
			{
				server.RequestHandler = (request, clientIp) =>
				{
					CollectionAssert.AreEqual(expectedRequest, request.Skip(2).ToArray(), "Request is incorrect");
					Console.WriteLine("Server sending response");
					return new byte[] { request[0], request[1], 0, 0, 0, 7, 5, 3, 4, 0, 3, 0, 7 };
				};
				server.Start();
				using (var client = new ModbusClient("localhost", server.Port))
				{
					await client.Connect();
					Assert.IsTrue(client.IsConnected);

					var registers = await client.ReadHoldingRegisters(5, 10, 2);
					Assert.IsTrue(string.IsNullOrWhiteSpace(server.LastError), server.LastError);
					CollectionAssert.AreEqual(expectedResponse, registers, "Response is incorrect");
				}
			}
		}

		[TestMethod]
		public async Task ClientReadInputRegisterTest()
		{
			// Function Code 0x04

			var expectedRequest = new byte[] { 0, 0, 0, 6, 3, 4, 0, 6, 0, 3 };
			var expectedResponse = new List<Register>
			{
				new Register { Address = 6, Value = 123 },
				new Register { Address = 7, Value = 0 },
				new Register { Address = 8, Value = 12345 }
			};

			using (var server = new MiniTestServer())
			{
				server.RequestHandler = (request, clientIp) =>
				{
					CollectionAssert.AreEqual(expectedRequest, request.Skip(2).ToArray(), "Request is incorrect");
					Console.WriteLine("Server sending response");
					return new byte[] { request[0], request[1], 0, 0, 0, 9, 3, 4, 6, 0, 123, 0, 0, 48, 57 };
				};
				server.Start();
				using (var client = new ModbusClient("localhost", server.Port))
				{
					await client.Connect();
					Assert.IsTrue(client.IsConnected);

					var registers = await client.ReadInputRegisters(3, 6, 3);
					Assert.IsTrue(string.IsNullOrWhiteSpace(server.LastError), server.LastError);
					CollectionAssert.AreEqual(expectedResponse, registers, "Response is incorrect");
				}
			}
		}

		[TestMethod]
		public async Task ClientReadDeviceInformationBasicTest()
		{
			var expectedRequest = new byte[] { 0, 0, 0, 5, 13, 43, 14, 1, 0 };
			var expectedResponse = new Dictionary<DeviceIDObject, string>
			{
				{ DeviceIDObject.VendorName, "AM.WD" },
				{ DeviceIDObject.ProductCode, "Mini-Test" },
				{ DeviceIDObject.MajorMinorRevision, "1.2.3.4" }
			};

			using (var server = new MiniTestServer())
			{
				server.RequestHandler = (request, clientIp) =>
				{
					CollectionAssert.AreEqual(expectedRequest, request.Skip(2).ToArray(), "Request is incorrect");
					Console.WriteLine("Server sending response");

					var bytes = new List<byte>();
					bytes.AddRange(request.Take(2));
					bytes.AddRange(new byte[] { 0, 0, 0, 0, 13, 43, 14, 1, 1, 0, 0, (byte)expectedResponse.Count });
					var len = 8;
					foreach (var kvp in expectedResponse)
					{
						var b = Encoding.ASCII.GetBytes(kvp.Value);
						bytes.Add((byte)kvp.Key);
						len++;
						bytes.Add((byte)b.Length);
						len++;
						bytes.AddRange(b);
						len += b.Length;
					}
					bytes[5] = (byte)len;

					return bytes.ToArray();
				};
				server.Start();
				using (var client = new ModbusClient("localhost", server.Port))
				{
					await client.Connect();
					Assert.IsTrue(client.IsConnected);

					var deviceInfo = await client.ReadDeviceInformation(13, DeviceIDCategory.Basic);
					Assert.IsTrue(string.IsNullOrWhiteSpace(server.LastError), server.LastError);
					CollectionAssert.AreEqual(expectedResponse, deviceInfo, "Response is incorrect");
				}
			}
		}

		[TestMethod]
		public async Task ClientReadDeviceInformationIndividualTest()
		{
			var expectedRequest = new byte[] { 0, 0, 0, 5, 13, 43, 14, 4, (byte)DeviceIDObject.ModelName };
			var expectedResponse = new Dictionary<DeviceIDObject, string>
			{
				{ DeviceIDObject.ModelName, "TestModel" }
			};

			using (var server = new MiniTestServer())
			{
				server.RequestHandler = (request, clientIp) =>
				{
					CollectionAssert.AreEqual(expectedRequest, request.Skip(2).ToArray(), "Request is incorrect");
					Console.WriteLine("Server sending response");

					var bytes = new List<byte>();
					bytes.AddRange(request.Take(2));
					bytes.AddRange(new byte[] { 0, 0, 0, 0, 13, 43, 14, 4, 2, 0, 0, (byte)expectedResponse.Count });
					var len = 8;
					foreach (var kvp in expectedResponse)
					{
						var b = Encoding.ASCII.GetBytes(kvp.Value);
						bytes.Add((byte)kvp.Key);
						len++;
						bytes.Add((byte)b.Length);
						len++;
						bytes.AddRange(b);
						len += b.Length;
					}
					bytes[5] = (byte)len;

					return bytes.ToArray();
				};
				server.Start();
				using (var client = new ModbusClient("localhost", server.Port))
				{
					await client.Connect();
					Assert.IsTrue(client.IsConnected);

					var deviceInfo = await client.ReadDeviceInformation(13, DeviceIDCategory.Individual, DeviceIDObject.ModelName);
					Assert.IsTrue(string.IsNullOrWhiteSpace(server.LastError), server.LastError);
					CollectionAssert.AreEqual(expectedResponse, deviceInfo, "Response is incorrect");
				}
			}
		}

		#endregion Read

		#region Write

		[TestMethod]
		public async Task ClientWriteSingleCoilTest()
		{
			// Function Code 0x05

			var expectedRequest = new byte[] { 0, 0, 0, 6, 1, 5, 0, 173, 255, 0 };

			using (var server = new MiniTestServer())
			{
				server.RequestHandler = (request, clientIp) =>
				{
					CollectionAssert.AreEqual(expectedRequest, request.Skip(2).ToArray(), "Request is incorrect");
					Console.WriteLine("Server sending response");
					return request;
				};
				server.Start();
				using (var client = new ModbusClient("localhost", server.Port))
				{
					await client.Connect();
					Assert.IsTrue(client.IsConnected);

					var coil = new Coil
					{
						Address = 173,
						Value = true
					};
					var success = await client.WriteSingleCoil(1, coil);
					Assert.IsTrue(string.IsNullOrWhiteSpace(server.LastError), server.LastError);
					Assert.IsTrue(success);
				}
			}
		}

		[TestMethod]
		public async Task ClientWriteSingleRegisterTest()
		{
			// Function Code 0x06

			var expectedRequest = new byte[] { 0, 0, 0, 6, 2, 6, 0, 5, 48, 57 };

			using (var server = new MiniTestServer())
			{
				server.RequestHandler = (request, clientIp) =>
				{
					CollectionAssert.AreEqual(expectedRequest, request.Skip(2).ToArray(), "Request is incorrect");
					Console.WriteLine("Server sending response");
					return request;
				};
				server.Start();
				using (var client = new ModbusClient("localhost", server.Port))
				{
					await client.Connect();
					Assert.IsTrue(client.IsConnected);

					var register = new Register
					{
						Address = 5,
						Value = 12345
					};
					var success = await client.WriteSingleRegister(2, register);
					Assert.IsTrue(string.IsNullOrWhiteSpace(server.LastError), server.LastError);
					Assert.IsTrue(success);
				}
			}
		}

		[TestMethod]
		public async Task ClientWriteCoilsTest()
		{
			// Function Code 0x0F

			var expectedRequest = new byte[] { 0, 0, 0, 9, 4, 15, 0, 20, 0, 10, 2, 205, 1 };

			using (var server = new MiniTestServer())
			{
				server.RequestHandler = (request, clientIp) =>
				{
					CollectionAssert.AreEqual(expectedRequest, request.Skip(2).ToArray(), "Request is incorrect");
					Console.WriteLine("Server sending response");
					return new byte[] { request[0], request[1], 0, 0, 0, 6, 4, 15, 0, 20, 0, 10 };
				};
				server.Start();
				using (var client = new ModbusClient("localhost", server.Port))
				{
					await client.Connect();
					Assert.IsTrue(client.IsConnected);

					var coils = new List<Coil>
					{
						new Coil { Address = 20, Value = true },
						new Coil { Address = 21, Value = false },
						new Coil { Address = 22, Value = true },
						new Coil { Address = 23, Value = true },
						new Coil { Address = 24, Value = false },
						new Coil { Address = 25, Value = false },
						new Coil { Address = 26, Value = true },
						new Coil { Address = 27, Value = true },
						new Coil { Address = 28, Value = true },
						new Coil { Address = 29, Value = false },
					};
					var success = await client.WriteCoils(4, coils);
					Assert.IsTrue(string.IsNullOrWhiteSpace(server.LastError), server.LastError);
					Assert.IsTrue(success);
				}
			}
		}

		[TestMethod]
		public async Task ClientWriteRegistersTest()
		{
			// Function Code 0x10

			var expectedRequest = new byte[] { 0, 0, 0, 11, 10, 16, 0, 2, 0, 2, 4, 0, 10, 1, 2 };

			using (var server = new MiniTestServer())
			{
				server.RequestHandler = (request, clientIp) =>
				{
					CollectionAssert.AreEqual(expectedRequest, request.Skip(2).ToArray(), "Request is incorrect");
					Console.WriteLine("Server sending response");
					return new byte[] { request[0], request[1], 0, 0, 0, 6, 10, 16, 0, 2, 0, 2 };
				};
				server.Start();
				using (var client = new ModbusClient("localhost", server.Port))
				{
					await client.Connect();
					Assert.IsTrue(client.IsConnected);

					var registers = new List<Register>
					{
						new Register { Address = 2, Value = 10 },
						new Register { Address = 3, Value = 258 }
					};
					var success = await client.WriteRegisters(10, registers);
					Assert.IsTrue(string.IsNullOrWhiteSpace(server.LastError), server.LastError);
					Assert.IsTrue(success);
				}
			}
		}

		#endregion Write

		#endregion Modbus Client

		#region Modbus Server

		[TestMethod]
		public async Task ServerStartTest()
		{
			var port = 0;
			using (var testServer = new MiniTestServer())
			{
				testServer.Start();
				port = testServer.Port;
			}

			using (var server = new ModbusServer(port))
			{
				await server.Initialization;
				Assert.IsTrue(server.IsRunning);
			}
		}

		#endregion Modbus Server

		#region TestServer

		internal delegate byte[] MiniTestServerRequestHandler(byte[] request, IPEndPoint endPoint);

		internal class MiniTestServer : IDisposable
		{
			private TcpListener listener;
			private CancellationTokenSource cts;

			private Task runTask;

			public MiniTestServer(int port = 0)
			{
				Port = port;
			}

			public int Port { get; private set; }

			public string LastError { get; private set; }

			public MiniTestServerRequestHandler RequestHandler { get; set; }

			public void Start()
			{
				cts = new CancellationTokenSource();

				listener = new TcpListener(IPAddress.IPv6Loopback, Port);
				listener.Server.DualMode = true;
				listener.Start();

				Port = ((IPEndPoint)listener.LocalEndpoint).Port;

				Console.WriteLine("Server started: " + Port);
				runTask = Task.Run(() => RunServer(cts.Token));
			}

			public async Task Stop()
			{
				listener.Stop();
				cts.Cancel();
				await runTask;
				Console.WriteLine("Server stopped");
			}

			public void Dispose()
			{
				try
				{
					Stop().Wait();
				}
				catch
				{ }
			}

			private async Task RunServer(CancellationToken ct)
			{
				while (!ct.IsCancellationRequested)
				{
					try
					{
						var waitForClient = listener.AcceptTcpClientAsync();
						if (await Task.WhenAny(waitForClient, Task.Delay(Timeout.Infinite, ct)) == waitForClient)
						{
							using (var client = waitForClient.Result)
							{
								var clientEndPoint = (IPEndPoint)client.Client.RemoteEndPoint;
								var stream = client.GetStream();

								SpinWait.SpinUntil(() => stream.DataAvailable || ct.IsCancellationRequested);
								if (ct.IsCancellationRequested)
								{
									Console.WriteLine("Server cancel => WaitData");
									return;
								}

								var buffer = new byte[100];
								var bytes = new List<byte>();
								do
								{
									var count = await stream.ReadAsync(buffer, 0, buffer.Length, ct);
									bytes.AddRange(buffer.Take(count));
								}
								while (stream.DataAvailable && !ct.IsCancellationRequested);

								if (ct.IsCancellationRequested)
								{
									Console.WriteLine("Server cancel => DataRead");
									return;
								}

								Debug.WriteLine($"Server data read done: {bytes.Count} bytes");
								if (RequestHandler != null)
								{
									Console.WriteLine("Server send RequestHandler");
									try
									{
										var response = RequestHandler(bytes.ToArray(), clientEndPoint);
										Console.WriteLine($"Server response: {response?.Length ?? -1}");
										if (response != null)
										{
											await stream.WriteAsync(response, 0, response.Length, ct);
											Console.WriteLine("Server response written");
										}
									}
									catch (Exception ex)
									{
										LastError = ex.InnerException?.Message ?? ex.Message;
									}
								}
							}
						}
					}
					catch (Exception ex)
					{
						var msg = ex.InnerException?.Message ?? ex.Message;
						Console.WriteLine($"Server exception: " + msg);
					}
				}
			}
		}

		#endregion TestServer
	}
}
