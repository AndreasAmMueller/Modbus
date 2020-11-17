namespace AMWD.Modbus.Common.Structures
{
	/// <summary>
	/// Represents the contents of a discrete input on a Modbus device.
	/// </summary>
	public class DiscreteInput : ModbusDiscrete
	{
		/// <inheritdoc/>
		public override ValueType Type => ValueType.DiscreteInput;


		/// <inheritdoc/>
		public override string ToString()
		{
			return $"Discrete Input #{Address} | {Value}";
		}
	}
}
