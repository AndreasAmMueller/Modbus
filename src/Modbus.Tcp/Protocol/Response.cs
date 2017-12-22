using Modbus.Common;
using Modbus.Tcp.Utils;
using System;
using System.Linq;

namespace Modbus.Tcp.Protocol
{
	/// <summary>
	/// Represents the response from the server to a client.
	/// </summary>
	public class Response
	{
		#region Constructors

		/// <summary>
		/// Initializes a new instance of the <see cref="Response"/> class.
		/// </summary>
		/// <param name="request">The corresponding request.</param>
		public Response(Request request)
		{
			TransactionId = request.TransactionId;
			DeviceId = request.DeviceId;
			Function = request.Function;
			Address = request.Address;
			Count = request.Count;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Response"/> class.
		/// </summary>
		/// <param name="response">The serialized response.</param>
		internal Response(byte[] response)
		{
			Deserialize(response);
		}

		#endregion Constructors

		#region Properties

		/// <summary>
		/// Gets the unique transaction id.
		/// </summary>
		public ushort TransactionId { get; private set; }

		/// <summary>
		/// Gets the id to identify the device.
		/// </summary>
		public byte DeviceId { get; private set; }

		/// <summary>
		/// Gets the function code.
		/// </summary>
		public FunctionCode Function { get; private set; }

		/// <summary>
		/// Gets a value indicating whether an error occurred.
		/// </summary>
		public bool IsError => ErrorCode > 0;

		/// <summary>
		/// Gets or sets the error/exception code.
		/// </summary>
		public byte ErrorCode { get; set; }

		/// <summary>
		/// Gets the error message.
		/// </summary>
		public string ErrorMessage => Consts.ErrorMessages[ErrorCode];

		/// <summary>
		/// Gets or sets the register address.
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

		internal byte[] Serialize()
		{
			var buffer = new DataBuffer(8);

			buffer.SetUInt16(0, TransactionId);
			buffer.SetUInt16(2, 0x0000);
			buffer.SetByte(6, DeviceId);

			var fn = (byte)Function;
			if (IsError)
			{
				fn = (byte)(fn & Consts.ErrorMask);
				buffer.AddByte(ErrorCode);
			}
			else
			{
				switch (Function)
				{
					case FunctionCode.ReadCoils:
					case FunctionCode.ReadDiscreteInputs:
					case FunctionCode.ReadHoldingRegisters:
					case FunctionCode.ReadInputRegisters:
						buffer.AddByte((byte)Data.Length);
						buffer.AddBytes(Data.Buffer);
						break;
					case FunctionCode.WriteMultipleCoils:
					case FunctionCode.WriteMultipleRegisters:
						buffer.AddUInt16(Address);
						buffer.AddUInt16(Count);
						break;
					case FunctionCode.WriteSingleCoil:
					case FunctionCode.WriteSingleRegister:
						buffer.AddUInt16(Address);
						buffer.AddBytes(Data.Buffer);
						break;
					default:
						throw new NotImplementedException();
				}
			}

			buffer.SetByte(7, fn);

			var len = buffer.Length - 6;
			buffer.SetUInt16(4, (ushort)len);

			return buffer.Buffer;
		}

		private void Deserialize(byte[] bytes)
		{
			var buffer = new DataBuffer(bytes);
			var ident = buffer.GetUInt16(2);
			if (ident != 0)
			{
				throw new ArgumentException("Response not valid Modbus TCP protocol");
			}
			var len = buffer.GetUInt16(4);
			if (buffer.Length != len + 6)
			{
				throw new ArgumentException("Response incomplete");
			}

			TransactionId = buffer.GetUInt16(0);
			DeviceId = buffer.GetByte(6);

			var fn = buffer.GetByte(7);
			if ((fn & Consts.ErrorMask) > 0)
			{
				Function = (FunctionCode)(fn ^ Consts.ErrorMask);
				ErrorCode = buffer.GetByte(8);
			}
			else
			{
				Function = (FunctionCode)fn;

				switch (Function)
				{
					case FunctionCode.ReadCoils:
					case FunctionCode.ReadDiscreteInputs:
					case FunctionCode.ReadHoldingRegisters:
					case FunctionCode.ReadInputRegisters:
						len = buffer.GetByte(8);
						if (buffer.Length != len + 9)
						{
							throw new ArgumentException("Response incomplete");
						}
						Data = new DataBuffer(buffer.Buffer.Skip(9).ToArray());
						break;
					case FunctionCode.WriteMultipleCoils:
					case FunctionCode.WriteMultipleRegisters:
						Address = buffer.GetUInt16(8);
						Count = buffer.GetUInt16(10);
						break;
					case FunctionCode.WriteSingleCoil:
					case FunctionCode.WriteSingleRegister:
						Address = buffer.GetUInt16(8);
						Data = new DataBuffer(buffer.Buffer.Skip(10).ToArray());
						break;
					default:
						throw new NotImplementedException();
				}
			}
		}

		#endregion Serialization

		#region Overrides

		/// <inheritdoc/>
		public override string ToString()
		{
			return $"Response#{TransactionId} | Device#{DeviceId}, Fn: {Function}, Error: {IsError}, Address: {Address}, Count: {Count} | {string.Join(" ", Data.Buffer.Select(b => b.ToString("X2")).ToArray())}";
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
			var res = obj as Response;
			if (res == null)
			{
				return false;
			}

			return res.TransactionId == TransactionId &&
				res.DeviceId == DeviceId &&
				res.Function == Function &&
				res.Address == Address &&
				res.Count == Count &&
				Data.Equals(res.Data);
		}

		#endregion Overrides
	}
}
