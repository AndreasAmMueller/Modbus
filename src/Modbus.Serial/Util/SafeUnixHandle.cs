using System;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Security.Permissions;

namespace AMWD.Modbus.Serial.Util
{
	/// <summary>
	/// Implements a safe handle for unix systems.
	/// Found on https://stackoverflow.com/a/10388107
	/// </summary>
	[SecurityPermission(SecurityAction.InheritanceDemand, UnmanagedCode = true)]
	[SecurityPermission(SecurityAction.Demand, UnmanagedCode = true)]
	internal sealed class SafeUnixHandle : SafeHandle
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="SafeUnixHandle"/> class.
		/// </summary>
		[ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
		private SafeUnixHandle()
			: base(new IntPtr(-1), true)
		{ }

		/// <inheritdoc/>
		public override bool IsInvalid
		{
			get { return handle == new IntPtr(-1); }
		}

		/// <inheritdoc/>
		[ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
		protected override bool ReleaseHandle()
		{
			return UnsafeNativeMethods.Close(handle) != -1;
		}
	}
}
