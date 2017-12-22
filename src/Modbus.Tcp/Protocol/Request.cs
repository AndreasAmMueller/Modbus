using Modbus.Common;
using Modbus.Tcp.Utils;
using System;
using System.Linq;
using System.Threading;

namespace Modbus.Tcp.Protocol
{
	/// <summary>
	/// Represents the request from a client to the server.
	/// </summary>
	public class Request
	{
		#region Fields

		private static int transactionNumber = 0;
		private static ushort NextTransactionId
		{
			get
			{
				return (ushort)Interlocked.Increment(ref transactionNumber);
			}
		}

		#endregion Fields

		#region Constructors

		/// <summary>
		/// Initializes a new instance of the <see cref="Request"/> class.
		/// </summary>
		/// <remarks>
		/// The transaction id is automatically set to a unique number.
		/// </remarks>
		internal Request()
		{
			TransactionId = NextTransactionId;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Request"/> class.
		/// </summary>
		/// <param name="bytes">The serialized request from the client.</param>
		internal Request(byte[] bytes)
		{
			Deserialize(bytes);
		}

		#endregion Constructors

		#region Properties

		/// <summary>
		/// Gets the unique transaction id of the request.
		/// </summary>
		public ushort TransactionId { get; private set; }

		/// <summary>
		/// Gets the id to identify the device.
		/// </summary>
		public byte DeviceId { get; set; }

		/// <summary>
		/// Gets or sets the function code.
		/// </summary>
		public FunctionCode Function { get; set; }

		/// <summary>
		/// Gets or sets the (first) address.
		/// </summary>
		public ushort Address { get; set; }

		/// <summary>
		/// Gets or sets the number of registers.
		/// </summary>
		public ushort Count { get; set; }

		/// <summary>
		/// Gets or sets the data bytes.
		/// </summary>
		public byte[] Bytes
		{
			get { return Data.Buffer; }
			set { Data = new DataBuffer(value); }
		}

		/// <summary>
		/// Gets or sets the data.
		/// </summary>
		internal DataBuffer Data { get; set; }

		#endregion Properties

		#region Serialization

		/// <summary>
		/// Serializes the request ready to send via tcp.
		/// </summary>
		/// <returns></returns>
		internal byte[] Serialize()
		{
			var buffer = new DataBuffer(10);

			buffer.SetUInt16(0, TransactionId);
			buffer.SetUInt16(2, 0x0000); // Protocol ID

			buffer.SetByte(6, DeviceId);
			buffer.SetByte(7, (byte)Function);

			buffer.SetUInt16(8, Address);

			switch (Function)
			{
				case FunctionCode.ReadCoils:
				case FunctionCode.ReadDiscreteInputs:
				case FunctionCode.ReadHoldingRegisters:
				case FunctionCode.ReadInputRegisters:
					buffer.AddUInt16(Count);
					break;
				case FunctionCode.WriteMultipleCoils:
				case FunctionCode.WriteMultipleRegisters:
					buffer.AddUInt16(Count);
					if (Data?.Length > 0)
					{
						buffer.AddBytes(Data.Buffer);
					}
					break;
				case FunctionCode.WriteSingleCoil:
				case FunctionCode.WriteSingleRegister:
					if (Data?.Length > 0)
					{
						buffer.AddBytes(Data.Buffer);
					}
					break;
				default:
					throw new NotImplementedException();
			}

			var len = buffer.Length - 6;
			buffer.SetUInt16(4, (ushort)len);

			return buffer.Buffer;
		}

		private void Deserialize(byte[] bytes)
		{
			var buffer = new DataBuffer(bytes);
			TransactionId = buffer.GetUInt16(0);
			var ident = buffer.GetUInt16(2);
			if (ident != 0)
			{
				throw new ArgumentException("Protocol ident not valid");
			}
			var length = buffer.GetUInt16(4);
			if (length + 6 != buffer.Length)
			{
				throw new ArgumentException("Data incomplete");
			}
			DeviceId = buffer.GetByte(6);
			Function = (FunctionCode)buffer.GetByte(7);
			Address = buffer.GetUInt16(8);

			switch (Function)
			{
				case FunctionCode.ReadCoils:
				case FunctionCode.ReadDiscreteInputs:
				case FunctionCode.ReadHoldingRegisters:
				case FunctionCode.ReadInputRegisters:
					Count = buffer.GetUInt16(10);
					break;
				case FunctionCode.WriteMultipleCoils:
				case FunctionCode.WriteMultipleRegisters:
					Count = buffer.GetUInt16(10);
					Data = new DataBuffer(buffer.GetBytes(12, buffer.Length - 12));
					break;
				case FunctionCode.WriteSingleCoil:
				case FunctionCode.WriteSingleRegister:
					Data = new DataBuffer(buffer.GetBytes(10, buffer.Length - 10));
					break;
				default:
					throw new NotImplementedException();
			}
		}

		#endregion Serialization

		#region Overrides

		/// <inheritdoc/>
		public override string ToString()
		{
			return $"Request#{TransactionId} | Device#{DeviceId}, Fn: {Function}, Address: {Address}, Count: {Count} | {string.Join(" ", Bytes.Select(b => b.ToString("X2")).ToArray())}";
		}

		/// <inheritdoc/>
		public override int GetHashCode()
		{
			return base.GetHashCode() ^
				TransactionId.GetHashCode() ^
				DeviceId.GetHashCode() ^
				Function.GetHashCode() ^
				Address.GetHashCode() ^
				Count.GetHashCode() ^
				Bytes.GetHashCode();
		}

		/// <inheritdoc/>
		public override bool Equals(object obj)
		{
			var req = obj as Request;
			if (req == null)
			{
				return false;
			}

			return req.TransactionId == TransactionId &&
				req.DeviceId == DeviceId &&
				req.Function == Function &&
				req.Address == Address &&
				req.Count == Count &&
				Data.Equals(req.Data);
		}

		#endregion Overrides
	}
}
