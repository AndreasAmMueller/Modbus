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
		WriteMultipleRegisters = 0x10,
		/// <summary>
		/// Tunnels service requests and method invocations (Fn 43).
		/// </summary>
		/// <remarks>
		/// This function code needs additional information about its type of request.
		/// </remarks>
		[Description("MODBUS Encapsulated Interface (MEI)")]
		EncapsulatedInterface = 0x2B
	}

	/// <summary>
	/// Lists the possible MEI types.
	/// </summary>
	/// <remarks>
	/// MEI = MODBUS Encapsulated Interface (Fn 43).
	/// </remarks>
	public enum MEIType : byte
	{
		/// <summary>
		/// The request contains data of CANopen
		/// </summary>
		[Description("CANopen General Reference Request and Response PDU")]
		CANOpenGeneralReference = 0x0D,
		/// <summary>
		/// The request contains data to read specific device information.
		/// </summary>
		[Description("Read Device Information")]
		ReadDeviceInformation = 0x0E
	}

	/// <summary>
	/// Lists the category of the device information.
	/// </summary>
	public enum DeviceIDCategory : byte
	{
		/// <summary>
		/// Read the basic information (mandatory).
		/// </summary>
		[Description("Basic Information Block")]
		Basic = 0x01,
		/// <summary>
		/// Read the regular information (optional).
		/// </summary>
		[Description("Regular Information Block")]
		Regular = 0x02,
		/// <summary>
		/// Read the extended information (optional, requires multiple requests).
		/// </summary>
		[Description("Extended Information Block")]
		Extended = 0x03,
		/// <summary>
		/// Read an individual object.
		/// </summary>
		[Description("Individual Object")]
		Individual = 0x04
	}

	/// <summary>
	/// List of known object ids of the device information.
	/// </summary>
	public enum DeviceIDObject : byte
	{
		/// <summary>
		/// The vendor name (mandatory).
		/// </summary>
		VendorName = 0x00,
		/// <summary>
		/// The product code (mandatory).
		/// </summary>
		ProductCode = 0x01,
		/// <summary>
		/// The major and minor revision (mandatory).
		/// </summary>
		MajorMinorRevision = 0x02,

		/// <summary>
		/// The vendor url (optional).
		/// </summary>
		VendorUrl = 0x03,
		/// <summary>
		/// The product name (optional).
		/// </summary>
		ProductName = 0x04,
		/// <summary>
		/// The model name (optional).
		/// </summary>
		ModelName = 0x05,
		/// <summary>
		/// The application name (optional).
		/// </summary>
		UserApplicationName = 0x06
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
		/// The data value to set is not valid.
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

	/// <summary>
	/// Defines the specific type.
	/// </summary>
	public enum ObjectType
	{
		/// <summary>
		/// The type is unknown (should not happen).
		/// </summary>
		Unknown,
		/// <summary>
		/// The discrete value is a coil (read/write).
		/// </summary>
		Coil,
		/// <summary>
		/// The discrete value is an input (read only).
		/// </summary>
		DiscreteInput,
		/// <summary>
		/// The value is an holding register (read/write).
		/// </summary>
		HoldingRegister,
		/// <summary>
		/// The value is an input register (read only).
		/// </summary>
		InputRegister
	}
}
