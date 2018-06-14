using AMWD.Modbus.Common;
using AMWD.Modbus.Common.Util;
using System;
using System.Linq;

namespace AMWD.Modbus.Serial.Protocol
{
	/// <summary>
	/// Represents the response from the server to a client.
	/// </summary>
	internal class Response
	{
		#region Constructors

		/// <summary>
		/// Initializes a new instance of the <see cref="Response"/> class.
		/// </summary>
		/// <param name="request">The corresponding request.</param>
		public Response(Request request)
		{
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
		public ErrorCode ErrorCode { get; set; }

		/// <summary>
		/// Gets the error message.
		/// </summary>
		public string ErrorMessage => ErrorCode.GetDescription();

		/// <summary>
		/// Gets or sets the register address.
		/// </summary>
		public ushort Address { get; set; }

		/// <summary>
		/// Gets or sets the number of registers.
		/// </summary>
		public ushort Count { get; set; }

		/// <summary>
		/// Gets or sets the data.
		/// </summary>
		public DataBuffer Data { get; set; }

		/// <summary>
		/// Gets a value indicating whether the response is a result of an timeout.
		/// </summary>
		public bool IsTimeout { get; private set; }

		#endregion Properties

		#region Serialization

		internal byte[] Serialize()
		{
			var buffer = new DataBuffer(2);

			buffer.SetByte(0, DeviceId);

			var fn = (byte)Function;
			if (IsError)
			{
				fn = (byte)(fn & Consts.ErrorMask);
				buffer.AddByte((byte)ErrorCode);
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

			buffer.SetByte(1, fn);

			var crc = Checksum.CRC16(buffer.Buffer);
			buffer.AddBytes(crc);

			return buffer.Buffer;
		}

		private void Deserialize(byte[] bytes)
		{
			// Response timed out => device not available
			if (bytes.All(b => b == 0))
			{
				IsTimeout = true;
				return;
			}

			var buffer = new DataBuffer(bytes);

			var crcBuff = buffer.GetBytes(buffer.Length - 3, 2);
			var crcCalc = Checksum.CRC16(bytes, 0, bytes.Length - 2);

			if (crcBuff[0] != crcCalc[0] || crcBuff[1] != crcCalc[1])
			{
				throw new InvalidOperationException("Data not valid (CRC check failed).");
			}

			DeviceId = buffer.GetByte(0);

			var fn = buffer.GetByte(1);
			if ((fn & Consts.ErrorMask) > 0)
			{
				Function = (FunctionCode)(fn ^ Consts.ErrorMask);
				ErrorCode = (ErrorCode)buffer.GetByte(2);
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
						var len = buffer.GetByte(2);
						if (buffer.Length != len + 3)
						{
							throw new ArgumentException("Response incomplete");
						}
						Data = new DataBuffer(buffer.GetBytes(3, buffer.Length - 5));
						break;
					case FunctionCode.WriteMultipleCoils:
					case FunctionCode.WriteMultipleRegisters:
						Address = buffer.GetUInt16(2);
						Count = buffer.GetUInt16(4);
						break;
					case FunctionCode.WriteSingleCoil:
					case FunctionCode.WriteSingleRegister:
						Address = buffer.GetUInt16(2);
						Data = new DataBuffer(buffer.GetBytes(4, buffer.Length - 6));
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
			return $"Response | Device#{DeviceId}, Fn: {Function}, Error: {IsError}, Address: {Address}, Count: {Count} | {string.Join(" ", Data.Buffer.Select(b => b.ToString("X2")).ToArray())}";
		}

		/// <inheritdoc/>
		public override int GetHashCode()
		{
			return base.GetHashCode() ^
				DeviceId.GetHashCode() ^
				Function.GetHashCode() ^
				Address.GetHashCode() ^
				Count.GetHashCode() ^
				Data.GetHashCode();
		}

		/// <inheritdoc/>
		public override bool Equals(object obj)
		{
			if (!(obj is Response res))
			{
				return false;
			}

			return res.DeviceId == DeviceId &&
				res.Function == Function &&
				res.Address == Address &&
				res.Count == Count &&
				Data.Equals(res.Data);
		}

		#endregion Overrides
	}
}
