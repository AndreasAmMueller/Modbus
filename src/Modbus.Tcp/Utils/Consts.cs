using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Modbus.Tcp.Utils
{
	internal class Consts
	{
		#region Error/Exception

		private static Dictionary<byte, string> exceptions = new Dictionary<byte, string>
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

		public static ReadOnlyDictionary<byte, string> ErrorMessages => new ReadOnlyDictionary<byte, string>(exceptions);

		public const byte ErrorMask = 0x80;

		#endregion Error/Exception

		#region Protocol limitations

		public const int MinDeviceId = 0x0000;

		public const int MaxDeviceId = 0x00FF; // 255

		public const int MinAddress = 0x0000;

		public const int MaxAddress = 0xFFFF; // 65535

		public const int MinCount = 0x01;

		public const int MaxCoilCountRead = 0x7D0; // 2000

		public const int MaxCoilCountWrite = 0x7B0; // 1968

		public const int MaxRegisterCountRead = 0x7D; // 125

		public const int MaxRegisterCountWrite = 0x7B; // 123

		#endregion Protocol limitations
	}
}
