﻿using System.Collections.Generic;
using System.Threading;
using AMWD.Modbus.Common.Structures;

namespace AMWD.Modbus.Common.Util
{
	/// <summary>
	/// Represents a Modbus device.
	/// </summary>
	public class ModbusDevice
	{
		private readonly ReaderWriterLockSlim coilsLock = new ReaderWriterLockSlim();
		private readonly ReaderWriterLockSlim discreteInputsLock = new ReaderWriterLockSlim();
		private readonly ReaderWriterLockSlim inputRegistersLock = new ReaderWriterLockSlim();
		private readonly ReaderWriterLockSlim holdingRegistersLock = new ReaderWriterLockSlim();

		private readonly List<ushort> coils = new List<ushort>();
		private readonly List<ushort> discreteInputs = new List<ushort>();
		private readonly Dictionary<ushort, ushort> inputRegisters = new Dictionary<ushort, ushort>();
		private readonly Dictionary<ushort, ushort> holdingRegisters = new Dictionary<ushort, ushort>();

		/// <summary>
		/// Initializes a new instance of the <see cref="ModbusDevice"/> class.
		/// </summary>
		/// <param name="id">The device id.</param>
		public ModbusDevice(byte id)
		{
			DeviceId = id;
		}

		/// <summary>
		/// Gets the device id.
		/// </summary>
		public byte DeviceId { get; private set; }

		#region Coils

		/// <summary>
		/// Gets a coil at an address.
		/// </summary>
		/// <param name="address">The address of the coil.</param>
		/// <returns></returns>
		public Coil GetCoil(ushort address)
		{
			using (coilsLock.GetReadLock())
			{
				return new Coil { Address = address, Value = coils.Contains(address) };
			}
		}

		/// <summary>
		/// Sets a coil value.
		/// </summary>
		/// <param name="address">The address.</param>
		/// <param name="value">A value indicating whether the coil is active.</param>
		public void SetCoil(ushort address, bool value)
		{
			using (coilsLock.GetWriteLock())
			{
				if (value && !coils.Contains(address))
				{
					coils.Add(address);
				}
				if (!value && coils.Contains(address))
				{
					coils.Remove(address);
				}
			}
		}

		#endregion Coils

		#region Discrete Input

		/// <summary>
		/// Gets an input.
		/// </summary>
		/// <param name="address">The address.</param>
		/// <returns></returns>
		public DiscreteInput GetInput(ushort address)
		{
			using (discreteInputsLock.GetReadLock())
			{
				return new DiscreteInput { Address = address, Value = discreteInputs.Contains(address) };
			}
		}

		/// <summary>
		/// Sets an input.
		/// </summary>
		/// <param name="address">The address.</param>
		/// <param name="value">A value indicating whether the input is active.</param>
		public void SetInput(ushort address, bool value)
		{
			using (discreteInputsLock.GetWriteLock())
			{
				if (value && !discreteInputs.Contains(address))
				{
					discreteInputs.Add(address);
				}
				if (!value && discreteInputs.Contains(address))
				{
					discreteInputs.Remove(address);
				}
			}
		}

		#endregion Discrete Input

		#region Input Register

		/// <summary>
		/// Gets an input register.
		/// </summary>
		/// <param name="address">The address.</param>
		/// <returns></returns>
		public InputRegister GetInputRegister(ushort address)
		{
			using (inputRegistersLock.GetReadLock())
			{
				if (inputRegisters.TryGetValue(address, out ushort value))
					return new InputRegister { Address = address, Value = value };
			}
			return new InputRegister { Address = address };
		}

		/// <summary>
		/// Sets an input register.
		/// </summary>
		/// <param name="address">The address.</param>
		/// <param name="value">The value.</param>
		public void SetInputRegister(ushort address, ushort value)
		{
			using (inputRegistersLock.GetWriteLock())
			{
				if (value > 0)
				{
					inputRegisters[address] = value;
				}
				else
				{
					inputRegisters.Remove(address);
				}
			}
		}

		#endregion Input Register

		#region Holding Register

		/// <summary>
		/// Gets an holding register.
		/// </summary>
		/// <param name="address">The address.</param>
		/// <returns></returns>
		public HoldingRegister GetHoldingRegister(ushort address)
		{
			using (holdingRegistersLock.GetReadLock())
			{
				if (holdingRegisters.TryGetValue(address, out ushort value))
					return new HoldingRegister { Address = address, Value = value };
			}
			return new HoldingRegister { Address = address };
		}

		/// <summary>
		/// Sets an holding register.
		/// </summary>
		/// <param name="address">The address.</param>
		/// <param name="value">The value to set.</param>
		public void SetHoldingRegister(ushort address, ushort value)
		{
			using (holdingRegistersLock.GetWriteLock())
			{
				if (value > 0)
				{
					holdingRegisters[address] = value;
				}
				else
				{
					holdingRegisters.Remove(address);
				}
			}
		}

		#endregion Holding Register
	}
}
