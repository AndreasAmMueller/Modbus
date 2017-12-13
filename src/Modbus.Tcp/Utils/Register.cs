using System;

namespace Modbus.Tcp.Utils
{
	/// <summary>
	/// Represents a register on a Modbus device.
	/// </summary>
	public class Register
	{
		/// <summary>
		/// Gets or sets the address.
		/// </summary>
		public ushort Address { get; set; }

		/// <summary>
		/// Gets or sets the High-Byte of the register.
		/// </summary>
		public byte HiByte { get; set; }

		/// <summary>
		/// Gets or sets the Low-Byte of the register.
		/// </summary>
		public byte LoByte { get; set; }

		/// <summary>
		/// Gets or sets the value of the register as WORD.
		/// </summary>
		public ushort Value
		{
			get
			{
				var blob = new[] { HiByte, LoByte };
				if (BitConverter.IsLittleEndian)
				{
					Array.Reverse(blob);
				}
				return BitConverter.ToUInt16(blob, 0);
			}
			set
			{
				var blob = BitConverter.GetBytes(value);
				if (BitConverter.IsLittleEndian)
				{
					Array.Reverse(blob);
				}
				HiByte = blob[0];
				LoByte = blob[1];
			}
		}

		/// <inheritdoc/>
		public override string ToString()
		{
			return $"Register#{Address} | Hi: {HiByte.ToString("X2")} Lo: {LoByte.ToString("X2")} | {Value}";
		}
	}
}
