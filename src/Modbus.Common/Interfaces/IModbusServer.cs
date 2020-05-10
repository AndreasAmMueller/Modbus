using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AMWD.Modbus.Common.Structures;

namespace AMWD.Modbus.Common.Interfaces
{
	/// <summary>
	/// Represents the interface of a Modbus server.
	/// </summary>
	public interface IModbusServer : IDisposable
	{
		#region Events

		/// <summary>
		/// Raised when a coil was written.
		/// </summary>
		event EventHandler<WriteEventArgs> CoilWritten;

		/// <summary>
		/// Raised when a register was written.
		/// </summary>
		event EventHandler<WriteEventArgs> RegisterWritten;

		#endregion Events

		#region Properties

		/// <summary>
		/// Gets the result of the asynchronous initialization of this instance.
		/// </summary>
		Task Initialization { get; }

		/// <summary>
		/// Gets the UTC timestamp of the server start.
		/// </summary>
		DateTime StartTime { get; }

		/// <summary>
		/// Gets a value indicating whether the server is running.
		/// </summary>
		bool IsRunning { get; }

		/// <summary>
		/// Gets a list of device ids the server handles.
		/// </summary>
		List<byte> DeviceIds { get; }

		#endregion Properties

		#region Public methods

		//void Start();

		//void Stop();

		#region Coils

		/// <summary>
		/// Returns a coil of a device.
		/// </summary>
		/// <param name="deviceId">The device id.</param>
		/// <param name="coilNumber">The address of the coil.</param>
		/// <returns>The coil.</returns>
		Coil GetCoil(byte deviceId, ushort coilNumber);

		/// <summary>
		/// Sets the status of a coild to a device.
		/// </summary>
		/// <param name="deviceId">The device id.</param>
		/// <param name="coilNumber">The address of the coil.</param>
		/// <param name="value">The status of the coil.</param>
		void SetCoil(byte deviceId, ushort coilNumber, bool value);

		/// <summary>
		/// Sets the status of a coild to a device.
		/// </summary>
		/// <param name="deviceId">The device id.</param>
		/// <param name="coil">The coil.</param>
		void SetCoil(byte deviceId, Coil coil);

		#endregion Coils

		#region Discrete Inputs

		/// <summary>
		/// Returns a discrete input of a device.
		/// </summary>
		/// <param name="deviceId">The device id.</param>
		/// <param name="inputNumber">The discrete input address.</param>
		/// <returns>The discrete input.</returns>
		DiscreteInput GetDiscreteInput(byte deviceId, ushort inputNumber);

		/// <summary>
		/// Sets a discrete input of a device.
		/// </summary>
		/// <param name="deviceId">The device id.</param>
		/// <param name="inputNumber">The discrete input address.</param>
		/// <param name="value">A value inidcating whether the input is set.</param>
		void SetDiscreteInput(byte deviceId, ushort inputNumber, bool value);

		/// <summary>
		/// Sets a discrete input of a device.
		/// </summary>
		/// <param name="deviceId">The device id.</param>
		/// <param name="discreteInput">The discrete input to set.</param>
		void SetDiscreteInput(byte deviceId, DiscreteInput discreteInput);

		#endregion Discrete Inputs

		#region Input Registers

		/// <summary>
		/// Returns an input register of a device.
		/// </summary>
		/// <param name="deviceId">The device id.</param>
		/// <param name="registerNumber">The input register address.</param>
		/// <returns>The input register.</returns>
		Register GetInputRegister(byte deviceId, ushort registerNumber);

		/// <summary>
		/// Sets an input register of a device.
		/// </summary>
		/// <param name="deviceId">The device id.</param>
		/// <param name="registerNumber">The input register address.</param>
		/// <param name="value">The register value.</param>
		void SetInputRegister(byte deviceId, ushort registerNumber, ushort value);

