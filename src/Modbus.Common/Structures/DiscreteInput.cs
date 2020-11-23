namespace AMWD.Modbus.Common.Structures
{
	/// <summary>
	/// Represents the contents of a discrete input on a Modbus device.
	/// </summary>
	public class DiscreteInput : ModbusObject
	{
		/// <inheritdoc/>
		public override ModbusObjectType Type => ModbusObjectType.DiscreteInput;
	}
}
