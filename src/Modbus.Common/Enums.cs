namespace Modbus.Common
{
	/// <summary>
	/// Lists the Modbus request types.
	/// </summary>
	public enum MessageType
	{
		/// <summary>
		/// The type is not set.
		/// </summary>
		Unset,
		/// <summary>
		/// The request reads data.
		/// </summary>
		Read,
		/// <summary>
		/// The request writes one data set.
		/// </summary>
		WriteSingle,
		/// <summary>
		/// The request writes multiple data sets.
		/// </summary>
		WriteMultiple
	}

	/// <summary>
	/// Lists the Modbus function codes.
	/// </summary>
	public enum FunctionCode : byte
	{
		/// <summary>
		/// Read coils (Fn 1).
		/// </summary>
		ReadCoils = 0x01,
		/// <summary>
		/// Read discrete inputs (Fn 2).
		/// </summary>
		ReadDiscreteInputs = 0x02,
		/// <summary>
		/// Reads holding registers (Fn 3).
		/// </summary>
		ReadHoldingRegisters = 0x03,
		/// <summary>
		/// Reads input registers (Fn 4).
		/// </summary>
		ReadInputRegisters = 0x04,
		/// <summary>
		/// Writes a single coil (Fn 5).
		/// </summary>
		WriteSingleCoil = 0x05,
		/// <summary>
		/// Writes a single register (Fn 6).
		/// </summary>
		WriteSingleRegister = 0x06,
		/// <summary>
		/// Writes multiple coils (Fn 15).
		/// </summary>
		WriteMultipleCoils = 0x0F,
		/// <summary>
		/// Writes multiple registers (Fn 16).
		/// </summary>
		WriteMultipleRegisters = 0x10
	}
}
