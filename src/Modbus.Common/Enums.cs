using System.ComponentModel;

namespace AMWD.Modbus.Common
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
		[Description("Read Coils")]
		ReadCoils = 0x01,
		/// <summary>
		/// Read discrete inputs (Fn 2).
		/// </summary>
		[Description("Read Discrete Inputs")]
		ReadDiscreteInputs = 0x02,
		/// <summary>
		/// Reads holding registers (Fn 3).
		/// </summary>
		[Description("Read Holding Registers")]
		ReadHoldingRegisters = 0x03,
		/// <summary>
		/// Reads input registers (Fn 4).
		/// </summary>
		[Description("Read Input Registers")]
		ReadInputRegisters = 0x04,
		/// <summary>
		/// Writes a single coil (Fn 5).
		/// </summary>
		[Description("Write Single Coil")]
		WriteSingleCoil = 0x05,
		/// <summary>
		/// Writes a single register (Fn 6).
		/// </summary>
		[Description("Write Single Register")]
		WriteSingleRegister = 0x06,
		/// <summary>
		/// Writes multiple coils (Fn 15).
		/// </summary>
		[Description("Write Multiple Coils")]
		WriteMultipleCoils = 0x0F,
		/// <summary>
		/// Writes multiple registers (Fn 16).
		/// </summary>
		[Description("Write Multiple Registers")]
		WriteMultipleRegisters = 0x10
	}

	/// <summary>
	/// Lists the Modbus exception codes.
	/// </summary>
	public enum ErrorCode : byte
	{
		/// <summary>
		/// No error.
		/// </summary>
		[Description("No error")]
		NoError = 0,
		/// <summary>
		/// Function code not valid/supported.
		/// </summary>
		[Description("Illegal function")]
		IllegalFunction = 1,
		/// <summary>
		/// Data address not in range.
		/// </summary>
		[Description("Illegal data address")]
		IllegalDataAddress = 2,
		/// <summary>
		/// The data value to set not valid.
		/// </summary>
		[Description("Illegal data value")]
		IllegalDataValue = 3,
		/// <summary>
		/// Slave device produced a failure.
		/// </summary>
		[Description("Slave device failure")]
		SlaveDeviceFailure = 4,
		/// <summary>
		/// Ack
		/// </summary>
		[Description("Acknowledge")]
		Acknowledge = 5,
		/// <summary>
		/// Slave device is working on another task.
		/// </summary>
		[Description("Slave device busy")]
		SlaveDeviceBusy = 6,
		/// <summary>
		/// nAck
		/// </summary>
		[Description("Negative acknowledge")]
		NegativeAcknowledge = 7,
		/// <summary>
		/// Momory Parity Error.
		/// </summary>
		[Description("Memory parity error")]
		MemoryParityError = 8,
		/// <summary>
		/// Gateway of the device could not be reached.
		/// </summary>
		[Description("Gateway path unavailable")]
		GatewayPath = 10,
		/// <summary>
		/// Gateway device did no resopond.
		/// </summary>
		[Description("Gateway target device failed to respond")]
		GatewayTargetDevice = 11
	}
}