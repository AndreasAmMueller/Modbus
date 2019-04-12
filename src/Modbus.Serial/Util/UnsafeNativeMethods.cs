using System;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;

namespace AMWD.Modbus.Serial.Util
{
	/// <summary>
	/// Definitions of the unsafe system methods.
	/// Found on https://stackoverflow.com/a/10388107
	/// </summary>
	internal static class UnsafeNativeMethods
	{
		/// <summary>
		/// A flag for <see cref="Open(string, uint)"/>.
		/// </summary>
		internal const int O_RDWR = 2;
		/// <summary>
		/// A flag for <see cref="Open(string, uint)"/>.
		/// </summary>
		internal const int O_NOCTTY = 256;
		/// <summary>
		/// A flag for <see cref="IoCtl(SafeUnixHandle, uint, ref SerialRS485)"/>.
		/// </summary>
		internal const uint TIOCGRS485 = 0x542E;
		/// <summary>
		/// A flag for <see cref="IoCtl(SafeUnixHandle, uint, ref SerialRS485)"/>.
		/// </summary>
		internal const uint TIOCSRS485 = 0x542F;

		/// <summary>
		/// Opens a handle to a defined path (serial port).
		/// </summary>
		/// <param name="path">The path to open the handle.</param>
		/// <param name="flag">The flags for the handle.</param>
		/// <returns></returns>
		[DllImport("libc", EntryPoint = "open", SetLastError = true)]
		internal static extern SafeUnixHandle Open(string path, uint flag);

		/// <summary>
		/// Performs an ioctl request to the open handle.
		/// </summary>
		/// <param name="handle">The handle.</param>
		/// <param name="request">The request.</param>
		/// <param name="serialRs485">The data structure to read / write.</param>
		/// <returns></returns>
		[DllImport("libc", EntryPoint = "ioctl", SetLastError = true)]
		internal static extern int IoCtl(SafeUnixHandle handle, uint request, ref SerialRS485 serialRs485);

		/// <summary>
		/// Closes an open handle.
		/// </summary>
		/// <param name="handle">The handle.</param>
		/// <returns></returns>
		[ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
		[DllImport("libc", EntryPoint = "close", SetLastError = true)]
		internal static extern int Close(IntPtr handle);

		/// <summary>
		/// Converts the given error number (errno) into a readable string.
		/// </summary>
		/// <param name="errno">The error number (errno).</param>
		/// <returns></returns>
		[DllImport("libc", EntryPoint = "strerror", SetLastError = true, CallingConvention = CallingConvention.Cdecl)]
		internal static extern IntPtr StrError(int errno);
	}
}
