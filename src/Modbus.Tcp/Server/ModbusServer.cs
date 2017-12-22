using Modbus.Tcp.Protocol;
using Modbus.Tcp.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Modbus.Tcp.Server
{
	/// <summary>
	/// A server to communicate via Modbus TCP.
	/// </summary>
	public class ModbusServer : IDisposable
	{
		#region Fields

		private TcpListener tcpListener;
		private List<TcpClient> tcpClients = new List<TcpClient>();

		private ModbusRequestHandler requestHandler;

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

		#endregion Events

		#region Constructors

		/// <summary>
		/// Initializes a new instance of the <see cref="ModbusServer"/> class.
		/// </summary>
		/// <param name="port">The port to listen.</param>
		/// <param name="requestHandler">The handling method to process the request.</param>
		public ModbusServer(int port, ModbusRequestHandler requestHandler)
		{
			this.requestHandler = requestHandler;
			Initialize(port);
		}

		#endregion Constructors

		#region Properties

		/// <summary>
		/// Gets the UTC timestamp of the server start.
		/// </summary>
		public DateTime StartTime { get; private set; }

		/// <summary>
		/// Gets a value indicating whether the server is running.
		/// </summary>
		public bool IsRunning { get; private set; }

		#endregion Properties

		#region Private methods

		private void Initialize(int port)
		{
			if (isDisposed)
			{
				throw new ObjectDisposedException(GetType().FullName);
			}
			if (port < 1 || port > 65535)
			{
				throw new ArgumentOutOfRangeException(nameof(port));
			}

			tcpListener = new TcpListener(IPAddress.IPv6Any, port);
			tcpListener.Server.DualMode = true;
			tcpListener.Start();

			StartTime = DateTime.UtcNow;
			IsRunning = true;

			Console.WriteLine("Modbus server started to listen on " + port + "/tcp");

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

		private async void HandleClient(TcpClient client)
		{
			ClientConnected?.Invoke(this, new ClientEventArgs((IPEndPoint)client.Client.RemoteEndPoint));

			try
			{
				var stream = client.GetStream();
				while (true)
				{
					var requestBytes = new List<byte>();

					var buffer = new byte[6];
					var count = await stream.ReadAsync(buffer, 0, buffer.Length);
					requestBytes.AddRange(buffer.Take(count));

					var bytes = buffer.Skip(4).Take(2).ToArray();
					if (BitConverter.IsLittleEndian)
					{
						Array.Reverse(bytes);
					}
					int following = BitConverter.ToUInt16(bytes, 0);

					do
					{
						buffer = new byte[following];
						count = await stream.ReadAsync(buffer, 0, buffer.Length);
						following -= count;
						requestBytes.AddRange(buffer.Take(count));
					}
					while (following > 0);

					var request = new Request(requestBytes.ToArray());
					var response = requestHandler?.Invoke(request);

					if (response != null)
					{
						bytes = response.Serialize();
						await stream.WriteAsync(bytes, 0, bytes.Length);
					}
				}
			}
			catch (EndOfStreamException)
			{
				// client closed connection
			}

			ClientDisconnected?.Invoke(this, new ClientEventArgs((IPEndPoint)client.Client.RemoteEndPoint));

			lock (tcpClients)
			{
				tcpClients.Remove(client);
			}
		}

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
	/// Defines a method that handles an incoming Modbus request.
	/// </summary>
	/// <param name="request">The incoming request.</param>
	/// <returns>The response.</returns>
	public delegate Response ModbusRequestHandler(Request request);

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
