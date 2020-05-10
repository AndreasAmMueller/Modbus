using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace AMWD.Modbus.Proxy
{
	internal class ProxyDevice
	{
		#region Fields

		private readonly ReaderWriterLockSlim coilsLock = new ReaderWriterLockSlim();
		private readonly ReaderWriterLockSlim discreteInputsLock = new ReaderWriterLockSlim();
		private readonly ReaderWriterLockSlim inputRegistersLock = new ReaderWriterLockSlim();
		private readonly ReaderWriterLockSlim holdingRegistersLock = new ReaderWriterLockSlim();

		private readonly Dictionary<ushort, (DateTime Timestamp, bool Value)> coils = new Dictionary<ushort, (DateTime Timestamp, bool Value)>();
		private readonly Dictionary<ushort, (DateTime Timestamp, bool Value)> discreteInputs = new Dictionary<ushort, (DateTime Timestamp, bool Value)>();
		private readonly Dictionary<ushort, (DateTime Timestamp, ushort Value)> inputRegisters = new Dictionary<ushort, (DateTime Timestamp, ushort Value)>();
		private readonly Dictionary<ushort, (DateTime Timestamp, ushort Value)> holdingRegisters = new Dictionary<ushort, (DateTime Timestamp, ushort Value)>();

		#endregion Fields

		#region Coils

		public (DateTime Timestamp, bool Value) GetCoil(ushort address)
		{
			using (coilsLock.GetReadLock())
			{
				if (coils.TryGetValue(address, out var value))
					return value;
			}
			return (DateTime.UtcNow, false);
		}

		public void SetCoil(ushort address, bool value)
		{
			using (coilsLock.GetWriteLock())
			{
				coils[address] = (DateTime.UtcNow, value);
			}
		}

		#endregion Coils

		#region Discrete Inputs

		public (DateTime Timestamp, bool Value) GetDiscreteInput(ushort address)
		{
			using (discreteInputsLock.GetReadLock())
			{
				if (discreteInputs.TryGetValue(address, out var value))
					return value;
			}
			return (DateTime.UtcNow, false);
		}

		public void SetDiscreteInput(ushort address, bool value)
		{
			using (discreteInputsLock.GetWriteLock())
			{
				discreteInputs[address] = (DateTime.UtcNow, value);
			}
		}

		#endregion Discrete Inputs

		#region Input Registers

		public (DateTime Timestamp, ushort Value) GetInputRegister(ushort address)
		{
			using (inputRegistersLock.GetReadLock())
			{
				if (inputRegisters.TryGetValue(address, out var value))
					return value;
			}
			return (DateTime.UtcNow, 0);
		}

		public void SetInputRegister(ushort address, ushort value)
		{
			using (inputRegistersLock.GetWriteLock())
			{
				inputRegisters[address] = (DateTime.UtcNow, value);
			}
		}

		#endregion Input Registers

		#region Holding Registers

		public (DateTime Timestamp, ushort Value) GetHoldingRegister(ushort address)
		{
			using (holdingRegistersLock.GetReadLock())
			{
				if (holdingRegisters.TryGetValue(address, out var value))
					return value;
			}
			return (DateTime.UtcNow, 0);
		}

		public void SetHoldingRegister(ushort address, ushort value)
		{
			using (holdingRegistersLock.GetWriteLock())
			{
				holdingRegisters[address] = (DateTime.UtcNow, value);
			}
		}

		#endregion Holding Registers
	}
}
