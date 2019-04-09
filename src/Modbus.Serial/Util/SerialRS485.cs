using System;
using System.Runtime.InteropServices;

namespace AMWD.Modbus.Serial.Util
{
	[StructLayout(LayoutKind.Sequential, Size = 32)]
	internal struct SerialRS485
	{
		public RS485Flags Flags;

		public uint RtsDelayBeforeSend;

		public uint RtsDelayAfterSend;
	}

	[Flags]
	internal enum RS485Flags : uint
	{
		SerRS485Enabled = 1,
		SerRS485RtsOnSend = 2,
		SerRS485RtsAfterSend = 4,
		SerRS485RxDuringTx = 16
	}
}
