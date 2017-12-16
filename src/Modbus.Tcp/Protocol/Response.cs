using Modbus.Tcp.Utils;
using System;
using System.Linq;

namespace Modbus.Tcp.Protocol
{
	internal class Response
	{
		public Response(MessageType type, byte[] response)
		{
			var buffer = new DataBuffer(response);
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
				IsError = true;
				Function = (FunctionCode)(fn ^ Consts.ErrorMask);
			}
			else
			{
				Function = (FunctionCode)fn;
			}

			if (IsError)
			{
				ErrorCode = buffer.GetByte(8);
			}
			else
			{
				switch (type)
				{
					case MessageType.Read:
						len = buffer.GetByte(8);
						if (buffer.Length != len + 9)
						{
							throw new ArgumentException("Response incomplete");
						}
						Data = new DataBuffer(buffer.Buffer.Skip(9).ToArray());
						break;
					case MessageType.WriteSingle:
						Address = buffer.GetUInt16(8);
						Data = new DataBuffer(buffer.Buffer.Skip(10).ToArray());
						break;
					case MessageType.WriteMultiple:
						Address = buffer.GetUInt16(8);
						Count = buffer.GetUInt16(10);
						break;
					default:
						throw new NotImplementedException();
				}
			}
		}

		public ushort TransactionId { get; private set; }

		public byte DeviceId { get; private set; }

		public FunctionCode Function { get; private set; }

		public bool IsError { get; private set; }

		public byte ErrorCode { get; private set; }

		public string ErrorMessage => Consts.ErrorMessages[ErrorCode];

		public ushort Address { get; private set; }

		public ushort Count { get; private set; }

		public DataBuffer Data { get; private set; }

		public override string ToString()
		{
			return $"Response#{TransactionId} | Device#{DeviceId}, Fn: {Function}, Error: {IsError}, Address: {Address}, Count: {Count} | {string.Join(" ", Data.Buffer.Select(b => b.ToString("X2")).ToArray())}";
		}
	}
}
