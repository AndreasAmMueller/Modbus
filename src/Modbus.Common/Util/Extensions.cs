using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using AMWD.Modbus.Common.Structures;

namespace AMWD.Modbus.Common.Util
{
	/// <summary>
	/// Contains some extensions to handle some features more easily.
	/// </summary>
	public static class Extensions
	{
		#region Register handling

		#region To unsigned data types

		/// <summary>
		/// Converts a register value into a byte.
		/// </summary>
		/// <param name="register">The register.</param>
		/// <returns></returns>
		public static byte GetByte(this ModbusRegister register)
		{
			return (byte)register.Value;
		}

		/// <summary>
		/// Converts a register into a word.
		/// </summary>
		/// <param name="register">The register.</param>
		/// <returns></returns>
		public static ushort GetUInt16(this ModbusRegister register)
		{
			return register.Value;
		}

		/// <summary>
		/// Converts two registers into a dword.
		/// </summary>
		/// <param name="list">The list of registers (min. 2).</param>
		/// <param name="startIndex">The start index. Default: 0.</param>
		/// <returns></returns>
		public static uint GetUInt32(this IEnumerable<ModbusRegister> list, int startIndex = 0)
		{
			var registers = list.Skip(startIndex).Take(2).ToArray();
			byte[] blob = new byte[registers.Length * 2];

			for (int i = 0; i < registers.Length; i++)
			{
				blob[i * 2] = registers[i].HiByte;
				blob[i * 2 + 1] = registers[i].LoByte;
			}

			if (BitConverter.IsLittleEndian)
				Array.Reverse(blob);

			return BitConverter.ToUInt32(blob, 0);
		}

		/// <summary>
		/// Converts four registers into a qword.
		/// </summary>
		/// <param name="list">The list of registers (min. 4).</param>
		/// <param name="startIndex">The start index. Default: 0.</param>
		/// <returns></returns>
		public static ulong GetUInt64(this IEnumerable<ModbusRegister> list, int startIndex = 0)
		{
			var registers = list.Skip(startIndex).Take(4).ToArray();
			byte[] blob = new byte[registers.Length * 2];

			for (int i = 0; i < registers.Length; i++)
			{
				blob[i * 2] = registers[i].HiByte;
				blob[i * 2 + 1] = registers[i].LoByte;
			}

			if (BitConverter.IsLittleEndian)
				Array.Reverse(blob);

			return BitConverter.ToUInt64(blob, 0);
		}

		#endregion To unsigned data types

		#region To signed data types

		/// <summary>
		/// Converts a register into a signed byte.
		/// </summary>
		/// <param name="register">The register.</param>
		/// <returns></returns>
		public static sbyte GetSByte(this ModbusRegister register)
		{
			return (sbyte)register.Value;
		}

		/// <summary>
		/// Converts a register into a short.
		/// </summary>
		/// <param name="register">The register.</param>
		/// <returns></returns>
		public static short GetInt16(this ModbusRegister register)
		{
			byte[] blob = new[] { register.HiByte, register.LoByte };
			if (BitConverter.IsLittleEndian)
				Array.Reverse(blob);

			return BitConverter.ToInt16(blob, 0);
		}

		/// <summary>
		/// Converts two registers into an int.
		/// </summary>
		/// <param name="list">A list of registers (min. 2).</param>
		/// <param name="startIndex">The start index. Default: 0.</param>
		/// <returns></returns>
		public static int GetInt32(this IEnumerable<ModbusRegister> list, int startIndex = 0)
		{
			var registers = list.Skip(startIndex).Take(2).ToArray();
			byte[] blob = new byte[registers.Length * 2];

			for (int i = 0; i < registers.Length; i++)
			{
				blob[i * 2] = registers[i].HiByte;
				blob[i * 2 + 1] = registers[i].LoByte;
			}

			if (BitConverter.IsLittleEndian)
				Array.Reverse(blob);

			return BitConverter.ToInt32(blob, 0);
		}

