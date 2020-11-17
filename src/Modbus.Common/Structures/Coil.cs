namespace AMWD.Modbus.Common.Structures
{
	/// <summary>
	/// Represents the contents of a coil on a Modbus device.
	/// </summary>
	public class Coil : ModbusDiscrete
	{
		/// <inheritdoc/>
		public override ValueType Type => ValueType.Coil;

		/// <inheritdoc/>
		public override string ToString()
		{
			return $"Coil #{Address} | {Value}";
		}
	}
}
