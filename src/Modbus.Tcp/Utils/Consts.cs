using System.Collections.Generic;

namespace Modbus.Tcp.Utils
{
	internal class Consts
	{
		public static readonly Dictionary<byte, string> ErrorMessages = new Dictionary<byte, string>
		{
			{  0, "No Error" },
			{  1, "Illegal Function" },
			{  2, "Illegal Data Address" },
			{  3, "Illegal Data Value" },
			{  4, "Slave Device Failure" },
			{  5, "Acknowledge" },
			{  6, "Slave Device Busy" },
			{  7, "Negative Acknowledge" },
			{  8, "Memory Parity Error" },
			{ 10, "Gateway Path Unavailable" },
			{ 11, "Gateway Target Device Failed to Respond" }
		};

		public const byte ErrorMask = 0x80;

		public const byte ReadCoilsFunctionNumber = 0x01;

		public const byte ReadDiscreteInputsFunctionNumber = 0x02;

		public const byte ReadHoldingRegistersFunctionNumber = 0x03;

		public const byte ReadInputRegistersFunctionNumber = 0x04;

		public const byte WriteSingleCoilFunctionNumber = 0x05;

		public const byte WriteSingleRegisterFunctionNumber = 0x06;

		public const byte WriteMultipleCoilsFunctionNumber = 0x0F;

		public const byte WriteMultipleRegistersFunctionNumber = 0x10;

		public const int MinDeviceId = 0x0000;

		public const int MaxDeviceId = 0x00FF; // 255

		public const int MinAddress = 0x0000;

		public const int MaxAddress = 0xFFFF; // 65535

		public const int MinCount = 0x01;

		public const int MaxCoilCountRead = 0x7D0; // 2000

		public const int MaxCoilCountWrite = 0x7B0; // 1968

		public const int MaxRegisterCountRead = 0x7D; // 125

		public const int MaxRegisterCountWrite = 0x7B; // 123
	}
}
