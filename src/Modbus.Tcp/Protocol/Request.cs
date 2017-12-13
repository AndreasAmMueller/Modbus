using Modbus.Tcp.Utils;
using System;
using System.Linq;

namespace Modbus.Tcp.Protocol
{
	internal class Request
	{
		private static ushort transactionNumber = 0;
		private static ushort NextTransactionId
		{
			get
			{
				transactionNumber++;
				return transactionNumber;
			}
		}

		public Request(MessageType type)
		{
			Type = type;
			TransactionId = NextTransactionId;
		}

		public MessageType Type { get; private set; }

		public ushort TransactionId { get; private set; }

		public byte DeviceId { get; set; }

		public byte Function { get; set; }

		public ushort Address { get; set; }

		public ushort Count { get; set; }

		public DataBuffer Data { get; set; }

		public byte[] Serialize()
		{
			var buffer = new DataBuffer(10);

			buffer.SetUInt16(0, TransactionId);
			buffer.SetUInt16(2, 0x0000); // Protocol ID

			buffer.SetByte(6, DeviceId);
			buffer.SetByte(7, Function);

			buffer.SetUInt16(8, Address);

			switch (Type)
			{
				case MessageType.Read:
					buffer.AddUInt16(Count);
					break;
				case MessageType.WriteSingle:
					if (Data?.Length > 0)
					{
						buffer.AddBytes(Data.Buffer);
					}
					break;
				case MessageType.WriteMultiple:
					buffer.AddUInt16(Count);
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

		public override string ToString()
		{
			return $"Request#{TransactionId} | Device#{DeviceId}, Fn: {Function}, Address: {Address}, Count: {Count} | {string.Join(" ", Data.Buffer.Select(b => b.ToString("X2")).ToArray())}";
		}
	}
}
