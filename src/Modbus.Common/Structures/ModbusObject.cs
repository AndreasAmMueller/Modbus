using System;

namespace AMWD.Modbus.Common.Structures
{
	/// <summary>
	/// The abstract basis class for all types of modbus register.
	/// </summary>
	public class ModbusObject
	{
		/// <summary>
		/// Gets the explicit type.
		/// </summary>
		public virtual ModbusObjectType Type { get; set; }

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
		public ushort RegisterValue
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

		/// <summary>
		/// Gets or sets a value indicating whether the discrete value is set.
		/// </summary>
		public bool BoolValue
		{
			get
			{
				return HiByte > 0 || LoByte > 0;
			}
			set
			{
				HiByte = 0;
				LoByte = (byte)(value ? 1 : 0);
			}
		}

		#endregion Properties

		#region Overrides

		/// <inheritdoc/>
		public override string ToString()
			=> Type switch
			{
				ModbusObjectType.Coil => $"Coil #{Address} | {BoolValue}",
				ModbusObjectType.DiscreteInput => $"Discrete Input #{Address} | {BoolValue}",
				ModbusObjectType.HoldingRegister => $"Holding Register #{Address} | Hi: {HiByte:X2} Lo: {LoByte:X2} | {RegisterValue}",
				ModbusObjectType.InputRegister => $"Input Register #{Address} | Hi: {HiByte:X2} Lo: {LoByte:X2} | {RegisterValue}",
				_ => base.ToString(),
			};

		/// <inheritdoc/>
		public override bool Equals(object obj)
		{
			if (obj is not ModbusObject register)
				return false;

			return Type == register.Type
				&& Address == register.Address
				&& HiByte == register.HiByte
				&& LoByte == register.LoByte;
		}

		/// <inheritdoc/>
		public override int GetHashCode()
			=> base.GetHashCode() ^
				Address.GetHashCode() ^
				HiByte.GetHashCode() ^
				LoByte.GetHashCode();

		#endregion Overrides
	}
}
