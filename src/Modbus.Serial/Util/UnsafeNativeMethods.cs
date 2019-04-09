using System;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Text;

namespace AMWD.Modbus.Serial.Util
{
	internal static class UnsafeNativeMethods
	{
		internal const int O_RDWR = 2;
		internal const int O_NOCTTY = 256;
		internal const uint TIOCGRS485 = 0x542E;
		internal const uint TIOCSRS485 = 0x542F;

		[ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
		[DllImport("libc", EntryPoint = "close", SetLastError = true)]
		internal static extern int Close(IntPtr handle);

		[DllImport("libc", EntryPoint = "ioctl", SetLastError = true)]
		internal static extern int IoCtl(SafeUnixHandle handle, uint request, ref SerialRS485 serialRs485);

		[DllImport("libc", EntryPoint = "open", SetLastError = true)]
		internal static extern SafeUnixHandle Open(string path, uint flag);

		[DllImport("libc", EntryPoint = "strerr", SetLastError = true)]
		internal static extern int StrError(int error, [Out] StringBuilder buffer, ulong bufferLength);
	}
}