		/// <summary>
		/// Sets an input register of a device.
		/// </summary>
		/// <param name="deviceId">The device id.</param>
		/// <param name="registerNumber">The input register address.</param>
		/// <param name="highByte">The High-Byte value.</param>
		/// <param name="lowByte">The Low-Byte value.</param>
		void SetInputRegister(byte deviceId, ushort registerNumber, byte highByte, byte lowByte);

		/// <summary>
		/// Sets an input register of a device.
		/// </summary>
		/// <param name="deviceId">The device id.</param>
		/// <param name="register">The input register.</param>
		void SetInputRegister(byte deviceId, Register register);

		#endregion Input Registers

		#region Holding Registers

		/// <summary>
		/// Returns a holding register of a device.
		/// </summary>
		/// <param name="deviceId">The device id.</param>
		/// <param name="registerNumber">The holding register address.</param>
		/// <returns>The holding register.</returns>
		Register GetHoldingRegister(byte deviceId, ushort registerNumber);

		/// <summary>
		/// Sets a holding register of a device.
		/// </summary>
		/// <param name="deviceId">The device id.</param>
		/// <param name="registerNumber">The holding register address.</param>
		/// <param name="value">The register value.</param>
		void SetHoldingRegister(byte deviceId, ushort registerNumber, ushort value);

		/// <summary>
		/// Sets a holding register of a device.
		/// </summary>
		/// <param name="deviceId">The device id.</param>
		/// <param name="registerNumber">The holding register address.</param>
		/// <param name="highByte">The high byte value.</param>
		/// <param name="lowByte">The low byte value.</param>
		void SetHoldingRegister(byte deviceId, ushort registerNumber, byte highByte, byte lowByte);

		/// <summary>
		/// Sets a holding register of a device.
		/// </summary>
		/// <param name="deviceId">The device id.</param>
		/// <param name="register">The register.</param>
		void SetHoldingRegister(byte deviceId, Register register);

		#endregion Holding Registers

		#region Devices

		/// <summary>
		/// Adds a new device to the server.
		/// </summary>
		/// <param name="deviceId">The id of the new device.</param>
		/// <returns>true on success, otherwise false.</returns>
		bool AddDevice(byte deviceId);

		/// <summary>
		/// Removes a device from the server.
		/// </summary>
		/// <param name="deviceId">The device id to remove.</param>
		/// <returns>true on success, otherwise false.</returns>
		bool RemoveDevice(byte deviceId);

		#endregion Devices

		#endregion Public methods
	}

	/// <summary>
	/// Provides information of the write action.
	/// </summary>
	public class WriteEventArgs : EventArgs
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="WriteEventArgs"/> class using a single coil.
		/// </summary>
		/// <param name="deviceId">The device id.</param>
		/// <param name="coil">The coil.</param>
		public WriteEventArgs(byte deviceId, Coil coil)
		{
			Coils = new List<Coil> { coil };
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="WriteEventArgs"/> class using a list of coils.
		/// </summary>
		/// <param name="deviceId">The device id.</param>
		/// <param name="coils">A list of coils.</param>
		public WriteEventArgs(byte deviceId, List<Coil> coils)
		{
			Coils = coils;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="WriteEventArgs"/> class using a single register.
		/// </summary>
		/// <param name="deviceId">The device id.</param>
		/// <param name="register">The register.</param>
		public WriteEventArgs(byte deviceId, Register register)
		{
			Registers = new List<Register> { register };
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="WriteEventArgs"/> class using a list of registers.
		/// </summary>
		/// <param name="deviceId">The device id.</param>
		/// <param name="registers">A list of registers.</param>
		public WriteEventArgs(byte deviceId, List<Register> registers)
		{
			Registers = registers;
		}

		/// <summary>
		/// Gets a list of written coils.
		/// </summary>
		public List<Coil> Coils { get; private set; }

		/// <summary>
		/// Gets a list of written registers.
		/// </summary>
		public List<Register> Registers { get; private set; }
	}
}
