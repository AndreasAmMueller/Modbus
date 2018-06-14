namespace AMWD.Modbus.Common.Structures
{
	/// <summary>
	/// Represents the contents of a coil on a Modbus device.
	/// </summary>
	public class Coil
	{
		/// <summary>
		/// Gets or sets the address.
		/// </summary>
		public ushort Address { get; set; }

		/// <summary>
		/// Gets or sets a value indicating the status of the coil.
		/// </summary>
		public bool Value { get; set; }

		/// <inheritdoc/>
		public override string ToString()
		{
			return $"Coil#{Address} | {Value}";
		}
	}
}
