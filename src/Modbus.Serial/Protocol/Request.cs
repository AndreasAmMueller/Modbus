using AMWD.Modbus.Common;
using AMWD.Modbus.Common.Util;
using System;
using System.Linq;

namespace AMWD.Modbus.Serial.Protocol
{
	/// <summary>
	/// Represents the request from a client to the server.
	/// </summary>
	internal class Request
	{
		#region Constructors

		/// <summary>
		/// Initializes a new instance of the <see cref="Request"/> class.
		/// </summary>
		/// <remarks>
		/// The transaction id is automatically set to a unique number.
		/// </remarks>
		internal Request()
		{ }

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
		/// Gets or sets the number of elements.
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
		/// Serializes the request ready to send via serial.
		/// </summary>
		/// <returns></returns>
		internal byte[] Serialize()
		{
			var buffer = new DataBuffer(4);

			buffer.SetByte(0, DeviceId);
			buffer.SetByte(1, (byte)Function);

			buffer.SetUInt16(2, Address);

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

			var crc = Checksum.CRC16(buffer.Buffer);
			buffer.AddBytes(crc);

			return buffer.Buffer;
		}

		private void Deserialize(byte[] bytes)
		{
			var buffer = new DataBuffer(bytes);
			DeviceId = buffer.GetByte(0);
			Function = (FunctionCode)buffer.GetByte(1);
			Address = buffer.GetUInt16(2);

			var crcBuff = buffer.GetBytes(buffer.Length - 3, 2);
			var crcCalc = Checksum.CRC16(bytes, 0, bytes.Length - 2);

			if (crcBuff[0] != crcCalc[0] || crcBuff[1] != crcCalc[1])
			{
				throw new InvalidOperationException("Data not valid (CRC check failed).");
			}

			switch (Function)
			{
				case FunctionCode.ReadCoils:
				case FunctionCode.ReadDiscreteInputs:
				case FunctionCode.ReadHoldingRegisters:
				case FunctionCode.ReadInputRegisters:
					Count = buffer.GetUInt16(4);
					break;
				case FunctionCode.WriteMultipleCoils:
				case FunctionCode.WriteMultipleRegisters:
					Count = buffer.GetUInt16(4);
					Data = new DataBuffer(buffer.GetBytes(6, buffer.Length - 8));
					break;
				case FunctionCode.WriteSingleCoil:
				case FunctionCode.WriteSingleRegister:
					Data = new DataBuffer(buffer.GetBytes(4, buffer.Length - 6));
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
			return $"Request | Device#{DeviceId}, Fn: {Function}, Address: {Address}, Count: {Count} | {string.Join(" ", Bytes.Select(b => b.ToString("X2")).ToArray())}";
		}

		/// <inheritdoc/>
		public override int GetHashCode()
		{
			return base.GetHashCode() ^
				DeviceId.GetHashCode() ^
				Function.GetHashCode() ^
				Address.GetHashCode() ^
				Count.GetHashCode() ^
				Bytes.GetHashCode();
		}

		/// <inheritdoc/>
		public override bool Equals(object obj)
		{
			if (!(obj is Request req))
			{
				return false;
			}

			return req.DeviceId == DeviceId &&
				req.Function == Function &&
				req.Address == Address &&
				req.Count == Count &&
				Data.Equals(req.Data);
		}

		#endregion Overrides
	}
}
