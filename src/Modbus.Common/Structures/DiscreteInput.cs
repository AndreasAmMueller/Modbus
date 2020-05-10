namespace AMWD.Modbus.Common.Structures
{
	/// <summary>
	/// Represents the contents of a discrete input on a Modbus device.
	/// </summary>
	public class DiscreteInput
	{
		/// <summary>
		/// Gets or sets the address.
		/// </summary>
		public ushort Address { get; set; }

		/// <summary>
		/// Gets or sets a value indicating the status of the discrete input.
		/// </summary>
		public bool Value { get; set; }

		/// <inheritdoc/>
		public override string ToString()
		{
			return $"DiscreteInput#{Address} | {Value}";
		}

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
			if (!(obj is DiscreteInput input))
				return false;

			return Address == input.Address &&
				Value == input.Value;
		}
	}
}
