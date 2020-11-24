using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AMWD.Modbus.Common.Structures;

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
		Task ConnectingTask { get; }

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
		TimeSpan SendTimeout { get; set; }

		/// <summary>
		/// Gets ors sets the receive timeout in milliseconds. Default: 1000;
		/// </summary>
		TimeSpan ReceiveTimeout { get; set; }

		#endregion Properties

		#region Control

		/// <summary>
		/// Connects the client to the server.
		/// </summary>
		/// <returns>An awaitable task.</returns>
		Task Connect();

		/// <summary>
		/// Disconnects the client.
		/// </summary>
		/// <returns>An awaitable task.</returns>
		Task Disconnect();

		#endregion Control

		#region Read methods

		/// <summary>
		/// Reads one or more coils of a device. (Modbus function 1).
		/// </summary>
		/// <param name="deviceId">The id to address the device (slave).</param>
		/// <param name="startAddress">The first coil number to read.</param>
		/// <param name="count">The number of coils to read.</param>
		/// <param name="cancellationToken">A cancellation token to abort the action.</param>
		/// <returns>A list of coils or null on error.</returns>
		Task<List<Coil>> ReadCoils(byte deviceId, ushort startAddress, ushort count, CancellationToken cancellationToken = default);

		/// <summary>
		/// Reads one or more discrete inputs of a device. (Modbus function 2).
		/// </summary>
		/// <param name="deviceId">The id to address the device (slave).</param>
		/// <param name="startAddress">The first discrete input number to read.</param>
		/// <param name="count">The number of discrete inputs to read.</param>
		/// <param name="cancellationToken">A cancellation token to abort the action.</param>
		/// <returns>A list of discrete inputs or null on error.</returns>
		Task<List<DiscreteInput>> ReadDiscreteInputs(byte deviceId, ushort startAddress, ushort count, CancellationToken cancellationToken = default);

		/// <summary>
		/// Reads one or more holding registers of a device. (Modbus function 3).
		/// </summary>
		/// <param name="deviceId">The id to address the device (slave).</param>
		/// <param name="startAddress">The first register number to read.</param>
		/// <param name="count">The number of registers to read.</param>
		/// <param name="cancellationToken">A cancellation token to abort the action.</param>
		/// <returns>A list of registers or null on error.</returns>
		Task<List<Register>> ReadHoldingRegisters(byte deviceId, ushort startAddress, ushort count, CancellationToken cancellationToken = default);

		/// <summary>
		/// Reads one or more input registers of a device. (Modbus function 4).
		/// </summary>
		/// <param name="deviceId">The id to address the device (slave).</param>
		/// <param name="startAddress">The first register number to read.</param>
		/// <param name="count">The number of registers to read.</param>
		/// <param name="cancellationToken">A cancellation token to abort the action.</param>
		/// <returns>A list of registers or null on error.</returns>
		Task<List<Register>> ReadInputRegisters(byte deviceId, ushort startAddress, ushort count, CancellationToken cancellationToken = default);

		/// <summary>
		/// Reads device information. (Modbus function 43).
		/// </summary>
		/// <param name="deviceId">The id to address the device (slave).</param>
		/// <param name="categoryId">The category to read (basic, regular, extended, individual).</param>
		/// <param name="objectId">The first object id to read.</param>
		/// <param name="cancellationToken">A cancellation token to abort the action.</param>
		/// <returns>A map of device information and their content as string.</returns>
		Task<Dictionary<DeviceIDObject, string>> ReadDeviceInformation(byte deviceId, DeviceIDCategory categoryId, DeviceIDObject objectId = DeviceIDObject.VendorName, CancellationToken cancellationToken = default);

		/// <summary>
		/// Reads device information. (Modbus function 43).
		/// </summary>
		/// <param name="deviceId">The id to address the device (slave).</param>
		/// <param name="categoryId">The category to read (basic, regular, extended, individual).</param>
		/// <param name="objectId">The first object id to read.</param>
		/// <param name="cancellationToken">A cancellation token to abort the action.</param>
		/// <returns>A map of device information and their content as raw bytes.</returns>
		Task<Dictionary<byte, byte[]>> ReadDeviceInformationRaw(byte deviceId, DeviceIDCategory categoryId, DeviceIDObject objectId = DeviceIDObject.VendorName, CancellationToken cancellationToken = default);

		#endregion Read methods

		#region Write methods

		/// <summary>
		/// Writes a single coil status to the Modbus device. (Modbus function 5)
		/// </summary>
		/// <param name="deviceId">The id to address the device (slave).</param>
		/// <param name="coil">The coil to write.</param>
		/// <param name="cancellationToken">A cancellation token to abort the action.</param>
		/// <returns>true on success, otherwise false.</returns>
		Task<bool> WriteSingleCoil(byte deviceId, ModbusObject coil, CancellationToken cancellationToken = default);

		/// <summary>
		/// Writes a single holding register to the Modbus device. (Modbus function 6)
		/// </summary>
		/// <param name="deviceId">The id to address the device (slave).</param>
		/// <param name="register">The register to write.</param>
		/// <param name="cancellationToken">A cancellation token to abort the action.</param>
		/// <returns>true on success, otherwise false.</returns>
		Task<bool> WriteSingleRegister(byte deviceId, ModbusObject register, CancellationToken cancellationToken = default);

		/// <summary>
		/// Writes multiple coil status to the Modbus device. (Modbus function 15)
		/// </summary>
		/// <param name="deviceId">The id to address the device (slave).</param>
		/// <param name="coils">A list of coils to write.</param>
		/// <param name="cancellationToken">A cancellation token to abort the action.</param>
		/// <returns>true on success, otherwise false.</returns>
		Task<bool> WriteCoils(byte deviceId, IEnumerable<ModbusObject> coils, CancellationToken cancellationToken = default);

		/// <summary>
		/// Writes multiple holding registers to the Modbus device. (Modbus function 16)
		/// </summary>
		/// <param name="deviceId">The id to address the device (slave).</param>
		/// <param name="registers">A list of registers to write.</param>
		/// <param name="cancellationToken">A cancellation token to abort the action.</param>
		/// <returns>true on success, otherwise false.</returns>
		Task<bool> WriteRegisters(byte deviceId, IEnumerable<ModbusObject> registers, CancellationToken cancellationToken = default);

		#endregion Write methods
	}
}