		/// <summary>
		/// Converts four registers into a long.
		/// </summary>
		/// <param name="list">A list of registers (min. 4).</param>
		/// <param name="startIndex">The start index. Default: 0.</param>
		/// <returns></returns>
		public static long GetInt64(this IEnumerable<ModbusRegister> list, int startIndex = 0)
		{
			var registers = list.Skip(startIndex).Take(4).ToArray();
			byte[] blob = new byte[registers.Length * 2];

			for (int i = 0; i < registers.Length; i++)
			{
				blob[i * 2] = registers[i].HiByte;
				blob[i * 2 + 1] = registers[i].LoByte;
			}

			if (BitConverter.IsLittleEndian)
				Array.Reverse(blob);

			return BitConverter.ToInt64(blob, 0);
		}

		#endregion To signed data types

		#region To floating point types

		/// <summary>
		/// Converts two registers into a single.
		/// </summary>
		/// <param name="list">A list of registers (min. 2).</param>
		/// <param name="startIndex">The start index. Default: 0.</param>
		/// <returns></returns>
		public static float GetSingle(this IEnumerable<ModbusRegister> list, int startIndex = 0)
		{
			var registers = list.Skip(startIndex).Take(2).ToArray();
			byte[] blob = new byte[registers.Length * 2];

			for (int i = 0; i < registers.Length; i++)
			{
				blob[i * 2] = registers[i].HiByte;
				blob[i * 2 + 1] = registers[i].LoByte;
			}

			if (BitConverter.IsLittleEndian)
				Array.Reverse(blob);

			return BitConverter.ToSingle(blob, 0);
		}

		/// <summary>
		/// Converts four registers into a double.
		/// </summary>
		/// <param name="list">A list of registers (min. 4).</param>
		/// <param name="startIndex">The start index. Default: 0.</param>
		/// <returns></returns>
		public static double GetDouble(this IEnumerable<ModbusRegister> list, int startIndex = 0)
		{
			var registers = list.Skip(startIndex).Take(4).ToArray();
			byte[] blob = new byte[registers.Length * 2];

			for (int i = 0; i < registers.Length; i++)
			{
				blob[i * 2] = registers[i].HiByte;
				blob[i * 2 + 1] = registers[i].LoByte;
			}

			if (BitConverter.IsLittleEndian)
				Array.Reverse(blob);

			return BitConverter.ToDouble(blob, 0);
		}

		#endregion To floating point types

		#region To string

		/// <summary>
		/// Converts a list of registers into a string.
		/// </summary>
		/// <param name="list">A list of registers.</param>
		/// <param name="length">The number of registers to use.</param>
		/// <param name="index">The start index. Default: 0.</param>
		/// <param name="encoding">The encoding to convert the string. Default: <see cref="Encoding.UTF8"/>.</param>
		/// <returns></returns>
		public static string GetString(this IEnumerable<ModbusRegister> list, int length, int index = 0, Encoding encoding = null)
		{
			if (encoding == null)
			{
				encoding = Encoding.UTF8;
			}

			var registers = list.Skip(index).Take(length).ToArray();
			byte[] blob = new byte[registers.Length * 2];

			for (int i = 0; i < registers.Length; i++)
			{
				blob[i * 2] = registers[i].HiByte;
				blob[i * 2 + 1] = registers[i].LoByte;
			}

			string str = encoding.GetString(blob).Trim(new[] { ' ', '\t', '\0', '\r', '\n' });
			int nullIdx = str.IndexOf('\0');

			if (nullIdx >= 0)
				return str.Substring(0, nullIdx);

			return str;
		}

		#endregion To string

		#endregion Register handling

		#region Enums

		/// <summary>
		/// Tries to return an attribute of an enum value.
		/// </summary>
		/// <typeparam name="T">The attribute type.</typeparam>
		/// <param name="enumValue">The enum value.</param>
		/// <returns>The first attribute of the type present or null.</returns>
		internal static T GetAttribute<T>(this Enum enumValue)
			where T : Attribute
		{
			if (enumValue != null)
			{
				var fi = enumValue.GetType().GetField(enumValue.ToString());
				var attrs = (T[])fi?.GetCustomAttributes(typeof(T), inherit: false);
				return attrs?.FirstOrDefault();
			}
			return default;
		}

