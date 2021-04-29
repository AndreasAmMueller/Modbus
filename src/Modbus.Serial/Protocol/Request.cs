using System;
using System.Linq;
using AMWD.Modbus.Common;
using AMWD.Modbus.Common.Util;

namespace AMWD.Modbus.Serial.Protocol
{
	/// <summary>
	/// Represents the request from a client to the server.
	/// </summary>
	public class Request
	{
		#region Constructors

		/// <summary>
		/// Initializes a new instance of the <see cref="Request"/> class.
		/// </summary>
		/// <remarks>
		/// The transaction id is automatically set to a unique number.
		/// </remarks>
		public Request()
		{ }

		/// <summary>
		/// Initializes a new instance of the <see cref="Request"/> class.
		/// </summary>
		/// <param name="bytes">The serialized request from the client.</param>
		public Request(byte[] bytes)
		{
			if (bytes?.Any() != true)
				throw new ArgumentNullException(nameof(bytes));

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
			get { return Data?.Buffer ?? new byte[0]; }
			set { Data = new DataBuffer(value); }
		}

		/// <summary>
		/// Gets or sets the data.
		/// </summary>
		internal DataBuffer Data { get; set; }

		#region MODBUS Encapsulated Interface Transport

		/// <summary>
		/// Gets or sets the Encapsulated Interface type.
		/// Only needed on <see cref="FunctionCode.EncapsulatedInterface"/>.
		/// </summary>
		public MEIType MEIType { get; set; }

		#region Device Information

		/// <summary>
		/// Gets or sets the Device ID code (category).
		/// Only needed on <see cref="FunctionCode.EncapsulatedInterface"/> and <see cref="MEIType.ReadDeviceInformation"/>.
		/// </summary>
		public DeviceIDCategory MEICategory { get; set; }

		/// <summary>
		/// Gets or sets the first Object ID to read.
		/// </summary>
		public DeviceIDObject MEIObject { get; set; }

		#endregion Device Information

		#endregion MODBUS Encapsulated Interface Transport

		#endregion Properties

		#region Serialization

		/// <summary>
		/// Serializes the request ready to send via serial.
		/// </summary>
		/// <returns></returns>
		public byte[] Serialize()
		{
			var buffer = new DataBuffer(2);

			buffer.SetByte(0, DeviceId);
			buffer.SetByte(1, (byte)Function);

			switch (Function)
			{
				case FunctionCode.ReadCoils:
				case FunctionCode.ReadDiscreteInputs:
				case FunctionCode.ReadHoldingRegisters:
				case FunctionCode.ReadInputRegisters:
					buffer.AddUInt16(Address);
					buffer.AddUInt16(Count);
					break;
				case FunctionCode.WriteMultipleCoils:
				case FunctionCode.WriteMultipleRegisters:
					buffer.AddUInt16(Address);
					buffer.AddUInt16(Count);
					if (Data?.Length > 0)
						buffer.AddBytes(Data.Buffer);
					break;
				case FunctionCode.WriteSingleCoil:
				case FunctionCode.WriteSingleRegister:
					buffer.AddUInt16(Address);
					if (Data?.Length > 0)
						buffer.AddBytes(Data.Buffer);
					break;
				case FunctionCode.EncapsulatedInterface:
					buffer.AddByte((byte)MEIType);
					switch (MEIType)
					{
						case MEIType.CANOpenGeneralReference:
							if (Data?.Length > 0)
								buffer.AddBytes(Data.Buffer);
							break;
						case MEIType.ReadDeviceInformation:
							buffer.AddByte((byte)MEICategory);
							buffer.AddByte((byte)MEIObject);
							break;
						default:
							throw new NotImplementedException();
					}
					break;
				default:
					throw new NotImplementedException();
			}

			byte[] crc = Checksum.CRC16(buffer.Buffer);
			buffer.AddBytes(crc);

			return buffer.Buffer;
		}

		private void Deserialize(byte[] bytes)
		{
			var buffer = new DataBuffer(bytes);

			DeviceId = buffer.GetByte(0);
			Function = (FunctionCode)buffer.GetByte(1);

			byte[] crcBuff = buffer.GetBytes(buffer.Length - 3, 2);
			byte[] crcCalc = Checksum.CRC16(bytes, 0, bytes.Length - 2);

			if (crcBuff[0] != crcCalc[0] || crcBuff[1] != crcCalc[1])
				throw new InvalidOperationException("Data not valid (CRC check failed).");

			switch (Function)
			{
				case FunctionCode.ReadCoils:
				case FunctionCode.ReadDiscreteInputs:
				case FunctionCode.ReadHoldingRegisters:
				case FunctionCode.ReadInputRegisters:
					Address = buffer.GetUInt16(2);
					Count = buffer.GetUInt16(4);
					break;
				case FunctionCode.WriteMultipleCoils:
				case FunctionCode.WriteMultipleRegisters:
					Address = buffer.GetUInt16(2);
					Count = buffer.GetUInt16(4);
					Data = new DataBuffer(buffer.GetBytes(6, buffer.Length - 8));
					break;
				case FunctionCode.WriteSingleCoil:
				case FunctionCode.WriteSingleRegister:
					Address = buffer.GetUInt16(2);
					Data = new DataBuffer(buffer.GetBytes(4, buffer.Length - 6));
					break;
				case FunctionCode.EncapsulatedInterface:
					MEIType = (MEIType)buffer.GetByte(8);
					switch (MEIType)
					{
						case MEIType.CANOpenGeneralReference:
							Data = new DataBuffer(buffer.Buffer.Skip(9).ToArray());
							break;
						case MEIType.ReadDeviceInformation:
							MEICategory = (DeviceIDCategory)buffer.GetByte(9);
							MEIObject = (DeviceIDObject)buffer.GetByte(10);
							break;
						default:
							throw new NotImplementedException($"Unknown MEI type: {MEIType}");
					}
					break;
				default:
					throw new NotImplementedException($"Unknown function code: {Function}");
			}
		}

		#endregion Serialization

		#region Overrides

		/// <inheritdoc/>
		public override string ToString()
			=> $"Request | Device#{DeviceId}, Fn: {Function}, Address: {Address}, Count: {Count} | {string.Join(" ", Bytes.Select(b => b.ToString("X2")))}";

		/// <inheritdoc/>
		public override int GetHashCode()
			=> base.GetHashCode() ^
				DeviceId.GetHashCode() ^
				Function.GetHashCode() ^
				Address.GetHashCode() ^
				Count.GetHashCode() ^
				Bytes.GetHashCode();

		/// <inheritdoc/>
		public override bool Equals(object obj)
		{
			if (obj is not Request req)
				return false;

			return req.DeviceId == DeviceId &&
				req.Function == Function &&
				req.Address == Address &&
				req.Count == Count &&
				Data.Equals(req.Data);
		}

		#endregion Overrides
	}
}
