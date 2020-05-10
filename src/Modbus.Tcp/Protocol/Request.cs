using AMWD.Modbus.Common;
using AMWD.Modbus.Common.Util;
using System;
using System.Buffers.Binary;
using System.Linq;
using System.Threading;

namespace AMWD.Modbus.Tcp.Protocol
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
		internal Request(ReadOnlySpan<byte> bytes)
		{
			Deserialize(bytes);
		}

		#endregion Constructors

		#region Properties

		/// <summary>
		/// Gets the unique transaction id of the request.
		/// </summary>
		public ushort TransactionId { get; set; }

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
		/// Serializes the request ready to send via tcp.
		/// </summary>
		/// <returns></returns>
		internal byte[] Serialize()
		{
			var buffer = new DataBuffer(8);

			buffer.SetUInt16(0, TransactionId);
			buffer.SetUInt16(2, 0x0000); // Protocol ID

			buffer.SetByte(6, DeviceId);
			buffer.SetByte(7, (byte)Function);

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
					buffer.AddByte((byte)(Data?.Length ?? 0));
					if (Data?.Length > 0)
					{
						buffer.AddBytes(Data.Buffer);
					}
					break;
				case FunctionCode.WriteSingleCoil:
				case FunctionCode.WriteSingleRegister:
					buffer.AddUInt16(Address);
					if (Data?.Length > 0)
					{
						buffer.AddBytes(Data.Buffer);
					}
					break;
				case FunctionCode.EncapsulatedInterface:
					buffer.AddByte((byte)MEIType);
					switch (MEIType)
					{
						case MEIType.CANOpenGeneralReference:
							if (Data?.Length > 0)
							{
								buffer.AddBytes(Data.Buffer);
							}
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

			var len = buffer.Length - 6;
			buffer.SetUInt16(4, (ushort)len);

			return buffer.Buffer;
		}

		private void Deserialize(ReadOnlySpan<byte> bytes)
		{
			TransactionId = BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(0, 2));
			var ident = BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(2, 2));
			if (ident != 0)
			{
				throw new ArgumentException("Protocol ident not valid");
			}
			var length = BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(4, 2));
			if (length + 6 != bytes.Length)
			{
				throw new ArgumentException("Data incomplete");
			}
			DeviceId = bytes[6];
			Function = (FunctionCode)bytes[7];

			switch (Function)
			{
				case FunctionCode.ReadCoils:
				case FunctionCode.ReadDiscreteInputs:
				case FunctionCode.ReadHoldingRegisters:
				case FunctionCode.ReadInputRegisters:
					Address = BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(8, 2));
					Count = BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(10, 2));
					break;
				case FunctionCode.WriteMultipleCoils:
				case FunctionCode.WriteMultipleRegisters:
					Address = BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(8, 2));
					Count = BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(10, 2));
					Data = new DataBuffer(bytes.Slice(12).ToArray());
					break;
				case FunctionCode.WriteSingleCoil:
				case FunctionCode.WriteSingleRegister:
					Address = BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(8, 2));
					Data = new DataBuffer(bytes.Slice(10).ToArray());
					break;
				case FunctionCode.EncapsulatedInterface:
					MEIType = (MEIType)bytes[8];
					switch (MEIType)
					{
						case MEIType.CANOpenGeneralReference:
							Data = new DataBuffer(bytes.Slice(9).ToArray());
							break;
						case MEIType.ReadDeviceInformation:
							MEICategory = (DeviceIDCategory)bytes[9];
							MEIObject = (DeviceIDObject)bytes[10];
							break;
						default:
							throw new NotImplementedException();
					}
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
			if (!(obj is Request req))
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
