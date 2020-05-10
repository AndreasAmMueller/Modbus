using System;
using System.Linq;
using AMWD.Modbus.Common;
using AMWD.Modbus.Common.Util;
using AMWD.Modbus.Serial.Util;

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

		/// <summary>
		/// Gets or sets the conformity level of the device information.
		/// </summary>
		public byte ConformityLevel { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether further requests are needed to gather all device information.
		/// </summary>
		public bool MoreRequestsNeeded { get; set; }

		/// <summary>
		/// Gets or sets the object id to start with the next request.
		/// </summary>
		public byte NextObjectId { get; set; }

		/// <summary>
		/// Gets or sets the number of objects in list (appending).
		/// </summary>
		public byte ObjectCount { get; set; }

		#endregion Device Information

		#endregion MODBUS Encapsulated Interface Transport

		#endregion Properties

		#region Serialization

		internal byte[] Serialize()
		{
			var buffer = new DataBuffer(2);

			buffer.SetByte(0, DeviceId);

			byte fn = (byte)Function;
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
								buffer.AddByte(ConformityLevel);
								buffer.AddByte((byte)(MoreRequestsNeeded ? 0xFF : 0x00));
								buffer.AddByte(NextObjectId);
								buffer.AddByte(ObjectCount);
								buffer.AddBytes(Data.Buffer);
								break;
							default:
								throw new NotImplementedException();
						}
						break;
					default:
						throw new NotImplementedException();
				}
			}

			buffer.SetByte(1, fn);

			byte[] crc = Checksum.CRC16(buffer.Buffer);
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

			byte[] crcBuff = buffer.GetBytes(buffer.Length - 2, 2);
			byte[] crcCalc = Checksum.CRC16(bytes, 0, bytes.Length - 2);

			if (crcBuff[0] != crcCalc[0] || crcBuff[1] != crcCalc[1])
				throw new InvalidOperationException("Data not valid (CRC check failed).");

			DeviceId = buffer.GetByte(0);

			byte fn = buffer.GetByte(1);
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
						byte len = buffer.GetByte(2);
						if (buffer.Length != len + 3 + 2)   // following bytes + 3 byte head + 2 byte CRC
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
					case FunctionCode.EncapsulatedInterface:
						MEIType = (MEIType)buffer.GetByte(2);
						switch (MEIType)
						{
							case MEIType.CANOpenGeneralReference:
								Data = new DataBuffer(buffer.Buffer.Skip(3).ToArray());
								break;
							case MEIType.ReadDeviceInformation:
								MEICategory = (DeviceIDCategory)buffer.GetByte(3);
								ConformityLevel = buffer.GetByte(4);
								MoreRequestsNeeded = buffer.GetByte(5) > 0;
								NextObjectId = buffer.GetByte(6);
								ObjectCount = buffer.GetByte(7);
								Data = new DataBuffer(buffer.Buffer.Skip(8).ToArray());
								break;
							default:
								throw new NotImplementedException();
						}
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
				return false;

			return res.DeviceId == DeviceId &&
				res.Function == Function &&
				res.Address == Address &&
				res.Count == Count &&
				Data.Equals(res.Data);
		}

		#endregion Overrides
	}
}
