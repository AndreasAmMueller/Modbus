namespace AMWD.Modbus.Common.Structures
{
	/// <summary>
	/// The abstract basis class for all types of discrete values.
	/// </summary>
	public abstract class ModbusDiscrete
	{
		/// <summary>
		/// Gets the explicit type.
		/// </summary>
		public abstract ValueType Type { get; }

		/// <summary>
		/// Gets or sets the address.
		/// </summary>
		public ushort Address { get; set; }

		/// <summary>
		/// Gets or sets a value indicating the status of the coil.
		/// </summary>
		public bool Value { get; set; }

		/// <inheritdoc/>
		public override int GetHashCode()
		{
			return base.GetHashCode() ^
				Address.GetHashCode() ^
				Value.GetHashCode();
		}

		/// <inheritdoc/>
		public override bool Equals(object obj)
		{
			if (!(obj is ModbusDiscrete discrete))
				return false;

			return Type == discrete.Type
				&& Address == discrete.Address
				&& Value == discrete.Value;
		}
	}
}
