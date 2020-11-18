namespace AMWD.Modbus.Common.Structures
{
	/// <summary>
	/// Represents the contents of a coil on a Modbus device.
	/// </summary>
	public class Coil : ModbusObject
	{
		/// <inheritdoc/>
		public override ObjectType Type => ObjectType.Coil;
	}
}
