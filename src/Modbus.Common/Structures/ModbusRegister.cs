using System;

namespace AMWD.Modbus.Common.Structures
{
	/// <summary>
	/// The abstract basis class for all types of modbus register.
	/// </summary>
	public abstract class ModbusRegister
	{
		/// <summary>
		/// Gets the explicit type.
		/// </summary>
		public abstract ValueType Type { get; }

		#region Properties

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
				byte[] blob = new[] { HiByte, LoByte };
				if (BitConverter.IsLittleEndian)
					Array.Reverse(blob);

				return BitConverter.ToUInt16(blob, 0);
			}
			set
			{
				byte[] blob = BitConverter.GetBytes(value);
				if (BitConverter.IsLittleEndian)
					Array.Reverse(blob);

				HiByte = blob[0];
				LoByte = blob[1];
			}
		}

		#endregion Properties

		#region Overrides

		/// <inheritdoc/>
		public override bool Equals(object obj)
		{
			if (!(obj is ModbusRegister register))
				return false;

			return Type == register.Type
				&& Address == register.Address
				&& HiByte == register.HiByte
				&& LoByte == register.LoByte;
		}

		/// <inheritdoc/>
		public override int GetHashCode()
		{
			return base.GetHashCode() ^
				Address.GetHashCode() ^
				HiByte.GetHashCode() ^
				LoByte.GetHashCode();
		}

		#endregion Overrides
	}
}
