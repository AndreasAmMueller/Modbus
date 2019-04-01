namespace AMWD.Modbus.Common
{
	/// <summary>
	/// Contains all constants used in Modbus.
	/// </summary>
	public static class Consts
	{
		#region Error/Exception

		/// <summary>
		/// The Bit-Mask to filter the error-state of a Modbus response.
		/// </summary>
		public const byte ErrorMask = 0x80;

		#endregion Error/Exception

		#region Protocol limitations

		/// <summary>
		/// The lowest accepted device id on TCP protocol.
		/// </summary>
		public const byte MinDeviceIdTcp = 0x00;

		/// <summary>
		/// The lowest accepted device id on RTU protocol.
		/// </summary>
		public const byte MinDeviceIdRtu = 0x01;

		/// <summary>
		/// The highest accepted device id.
		/// </summary>
		public const byte MaxDeviceId = 0xFF; // 255

		/// <summary>
		/// The lowest address.
		/// </summary>
		public const ushort MinAddress = 0x0000;

		/// <summary>
		/// The highest address.
		/// </summary>
		public const ushort MaxAddress = 0xFFFF; // 65535

		/// <summary>
		/// The lowest number of requested data sets.
		/// </summary>
		public const ushort MinCount = 0x01;

		/// <summary>
		/// The highest number of requested coils to read.
		/// </summary>
		public const ushort MaxCoilCountRead = 0x7D0; // 2000

		/// <summary>
		/// The highest number of requested coils to write.
		/// </summary>
		public const ushort MaxCoilCountWrite = 0x7B0; // 1968

		/// <summary>
		/// The highest number of requested registers to read.
		/// </summary>
		public const ushort MaxRegisterCountRead = 0x7D; // 125

		/// <summary>
		/// The highest number of requested registers to write.
		/// </summary>
		public const ushort MaxRegisterCountWrite = 0x7B; // 123

		#endregion Protocol limitations
	}
}