		/// <summary>
		/// Tries to read the description of an enum value.
		/// </summary>
		/// <param name="enumValue">The enum value.</param>
		/// <returns>The description or the <see cref="Enum.ToString()"/></returns>
		internal static string GetDescription(this Enum enumValue)
		{
			return enumValue.GetAttribute<DescriptionAttribute>()?.Description ?? enumValue.ToString();
		}

		#endregion Enums

		#region ReaderWriterLockSlim

		internal static IDisposable GetReadLock(this ReaderWriterLockSlim rwLock, int millisecondsTimeout = -1)
		{
			if (!rwLock.TryEnterReadLock(millisecondsTimeout))
				throw new TimeoutException("Trying to enter a read lock.");

			return new DisposableReaderWriterLockSlim(rwLock, DisposableReaderWriterLockSlim.LockMode.ReadLock);
		}

		internal static IDisposable GetReadLock(this ReaderWriterLockSlim rwLock, TimeSpan timeSpan)
		{
			if (!rwLock.TryEnterReadLock(timeSpan))
				throw new TimeoutException("Trying to enter a read lock.");

			return new DisposableReaderWriterLockSlim(rwLock, DisposableReaderWriterLockSlim.LockMode.ReadLock);
		}

		internal static IDisposable GetUpgradableReadLock(this ReaderWriterLockSlim rwLock, int millisecondsTimeout = -1)
		{
			if (!rwLock.TryEnterUpgradeableReadLock(millisecondsTimeout))
				throw new TimeoutException("Trying to enter an upgradable read lock.");

			return new DisposableReaderWriterLockSlim(rwLock, DisposableReaderWriterLockSlim.LockMode.UpgradableReadLock);
		}

		internal static IDisposable GetUpgradableReadLock(this ReaderWriterLockSlim rwLock, TimeSpan timeSpan)
		{
			if (!rwLock.TryEnterUpgradeableReadLock(timeSpan))
				throw new TimeoutException("Trying to enter an upgradable read lock.");

			return new DisposableReaderWriterLockSlim(rwLock, DisposableReaderWriterLockSlim.LockMode.UpgradableReadLock);
		}

		internal static IDisposable GetWriteLock(this ReaderWriterLockSlim rwLock, int millisecondsTimeout = -1)
		{
			if (!rwLock.TryEnterWriteLock(millisecondsTimeout))
				throw new TimeoutException("Trying to enter a write lock.");

			return new DisposableReaderWriterLockSlim(rwLock, DisposableReaderWriterLockSlim.LockMode.WriteLock);
		}

		internal static IDisposable GetWriteLock(this ReaderWriterLockSlim rwLock, TimeSpan timeSpan)
		{
			if (!rwLock.TryEnterWriteLock(timeSpan))
				throw new TimeoutException("Trying to enter a write lock.");

			return new DisposableReaderWriterLockSlim(rwLock, DisposableReaderWriterLockSlim.LockMode.WriteLock);
		}

		private class DisposableReaderWriterLockSlim : IDisposable
		{
			private readonly ReaderWriterLockSlim rwLock;
			private LockMode mode;

			public DisposableReaderWriterLockSlim(ReaderWriterLockSlim rwLock, LockMode mode)
			{
				this.rwLock = rwLock;
				this.mode = mode;
			}

			public void Dispose()
			{
				if (rwLock == null || mode == LockMode.None)
					return;

				if (mode == LockMode.ReadLock)
					rwLock.ExitReadLock();

				if (mode == LockMode.UpgradableReadLock && rwLock.IsWriteLockHeld)
					rwLock.ExitWriteLock();

				if (mode == LockMode.UpgradableReadLock)
					rwLock.ExitUpgradeableReadLock();

				if (mode == LockMode.WriteLock)
					rwLock.ExitWriteLock();

				mode = LockMode.None;
			}

			public enum LockMode
			{
				None,
				ReadLock,
				UpgradableReadLock,
				WriteLock
			}
		}

		#endregion ReaderWriterLockSlim
	}
}
