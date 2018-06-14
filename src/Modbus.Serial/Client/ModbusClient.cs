using AMWD.Modbus.Common;
using AMWD.Modbus.Common.Interfaces;
using AMWD.Modbus.Common.Structures;
using AMWD.Modbus.Common.Util;
using AMWD.Modbus.Serial.Protocol;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AMWD.Modbus.Serial.Client
{
	/// <summary>
	/// A client to communicate with modbus devices via serial port.
	/// </summary>
	public class ModbusClient : IModbusClient
	{
		#region Fields

		private readonly object reconnectLock = new object();
		private readonly object sendLock = new object();
		private SerialPort serialPort;
		private bool reconnectFailed = false;
		private bool wasConnected = false;

		private BaudRate baudRate = BaudRate.Baud38400;
		private int dataBits = 8;
		private Parity parity = Parity.None;
		private StopBits stopBits = StopBits.None;
		private Handshake handshake = Handshake.None;
		private int bufferSize = 4096;
		private int sendTimeout = 1000;
		private int receiveTimeout = 1000;

		#endregion Fields

		#region Events

		/// <summary>
		/// Raised when the client has the connection successfully established.
		/// </summary>
		public event EventHandler Connected;

		/// <summary>
		/// Raised when the client has closed the connection.
		/// </summary>
		public event EventHandler Disconnected;

		#endregion Events

		#region Constructors

		/// <summary>
		/// Initializes a new instance of the <see cref="ModbusClient"/> class.
		/// </summary>
		/// <param name="portName">The serial port name.</param>
		public ModbusClient(string portName)
		{
			if (string.IsNullOrWhiteSpace(portName))
			{
				throw new ArgumentNullException(nameof(portName));
			}

			PortName = portName;

			Connect();
		}

		#endregion Constructors

		#region Properties

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
		/// Gets a value indicating whether the connection is established.
		/// </summary>
		public bool IsConnected => serialPort?.IsOpen ?? false;

		/// <summary>
		/// Gets or sets the max reconnect timespan until the reconnect is aborted.
		/// </summary>
		public TimeSpan ReconnectTimeSpan { get; set; } = TimeSpan.MaxValue;

		#endregion Properties

		#region Public methods

		#region Read methods

		/// <summary>
		/// Reads one or more coils of a device. (Modbus function 1).
		/// </summary>
		/// <param name="deviceId">The id to address the device (slave).</param>
		/// <param name="startAddress">The first coil number to read.</param>
		/// <param name="count">The number of coils to read.</param>
		/// <returns>A list of coils or null on error.</returns>
		public async Task<List<Coil>> ReadCoils(byte deviceId, ushort startAddress, ushort count)
		{
			if (isDisposed)
			{
				throw new ObjectDisposedException(GetType().FullName);
			}

			if (deviceId < Consts.MinDeviceId || Consts.MaxDeviceId < deviceId)
			{
				throw new ArgumentOutOfRangeException(nameof(deviceId));
			}
			if (startAddress < Consts.MinAddress || Consts.MaxAddress < startAddress + count)
			{
				throw new ArgumentOutOfRangeException(nameof(startAddress));
			}
			if (count < Consts.MinCount || Consts.MaxCoilCountRead < count)
			{
				throw new ArgumentOutOfRangeException(nameof(count));
			}

			List<Coil> list = null;
			try
			{
				var request = new Request
				{
					DeviceId = deviceId,
					Function = FunctionCode.ReadCoils,
					Address = startAddress,
					Count = count
				};
				var response = await SendRequest(request);
				if (response.IsTimeout)
				{
					throw new IOException("Request timed out");
				}
				if (response.IsError)
				{
					throw new ModbusException(response.ErrorMessage);
				}

				list = new List<Coil>();
				for (int i = 0; i < count; i++)
				{
					var posByte = i / 8;
					var posBit = i % 8;

					var val = response.Data[posByte] & (byte)Math.Pow(2, posBit);

					list.Add(new Coil
					{
						Address = (ushort)(startAddress + i),
						Value = val > 0
					});
				}
			}
			catch (IOException)
			{
				Task.Run((Action)Reconnect).Forget();
			}

			return list;
		}

		/// <summary>
		/// Reads one or more discrete inputs of a device. (Modbus function 2).
		/// </summary>
		/// <param name="deviceId">The id to address the device (slave).</param>
		/// <param name="startAddress">The first discrete input number to read.</param>
		/// <param name="count">The number of discrete inputs to read.</param>
		/// <returns>A list of discrete inputs or null on error.</returns>
		public async Task<List<DiscreteInput>> ReadDiscreteInputs(byte deviceId, ushort startAddress, ushort count)
		{
			if (isDisposed)
			{
				throw new ObjectDisposedException(GetType().FullName);
			}

			if (deviceId < Consts.MinDeviceId || Consts.MaxDeviceId < deviceId)
			{
				throw new ArgumentOutOfRangeException(nameof(deviceId));
			}
			if (startAddress < Consts.MinAddress || Consts.MaxAddress < startAddress + count)
			{
				throw new ArgumentOutOfRangeException(nameof(startAddress));
			}
			if (count < Consts.MinCount || Consts.MaxCoilCountRead < count)
			{
				throw new ArgumentOutOfRangeException(nameof(count));
			}

			List<DiscreteInput> list = null;
			try
			{
				var request = new Request
				{
					DeviceId = deviceId,
					Function = FunctionCode.ReadDiscreteInputs,
					Address = startAddress,
					Count = count
				};
				var response = await SendRequest(request);
				if (response.IsTimeout)
				{
					throw new IOException("Request timed out");
				}
				if (response.IsError)
				{
					throw new ModbusException(response.ErrorMessage);
				}

				list = new List<DiscreteInput>();
				for (int i = 0; i < count; i++)
				{
					var posByte = i / 8;
					var posBit = i % 8;

					var val = response.Data[posByte] & (byte)Math.Pow(2, posBit);

					list.Add(new DiscreteInput
					{
						Address = (ushort)(startAddress + i),
						Value = val > 0
					});
				}
			}
			catch (IOException)
			{
				Task.Run((Action)Reconnect).Forget();
			}

			return list;
		}

		/// <summary>
		/// Reads one or more holding registers of a device. (Modbus function 3).
		/// </summary>
		/// <param name="deviceId">The id to address the device (slave).</param>
		/// <param name="startAddress">The first register number to read.</param>
		/// <param name="count">The number of registers to read.</param>
		/// <returns>A list of registers or null on error.</returns>
		public async Task<List<Register>> ReadHoldingRegisters(byte deviceId, ushort startAddress, ushort count)
		{
			if (isDisposed)
			{
				throw new ObjectDisposedException(GetType().FullName);
			}

			if (deviceId < Consts.MinDeviceId || Consts.MaxDeviceId < deviceId)
			{
				throw new ArgumentOutOfRangeException(nameof(deviceId));
			}
			if (startAddress < Consts.MinAddress || Consts.MaxAddress < startAddress + count)
			{
				throw new ArgumentOutOfRangeException(nameof(startAddress));
			}
			if (count < Consts.MinCount || Consts.MaxRegisterCountRead < count)
			{
				throw new ArgumentOutOfRangeException(nameof(count));
			}

			List<Register> list = null;
			try
			{
				var request = new Request
				{
					DeviceId = deviceId,
					Function = FunctionCode.ReadHoldingRegisters,
					Address = startAddress,
					Count = count
				};
				var response = await SendRequest(request);
				if (response.IsTimeout)
				{
					throw new IOException("Request timed out");
				}
				if (response.IsError)
				{
					throw new ModbusException(response.ErrorMessage);
				}

				list = new List<Register>();
				for (int i = 0; i < count; i++)
				{
					list.Add(new Register
					{
						Address = (ushort)(startAddress + i),
						HiByte = response.Data[i * 2],
						LoByte = response.Data[i * 2 + 1]
					});
				}
			}
			catch (IOException)
			{
				Task.Run((Action)Reconnect).Forget();
			}

			return list;
		}

		/// <summary>
		/// Reads one or more input registers of a device. (Modbus function 4).
		/// </summary>
		/// <param name="deviceId">The id to address the device (slave).</param>
		/// <param name="startAddress">The first register number to read.</param>
		/// <param name="count">The number of registers to read.</param>
		/// <returns>A list of registers or null on error.</returns>
		public async Task<List<Register>> ReadInputRegisters(byte deviceId, ushort startAddress, ushort count)
		{
			if (isDisposed)
			{
				throw new ObjectDisposedException(GetType().FullName);
			}

			if (deviceId < Consts.MinDeviceId || Consts.MaxDeviceId < deviceId)
			{
				throw new ArgumentOutOfRangeException(nameof(deviceId));
			}
			if (startAddress < Consts.MinAddress || Consts.MaxAddress < startAddress + count)
			{
				throw new ArgumentOutOfRangeException(nameof(startAddress));
			}
			if (count < Consts.MinCount || Consts.MaxRegisterCountRead < count)
			{
				throw new ArgumentOutOfRangeException(nameof(count));
			}

			List<Register> list = null;
			try
			{
				var request = new Request
				{
					DeviceId = deviceId,
					Function = FunctionCode.ReadInputRegisters,
					Address = startAddress,
					Count = count
				};
				var response = await SendRequest(request);
				if (response.IsTimeout)
				{
					throw new IOException("Request timed out");
				}
				if (response.IsError)
				{
					throw new ModbusException(response.ErrorMessage);
				}

				list = new List<Register>();
				for (int i = 0; i < count; i++)
				{
					list.Add(new Register
					{
						Address = (ushort)(startAddress + i),
						HiByte = response.Data[i * 2],
						LoByte = response.Data[i * 2 + 1]
					});
				}
			}
			catch (IOException)
			{
				Task.Run((Action)Reconnect).Forget();
			}

			return list;
		}

		#endregion Read methods

		#region Write methods

		/// <summary>
		/// Writes a single coil status to the Modbus device. (Modbus function 5)
		/// </summary>
		/// <param name="deviceId">The id to address the device (slave).</param>
		/// <param name="coil">The coil to write.</param>
		/// <returns>true on success, otherwise false.</returns>
		public async Task<bool> WriteSingleCoil(byte deviceId, Coil coil)
		{
			if (isDisposed)
			{
				throw new ObjectDisposedException(GetType().FullName);
			}

			if (coil == null)
			{
				throw new ArgumentNullException(nameof(coil));
			}
			if (deviceId < Consts.MinDeviceId || Consts.MaxDeviceId < deviceId)
			{
				throw new ArgumentOutOfRangeException(nameof(deviceId));
			}
			if (coil.Address < Consts.MinAddress || Consts.MaxAddress < coil.Address)
			{
				throw new ArgumentOutOfRangeException(nameof(coil.Address));
			}

			try
			{
				var request = new Request
				{
					DeviceId = deviceId,
					Function = FunctionCode.WriteSingleCoil,
					Address = coil.Address,
					Data = new DataBuffer(2)
				};
				var value = (ushort)(coil.Value ? 0xFF00 : 0x0000);
				request.Data.SetUInt16(0, value);
				var response = await SendRequest(request);
				if (response.IsTimeout)
				{
					throw new ModbusException("Response timed out. Device id invalid?");
				}
				if (response.IsError)
				{
					throw new ModbusException(response.ErrorMessage);
				}

				return request.DeviceId == response.DeviceId &&
					request.Function == response.Function &&
					request.Address == response.Address &&
					request.Data.Equals(response.Data);
			}
			catch (IOException)
			{
				Task.Run((Action)Reconnect).Forget();
			}

			return false;
		}

		/// <summary>
		/// Writes a single register to the Modbus device. (Modbus function 6)
		/// </summary>
		/// <param name="deviceId">The id to address the device (slave).</param>
		/// <param name="register">The register to write.</param>
		/// <returns>true on success, otherwise false.</returns>
		public async Task<bool> WriteSingleRegister(byte deviceId, Register register)
		{
			if (isDisposed)
			{
				throw new ObjectDisposedException(GetType().FullName);
			}

			if (register == null)
			{
				throw new ArgumentNullException(nameof(register));
			}
			if (deviceId < Consts.MinDeviceId || Consts.MaxDeviceId < deviceId)
			{
				throw new ArgumentOutOfRangeException(nameof(deviceId));
			}
			if (register.Address < Consts.MinAddress || Consts.MaxAddress < register.Address)
			{
				throw new ArgumentOutOfRangeException(nameof(register.Address));
			}

			try
			{
				var request = new Request
				{
					DeviceId = deviceId,
					Function = FunctionCode.WriteSingleRegister,
					Address = register.Address,
					Data = new DataBuffer(new[] { register.HiByte, register.LoByte })
				};
				var response = await SendRequest(request);
				if (response.IsTimeout)
				{
					throw new ModbusException("Response timed out. Device id invalid?");
				}
				if (response.IsError)
				{
					throw new ModbusException(response.ErrorMessage);
				}

				return request.DeviceId == response.DeviceId &&
					request.Function == response.Function &&
					request.Address == response.Address &&
					request.Data.Equals(response.Data);
			}
			catch (IOException)
			{
				Task.Run((Action)Reconnect).Forget();
			}

			return false;
		}

		/// <summary>
		/// Writes multiple coil status to the Modbus device. (Modbus function 15)
		/// </summary>
		/// <param name="deviceId">The id to address the device (slave).</param>
		/// <param name="coils">A list of coils to write.</param>
		/// <returns>true on success, otherwise false.</returns>
		public async Task<bool> WriteCoils(byte deviceId, IEnumerable<Coil> coils)
		{
			if (isDisposed)
			{
				throw new ObjectDisposedException(GetType().FullName);
			}

			if (coils == null || !coils.Any())
			{
				throw new ArgumentNullException(nameof(coils));
			}
			if (deviceId < Consts.MinDeviceId || Consts.MaxDeviceId < deviceId)
			{
				throw new ArgumentOutOfRangeException(nameof(deviceId));
			}

			var orderedList = coils.OrderBy(c => c.Address).ToList();
			if (orderedList.Count < Consts.MinCount || Consts.MaxCoilCountWrite < orderedList.Count)
			{
				throw new ArgumentOutOfRangeException("Count");
			}

			var firstAddress = orderedList.First().Address;
			var lastAddress = orderedList.Last().Address;

			if (firstAddress + orderedList.Count - 1 != lastAddress)
			{
				throw new ArgumentException("No address gabs allowed within a request");
			}
			if (firstAddress < Consts.MinAddress || Consts.MaxAddress < lastAddress)
			{
				throw new ArgumentOutOfRangeException("Address");
			}

			var numBytes = (int)Math.Ceiling(orderedList.Count / 8.0);
			var coilBytes = new byte[numBytes];
			for (int i = 0; i < orderedList.Count; i++)
			{
				if (orderedList[i].Value)
				{
					var posByte = i / 8;
					var posBit = i % 8;

					var mask = (byte)Math.Pow(2, posBit);
					coilBytes[posByte] = (byte)(coilBytes[posByte] | mask);
				}
			}

			try
			{
				var request = new Request
				{
					DeviceId = deviceId,
					Function = FunctionCode.WriteMultipleCoils,
					Address = firstAddress,
					Count = (ushort)orderedList.Count,
					Data = new DataBuffer(coilBytes)
				};
				var response = await SendRequest(request);
				if (response.IsTimeout)
				{
					throw new ModbusException("Response timed out. Device id invalid?");
				}
				if (response.IsError)
				{
					throw new ModbusException(response.ErrorMessage);
				}

				return request.Address == response.Address &&
					request.Count == response.Count;
			}
			catch (IOException)
			{
				Task.Run((Action)Reconnect).Forget();
			}

			return false;
		}

		/// <summary>
		/// Writes multiple registers to the Modbus device. (Modbus function 16)
		/// </summary>
		/// <param name="deviceId">The id to address the device (slave).</param>
		/// <param name="registers">A list of registers to write.</param>
		/// <returns>true on success, otherwise false.</returns>
		public async Task<bool> WriteRegisters(byte deviceId, IEnumerable<Register> registers)
		{
			if (isDisposed)
			{
				throw new ObjectDisposedException(GetType().FullName);
			}

			if (registers == null || !registers.Any())
			{
				throw new ArgumentNullException(nameof(registers));
			}
			if (deviceId < Consts.MinDeviceId || Consts.MaxDeviceId < deviceId)
			{
				throw new ArgumentOutOfRangeException(nameof(deviceId));
			}

			var orderedList = registers.OrderBy(c => c.Address).ToList();
			if (orderedList.Count < Consts.MinCount || Consts.MaxRegisterCountWrite < orderedList.Count)
			{
				throw new ArgumentOutOfRangeException("Count");
			}

			var firstAddress = orderedList.First().Address;
			var lastAddress = orderedList.Last().Address;

			if (firstAddress + orderedList.Count - 1 != lastAddress)
			{
				throw new ArgumentException("No address gabs allowed within a request");
			}
			if (firstAddress < Consts.MinAddress || Consts.MaxAddress < lastAddress)
			{
				throw new ArgumentOutOfRangeException("Address");
			}

			try
			{
				var request = new Request
				{
					DeviceId = deviceId,
					Function = FunctionCode.WriteMultipleRegisters,
					Address = firstAddress,
					Count = (ushort)orderedList.Count,
					Data = new DataBuffer(orderedList.Count * 2 + 1)
				};

				request.Data.SetByte(0, (byte)(orderedList.Count * 2));
				for (int i = 0; i < orderedList.Count; i++)
				{
					request.Data.SetUInt16(i * 2 + 1, orderedList[i].Value);
				}
				var response = await SendRequest(request);
				if (response.IsTimeout)
				{
					throw new ModbusException("Response timed out. Device id invalid?");
				}
				if (response.IsError)
				{
					throw new ModbusException(response.ErrorMessage);
				}

				return request.Address == response.Address &&
					request.Count == response.Count;
			}
			catch (IOException)
			{
				Task.Run((Action)Reconnect).Forget();
			}

			return false;
		}

		#endregion Write methods

		#endregion Public methods

		#region Private methods

		private async void Connect()
		{
			if (isDisposed)
			{
				throw new ObjectDisposedException(GetType().FullName);
			}

			await Task.Run((Action)Reconnect);
		}

		private void Reconnect()
		{
			if (isDisposed)
			{
				throw new ObjectDisposedException(GetType().FullName);
			}

			lock (reconnectLock)
			{
				if (reconnectFailed)
				{
					throw new InvalidOperationException("Reconnecting has failed");
				}
				if (serialPort?.IsOpen == true)
				{
					return;
				}

				if (wasConnected)
				{
					Task.Run(() => Disconnected?.Invoke(this, EventArgs.Empty));
				}

				var startTime = DateTime.UtcNow;
				while (true)
				{
					try
					{
						serialPort?.Dispose();
						serialPort = null;

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

						serialPort.Open();
					}
					catch (IOException) when (ReconnectTimeSpan == TimeSpan.MaxValue || DateTime.UtcNow <= startTime + ReconnectTimeSpan)
					{
						Thread.Sleep(1000);
						continue;
					}
					catch (Exception ex)
					{
						reconnectFailed = true;
						if (isDisposed)
						{
							return;
						}

						if (wasConnected)
						{
							throw new IOException("Server connection lost, reconnect failed.", ex);
						}
						else
						{
							throw new IOException("Could not connect to the server.", ex);
						}
					}

					if (!wasConnected)
					{
						wasConnected = true;
					}

					Task.Run(() => Connected?.Invoke(this, EventArgs.Empty));
					break;
				}
			}
		}

		private async Task<Response> SendRequest(Request request)
		{
			if (!IsConnected)
			{
				throw new InvalidOperationException("No connection");
			}

			return await Task.Run(() =>
			{
				var bytes = request.Serialize();
				serialPort.Write(bytes, 0, bytes.Length);

				var responseBytes = new List<byte>();
				do
				{
					var buffer = new byte[BufferSize];
					var count = serialPort.Read(buffer, 0, buffer.Length);
					responseBytes.AddRange(buffer.Take(count));
				}
				while (serialPort.BytesToRead > 0);

				return new Response(responseBytes.ToArray());
			});
		}

		#endregion Private methods

		#region IDisposable implementation

		/// <summary>
		/// Releases all managed and unmanaged resources used by the <see cref="ModbusClient"/>.
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
				//tcpClient?.Dispose();
				//tcpClient = null;
			}

			isDisposed = true;
		}

		#endregion IDisposable implementation

		#region Overrides

		/// <inheritdoc/>
		public override string ToString()
		{
			if (isDisposed)
			{
				throw new ObjectDisposedException(GetType().FullName);
			}

			return $"Modbus Serial {PortName} - Connected: {IsConnected}";
		}

		#endregion Overrides
	}
}
