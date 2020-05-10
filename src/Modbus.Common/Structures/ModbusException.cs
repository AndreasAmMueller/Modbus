using System;
using AMWD.Modbus.Common.Util;

namespace AMWD.Modbus.Common.Structures
{
	/// <summary>
	/// Represents errors that occurr during Modbus requests.
	/// </summary>
	public class ModbusException : Exception
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="ModbusException"/> class.
		/// </summary>
		public ModbusException()
			: base()
		{ }

		/// <summary>
		/// Initializes a new instance of the <see cref="ModbusException"/> class
		/// with a specified error message.
		/// </summary>
		/// <param name="message">The specified error message.</param>
		public ModbusException(string message)
			: base(message)
		{ }

		/// <summary>
		/// Initializes a new instance of the <see cref="ModbusException"/> class
		/// with a specified error message and a reference to the inner exception that is the cause of this exception.
		/// </summary>
		/// <param name="message">The specified error message.</param>
		/// <param name="innerException">The inner exception.</param>
		public ModbusException(string message, Exception innerException)
			: base(message, innerException)
		{ }

		/// <summary>
		/// Gets or sets the error/exception code.
		/// </summary>
		public ErrorCode ErrorCode { get; set; }

		/// <summary>
		/// Gets the error message.
		/// </summary>
		public string ErrorMessage => ErrorCode.GetDescription();
	}
}
