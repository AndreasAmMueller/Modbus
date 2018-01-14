using Modbus.Common.Structures;
using System;
using System.Collections.Generic;

namespace Modbus.Tcp.Utils
{
	internal class ModbusDevice
	{
		private List<ushort> coils = new List<ushort>();
		private List<ushort> discreteInputs = new List<ushort>();
		private Dictionary<ushort, ushort> inputRegisters = new Dictionary<ushort, ushort>();
		private Dictionary<ushort, ushort> holdingRegisters = new Dictionary<ushort, ushort>();

		public ModbusDevice(byte id)
		{
			DeviceId = id;
		}

		public byte DeviceId { get; private set; }

		#region Coils

		public Coil GetCoil(ushort address)
		{
			lock (coils)
			{
				return new Coil { Address = address, Value = coils.Contains(address) };
			}
		}

		public void SetCoil(ushort address, bool value)
		{
			lock (coils)
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

		public DiscreteInput GetInput(ushort address)
		{
			lock (discreteInputs)
			{
				return new DiscreteInput { Address = address, Value = discreteInputs.Contains(address) };
			}
		}

		public void SetInput(ushort address, bool value)
		{
			lock (discreteInputs)
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

		public Register GetInputRegister(ushort address)
		{
			lock (inputRegisters)
			{
				if (inputRegisters.TryGetValue(address, out ushort value))
				{
					return new Register { Address = address, Value = value };
				}
			}
			return new Register { Address = address };
		}

		public void SetInputRegister(ushort address, ushort value)
		{
			lock (inputRegisters)
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

		public Register GetHoldingRegister(ushort address)
		{
			lock (holdingRegisters)
			{
				if (holdingRegisters.TryGetValue(address, out ushort value))
				{
					return new Register { Address = address, Value = value };
				}
			}
			return new Register { Address = address };
		}

		public void SetHoldingRegister(ushort address, ushort value)
		{
			lock (holdingRegisters)
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
