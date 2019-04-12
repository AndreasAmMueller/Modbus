using System;
using System.Runtime.InteropServices;

namespace AMWD.Modbus.Serial.Util
{
	/// <summary>
	/// Represents the structure of the driver settings for RS485.
	/// </summary>
	[StructLayout(LayoutKind.Sequential, Size = 32)]
	internal struct SerialRS485
	{
		/// <summary>
		/// The flags to change the driver state.
		/// </summary>
		public RS485Flags Flags;

		/// <summary>
		/// The delay in milliseconds before send.
		/// </summary>
		public uint RtsDelayBeforeSend;

		/// <summary>
		/// The delay in milliseconds after send.
		/// </summary>
		public uint RtsDelayAfterSend;
	}

	/// <summary>
	/// The flags for the driver state.
	/// </summary>
	[Flags]
	internal enum RS485Flags : uint
	{
		/// <summary>
		/// RS485 is enabled.
		/// </summary>
		Enabled = 1,
		/// <summary>
		/// RS485 uses RTS on send.
		/// </summary>
		RtsOnSend = 2,
		/// <summary>
		/// RS485 uses RTS after send.
		/// </summary>
		RtsAfterSend = 4,
		/// <summary>
		/// Receive during send (duplex).
		/// </summary>
		RxDuringTx = 16
	}
}
