using System;
using System.Linq;
using System.Text;
using AMWD.Modbus.Common;
using AMWD.Modbus.Common.Util;
using AMWD.Modbus.Tcp.Util;

namespace AMWD.Modbus.Tcp.Protocol
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
			Data = new DataBuffer();
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Response"/> class.
		/// </summary>
		/// <param name="response">The serialized response.</param>
		public Response(byte[] response)
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
		/// Gets a value indicating whether the response timed out.
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

		/// <summary>
		/// Serializes the response to send.
		/// </summary>
		/// <returns></returns>
		public byte[] Serialize()
		{
			var buffer = new DataBuffer(8);

			buffer.SetUInt16(0, TransactionId);
			buffer.SetUInt16(2, 0x0000);
			buffer.SetByte(6, DeviceId);

			byte fn = (byte)Function;
			if (IsError)
			{
				fn = (byte)(fn | Consts.ErrorMask);
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

			buffer.SetByte(7, fn);

			int len = buffer.Length - 6;
			buffer.SetUInt16(4, (ushort)len);

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
			ushort ident = buffer.GetUInt16(2);
			if (ident != 0)
				throw new ArgumentException("Protocol identifier not valid.");

			ushort length = buffer.GetUInt16(4);
			if (buffer.Length < length + 6)
				throw new ArgumentException("Too less data.");

			if (buffer.Length > length + 6)
			{
				if (buffer.Buffer.Skip(length + 6).Any(b => b != 0))
					throw new ArgumentException("Too many data.");

				buffer = new DataBuffer(bytes.Take(length + 6));
			}

			TransactionId = buffer.GetUInt16(0);
			DeviceId = buffer.GetByte(6);

			byte fn = buffer.GetByte(7);
			if ((fn & Consts.ErrorMask) > 0)
			{
				Function = (FunctionCode)(fn ^ Consts.ErrorMask);
				ErrorCode = (ErrorCode)buffer.GetByte(8);
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
						length = buffer.GetByte(8);
						if (buffer.Length != length + 9)
							throw new ArgumentException("Payload missing.");

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
					case FunctionCode.EncapsulatedInterface:
						MEIType = (MEIType)buffer.GetByte(8);
						switch (MEIType)
						{
							case MEIType.CANOpenGeneralReference:
								Data = new DataBuffer(buffer.Buffer.Skip(9).ToArray());
								break;
							case MEIType.ReadDeviceInformation:
								MEICategory = (DeviceIDCategory)buffer.GetByte(9);
								ConformityLevel = buffer.GetByte(10);
								MoreRequestsNeeded = buffer.GetByte(11) > 0;
								NextObjectId = buffer.GetByte(12);
								ObjectCount = buffer.GetByte(13);
								Data = new DataBuffer(buffer.Buffer.Skip(14).ToArray());
								break;
							default:
								throw new NotImplementedException($"Unknown MEI type: {MEIType}.");
						}
						break;
					default:
						throw new NotImplementedException($"Unknown function code: {Function}.");
				}
			}
		}

		#endregion Serialization

		#region Overrides

		/// <inheritdoc/>
		public override string ToString()
		{
			var sb = new StringBuilder();
			if (Data != null)
			{
				foreach (byte b in Data.Buffer)
				{
					if (sb.Length > 0)
						sb.Append(" ");

					sb.Append(b.ToString("X2"));
				}
			}

			return $"Response#{TransactionId} | Device#{DeviceId}, Fn: {Function}, Error: {IsError}, Address: {Address}, Count: {Count} | {sb}";
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
				Data.GetHashCode();
		}

		/// <inheritdoc/>
		public override bool Equals(object obj)
		{
			if (!(obj is Response res))
				return false;

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
