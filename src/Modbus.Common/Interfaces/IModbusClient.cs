using AMWD.Modbus.Common.Structures;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AMWD.Modbus.Common.Interfaces
{
	/// <summary>
	/// Represents the interface for a Modbus client.
	/// </summary>
	public interface IModbusClient : IDisposable
	{
		#region Properties

		/// <summary>
		/// Gets the result of the asynchronous initialization of this instance.
		/// </summary>
		Task Initialization { get; }

		/// <summary>
		/// Gets a value indicating whether the connection is established.
		/// </summary>
		bool IsConnected { get; }

		/// <summary>
		/// Gets or sets the max reconnect timespan until the reconnect is aborted.
		/// </summary>
		TimeSpan ReconnectTimeSpan { get; set; }

		/// <summary>
		/// Gets or sets the send timeout in milliseconds. Default: 1000.
		/// </summary>
		int SendTimeout { get; set; }

		/// <summary>
		/// Gets ors sets the receive timeout in milliseconds. Default: 1000;
		/// </summary>
		int ReceiveTimeout { get; set; }

		#endregion Properties

		#region Read methods

		/// <summary>
		/// Reads one or more coils of a device. (Modbus function 1).
		/// </summary>
		/// <param name="deviceId">The id to address the device (slave).</param>
		/// <param name="startAddress">The first coil number to read.</param>
		/// <param name="count">The number of coils to read.</param>
		/// <returns>A list of coils or null on error.</returns>
		Task<List<Coil>> ReadCoils(byte deviceId, ushort startAddress, ushort count);

		/// <summary>
		/// Reads one or more discrete inputs of a device. (Modbus function 2).
		/// </summary>
		/// <param name="deviceId">The id to address the device (slave).</param>
		/// <param name="startAddress">The first discrete input number to read.</param>
		/// <param name="count">The number of discrete inputs to read.</param>
		/// <returns>A list of discrete inputs or null on error.</returns>
		Task<List<DiscreteInput>> ReadDiscreteInputs(byte deviceId, ushort startAddress, ushort count);

		/// <summary>
		/// Reads one or more holding registers of a device. (Modbus function 3).
		/// </summary>
		/// <param name="deviceId">The id to address the device (slave).</param>
		/// <param name="startAddress">The first register number to read.</param>
		/// <param name="count">The number of registers to read.</param>
		/// <returns>A list of registers or null on error.</returns>
		Task<List<Register>> ReadHoldingRegisters(byte deviceId, ushort startAddress, ushort count);

		/// <summary>
		/// Reads one or more input registers of a device. (Modbus function 4).
		/// </summary>
		/// <param name="deviceId">The id to address the device (slave).</param>
		/// <param name="startAddress">The first register number to read.</param>
		/// <param name="count">The number of registers to read.</param>
		/// <returns>A list of registers or null on error.</returns>
		Task<List<Register>> ReadInputRegisters(byte deviceId, ushort startAddress, ushort count);

		#endregion Read methods

		#region Write methods

		/// <summary>
		/// Writes a single coil status to the Modbus device. (Modbus function 5)
		/// </summary>
		/// <param name="deviceId">The id to address the device (slave).</param>
		/// <param name="coil">The coil to write.</param>
		/// <returns>true on success, otherwise false.</returns>
		Task<bool> WriteSingleCoil(byte deviceId, Coil coil);

		/// <summary>
		/// Writes a single register to the Modbus device. (Modbus function 6)
		/// </summary>
		/// <param name="deviceId">The id to address the device (slave).</param>
		/// <param name="register">The register to write.</param>
		/// <returns>true on success, otherwise false.</returns>
		Task<bool> WriteSingleRegister(byte deviceId, Register register);

		/// <summary>
		/// Writes multiple coil status to the Modbus device. (Modbus function 15)
		/// </summary>
		/// <param name="deviceId">The id to address the device (slave).</param>
		/// <param name="coils">A list of coils to write.</param>
		/// <returns>true on success, otherwise false.</returns>
		Task<bool> WriteCoils(byte deviceId, IEnumerable<Coil> coils);

		/// <summary>
		/// Writes multiple registers to the Modbus device. (Modbus function 16)
		/// </summary>
		/// <param name="deviceId">The id to address the device (slave).</param>
		/// <param name="registers">A list of registers to write.</param>
		/// <returns>true on success, otherwise false.</returns>
		Task<bool> WriteRegisters(byte deviceId, IEnumerable<Register> registers);

		#endregion Write methods
	}
}
