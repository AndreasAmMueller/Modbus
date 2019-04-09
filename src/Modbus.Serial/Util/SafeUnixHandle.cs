﻿using System;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Security.Permissions;

namespace AMWD.Modbus.Serial.Util
{
	[SecurityPermission(SecurityAction.InheritanceDemand, UnmanagedCode = true)]
	[SecurityPermission(SecurityAction.Demand, UnmanagedCode = true)]
	internal sealed class SafeUnixHandle : SafeHandle
	{
		[ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
		private SafeUnixHandle()
			: base(new IntPtr(-1), true)
		{ }

		public override bool IsInvalid
		{
			get { return handle == new IntPtr(-1); }
		}

		[ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
		protected override bool ReleaseHandle()
		{
			return UnsafeNativeMethods.Close(handle) != -1;
		}
	}
}
