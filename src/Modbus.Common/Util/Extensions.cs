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
		#region Public extensions

		#region Register to data type

		#region unsigned data types

		/// <summary>
		/// Converts a register value into a boolean.
		/// </summary>
		/// <param name="obj">The Modbus object.</param>
		/// <returns><c>false</c> if the value is zero (0), else <c>true</c>.</returns>
		public static bool GetBool(this ModbusObject obj)
		{
			if (obj == null)
				throw new ArgumentNullException(nameof(obj));

			return obj.RegisterValue > 0;
		}

		/// <summary>
		/// Converts a register value into a byte.
		/// </summary>
		/// <param name="register">The register.</param>
		/// <returns></returns>
		public static byte GetByte(this ModbusObject register)
		{
			if (register == null)
				throw new ArgumentNullException(nameof(register));

			if (register.Type != ModbusObjectType.HoldingRegister && register.Type != ModbusObjectType.InputRegister)
				throw new ArgumentException("Invalid register type");

			return (byte)register.RegisterValue;
		}

		/// <summary>
		/// Converts a register into a word.
		/// </summary>
		/// <param name="register">The register.</param>
		/// <returns></returns>
		public static ushort GetUInt16(this ModbusObject register)
		{
			if (register == null)
				throw new ArgumentNullException(nameof(register));

			if (register.Type != ModbusObjectType.HoldingRegister && register.Type != ModbusObjectType.InputRegister)
				throw new ArgumentException("Invalid register type");

			return register.RegisterValue;
		}

		/// <summary>
		/// Converts two registers into a dword.
		/// </summary>
		/// <param name="list">The list of registers (min. 2).</param>
		/// <param name="startIndex">The start index. Default: 0.</param>
		/// <param name="inverseRegisters">Inverses the register order as required by some implementations. Default: false.</param>

		/// <returns></returns>
		public static uint GetUInt32(this IEnumerable<ModbusObject> list, int startIndex = 0, bool inverseRegisters = false)

		{
			if (list == null)
				throw new ArgumentNullException(nameof(list));

			int count = list.Count();
			if (count < 2)
				throw new ArgumentException("At least two registers needed", nameof(list));

			if (startIndex < 0 || count < startIndex + 2)
				throw new ArgumentOutOfRangeException(nameof(startIndex));

			if (!list.All(r => r.Type == ModbusObjectType.HoldingRegister) && !list.All(r => r.Type == ModbusObjectType.InputRegister))
				throw new ArgumentException("Invalid register type");

			list = list.OrderBy(r => r.Address).Skip(startIndex).Take(2);
			if (inverseRegisters)

				list = list.Reverse();

			var registers = list.ToArray();
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
		/// <param name="inverseRegisters">Inverses the register order as required by some implementations. Default: false.</param>

		/// <returns></returns>
		public static ulong GetUInt64(this IEnumerable<ModbusObject> list, int startIndex = 0, bool inverseRegisters = false)

		{
			if (list == null)
				throw new ArgumentNullException(nameof(list));

			int count = list.Count();
			if (count < 4)
				throw new ArgumentException("At least four registers needed", nameof(list));

			if (startIndex < 0 || count < startIndex + 4)
				throw new ArgumentOutOfRangeException(nameof(startIndex));

			if (!list.All(r => r.Type == ModbusObjectType.HoldingRegister) && !list.All(r => r.Type == ModbusObjectType.InputRegister))
				throw new ArgumentException("Invalid register type");

			list = list.OrderBy(r => r.Address).Skip(startIndex).Take(4);
			if (inverseRegisters)

				list = list.Reverse();

			var registers = list.ToArray();
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

		#endregion unsigned data types

		#region signed data types

		/// <summary>
		/// Converts a register into a signed byte.
		/// </summary>
		/// <param name="register">The register.</param>
		/// <returns></returns>
		public static sbyte GetSByte(this ModbusObject register)
		{
			if (register == null)
				throw new ArgumentNullException(nameof(register));

			if (register.Type != ModbusObjectType.HoldingRegister && register.Type != ModbusObjectType.InputRegister)
				throw new ArgumentException("Invalid register type");

			return (sbyte)register.RegisterValue;
		}

		/// <summary>
		/// Converts a register into a short.
		/// </summary>
		/// <param name="register">The register.</param>
		/// <returns></returns>
		public static short GetInt16(this ModbusObject register)
		{
			if (register == null)
				throw new ArgumentNullException(nameof(register));

			if (register.Type != ModbusObjectType.HoldingRegister && register.Type != ModbusObjectType.InputRegister)
				throw new ArgumentException("Invalid register type");

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
		/// <param name="inverseRegisters">Inverses the register order as required by some implementations. Default: false.</param>

		/// <returns></returns>
		public static int GetInt32(this IEnumerable<ModbusObject> list, int startIndex = 0, bool inverseRegisters = false)

		{
			if (list == null)
				throw new ArgumentNullException(nameof(list));

			int count = list.Count();
			if (count < 2)
				throw new ArgumentException("At least two registers needed", nameof(list));

			if (startIndex < 0 || count < startIndex + 2)
				throw new ArgumentOutOfRangeException(nameof(startIndex));

			if (!list.All(r => r.Type == ModbusObjectType.HoldingRegister) && !list.All(r => r.Type == ModbusObjectType.InputRegister))
				throw new ArgumentException("Invalid register type");

			list = list.OrderBy(r => r.Address).Skip(startIndex).Take(2);
			if (inverseRegisters)

				list = list.Reverse();

			var registers = list.ToArray();
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
		/// <param name="inverseRegisters">Inverses the register order as required by some implementations. Default: false.</param>

		/// <returns></returns>
		public static long GetInt64(this IEnumerable<ModbusObject> list, int startIndex = 0, bool inverseRegisters = false)

		{
			if (list == null)
				throw new ArgumentNullException(nameof(list));

			int count = list.Count();
			if (count < 4)
				throw new ArgumentException("At least four registers needed", nameof(list));

			if (startIndex < 0 || count < startIndex + 4)

				throw new ArgumentOutOfRangeException(nameof(startIndex));
			if (!list.All(r => r.Type == ModbusObjectType.HoldingRegister) && !list.All(r => r.Type == ModbusObjectType.InputRegister))
				throw new ArgumentException("Invalid register type");

			list = list.OrderBy(r => r.Address).Skip(startIndex).Take(4);
			if (inverseRegisters)

				list = list.Reverse();

			var registers = list.ToArray();
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

		#endregion signed data types

		#region floating point types

		/// <summary>
		/// Converts two registers into a single.
		/// </summary>
		/// <param name="list">A list of registers (min. 2).</param>
		/// <param name="startIndex">The start index. Default: 0.</param>
		/// <param name="inverseRegisters">Inverses the register order as required by some implementations. Default: false.</param>

		/// <returns></returns>
		public static float GetSingle(this IEnumerable<ModbusObject> list, int startIndex = 0, bool inverseRegisters = false)

		{
			if (list == null)
				throw new ArgumentNullException(nameof(list));

			int count = list.Count();
			if (count < 2)
				throw new ArgumentException("At least two registers needed", nameof(list));

			if (startIndex < 0 || count < startIndex + 2)

				throw new ArgumentOutOfRangeException(nameof(startIndex));
			if (!list.All(r => r.Type == ModbusObjectType.HoldingRegister) && !list.All(r => r.Type == ModbusObjectType.InputRegister))
				throw new ArgumentException("Invalid register type");

			list = list.OrderBy(r => r.Address).Skip(startIndex).Take(2);
			if (inverseRegisters)

				list = list.Reverse();

			var registers = list.ToArray();
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
		/// <param name="inverseRegisters">Inverses the register order as required by some implementations. Default: false.</param>

		/// <returns></returns>
		public static double GetDouble(this IEnumerable<ModbusObject> list, int startIndex = 0, bool inverseRegisters = false)

		{
			if (list == null)
				throw new ArgumentNullException(nameof(list));

			int count = list.Count();
			if (count < 4)
				throw new ArgumentException("At least four registers needed", nameof(list));

			if (startIndex < 0 || count < startIndex + 4)
				throw new ArgumentOutOfRangeException(nameof(startIndex));

			if (!list.All(r => r.Type == ModbusObjectType.HoldingRegister) && !list.All(r => r.Type == ModbusObjectType.InputRegister))
				throw new ArgumentException("Invalid register type");

			list = list.OrderBy(r => r.Address).Skip(startIndex).Take(4);
			if (inverseRegisters)

				list = list.Reverse();

			var registers = list.ToArray();
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

		#endregion floating point types

		#region string

		/// <summary>
		/// Converts a list of registers into a string.
		/// </summary>
		/// <param name="list">A list of registers.</param>
		/// <param name="length">The number of registers to use.</param>
		/// <param name="startIndex">The start index. Default: 0.</param>
		/// <param name="encoding">The encoding to convert the string. Default: <see cref="Encoding.UTF8"/>.</param>
		/// <param name="flipBytes">A value indicating whether the bytes within a register (hi/lo byte) should be fliped due to correct character order.</param>
		/// <returns></returns>
		public static string GetString(this IEnumerable<ModbusObject> list, int length, int startIndex = 0, Encoding encoding = null, bool flipBytes = false)
		{
			if (list == null)
				throw new ArgumentNullException(nameof(list));

			int count = list.Count();
			if (count < length)
				throw new ArgumentException($"At least {length} registers needed", nameof(list));

			if (startIndex < 0 || count < startIndex + length)
				throw new ArgumentOutOfRangeException(nameof(startIndex));

			if (!list.All(r => r.Type == ModbusObjectType.HoldingRegister) && !list.All(r => r.Type == ModbusObjectType.InputRegister))
				throw new ArgumentException("Invalid register type");

			if (encoding == null)
				encoding = Encoding.UTF8;

			var registers = list.Skip(startIndex).Take(length).ToArray();
			byte[] blob = new byte[registers.Length * 2];

			for (int i = 0; i < registers.Length; i++)
			{
				blob[i * 2] = flipBytes ? registers[i].LoByte : registers[i].HiByte;
				blob[i * 2 + 1] = flipBytes ? registers[i].HiByte : registers[i].LoByte;
			}

			string str = encoding.GetString(blob).Trim(new[] { ' ', '\t', '\0', '\r', '\n' });
			int nullIdx = str.IndexOf('\0');

			if (nullIdx >= 0)
				return str.Substring(0, nullIdx);

			return str;
		}

		#endregion string

		#endregion Register to data type

		#region Data type to register

		#region unsigned data types

		/// <summary>
		/// Converts a boolean to a Modbus Coil.
		/// </summary>
		/// <param name="value">The boolean value.</param>
		/// <param name="address">The Modbus coil address.</param>
		/// <returns></returns>
		public static ModbusObject ToModbusCoil(this bool value, ushort address)
		{
			return new ModbusObject
			{
				Address = address,
				Type = ModbusObjectType.Coil,
				BoolValue = value
			};
		}

		/// <summary>
		/// Converts a boolean to a Modbus register.
		/// </summary>
		/// <param name="value">The boolean value.</param>
		/// <param name="address">The Modbus register address.</param>
		/// <returns></returns>
		public static ModbusObject ToModbusRegister(this bool value, ushort address)
		{
			return new ModbusObject
			{
				Address = address,
				Type = ModbusObjectType.HoldingRegister,
				RegisterValue = (ushort)(value ? 1 : 0)
			};
		}

		/// <summary>
		/// Converts a byte to a Modbus register.
		/// </summary>
		/// <param name="value">The byte to convert.</param>
		/// <param name="address">The Modbus register address.</param>
		/// <returns></returns>
		public static ModbusObject ToModbusRegister(this byte value, ushort address)
		{
			return new ModbusObject
			{
				Address = address,
				Type = ModbusObjectType.HoldingRegister,
				RegisterValue = value
			};
		}

		/// <summary>
		/// Converts an unsigned short to a Modbus register.
		/// </summary>
		/// <param name="value">The unsigned short to convert.</param>
		/// <param name="address">The Modbus register address.</param>
		/// <returns></returns>
		public static ModbusObject ToModbusRegister(this ushort value, ushort address)
		{
			return new ModbusObject
			{
				Address = address,
				Type = ModbusObjectType.HoldingRegister,
				RegisterValue = value
			};
		}

		/// <summary>
		/// Converts an unsigned integer to two Modbus registers.
		/// </summary>
		/// <param name="value">The unsigned integer to convert.</param>
		/// <param name="address">The Modbus register address.</param>
		/// <param name="inverseRegisters">Inverses the register order as required by some implementations. Default: false.</param>

		/// <returns></returns>
		public static ModbusObject[] ToModbusRegister(this uint value, ushort address, bool inverseRegisters = false)

		{
			byte[] bytes = BitConverter.GetBytes(value);
			if (BitConverter.IsLittleEndian)
				Array.Reverse(bytes);

			var registers = new ModbusObject[bytes.Length / 2];
			if (inverseRegisters)

			{
				int startAddress = address + registers.Length - 1;
				for (int i = 0; i < registers.Length; i++)

				{
					registers[i] = new ModbusObject
					{
						Address = (ushort)(address + i),
						Type = ModbusObjectType.HoldingRegister,
						HiByte = bytes[i * 2],
						LoByte = bytes[i * 2 + 1]
					};
				}
			}
			else
			{
				var startAddress = address + registers.Length - 1;
				for (int i = 0; i < registers.Length; i++)
				{
					registers[i] = new ModbusObject
					{
						Address = (ushort)(startAddress - i),
						Type = ModbusObjectType.HoldingRegister,
						HiByte = bytes[i * 2],
						LoByte = bytes[i * 2 + 1]
					};
				}
			}
			return registers;
		}

		/// <summary>
		/// Converts an unsigned long to four Modbus registers.
		/// </summary>
		/// <param name="value">The unsigned long to convert.</param>
		/// <param name="address">The Modbus register address.</param>
		/// <param name="inverseRegisters">Inverses the register order as required by some implementations. Default: false.</param>

		/// <returns></returns>
		public static ModbusObject[] ToModbusRegister(this ulong value, ushort address, bool inverseRegisters = false)

		{
			byte[] bytes = BitConverter.GetBytes(value);
			if (BitConverter.IsLittleEndian)
				Array.Reverse(bytes);

			var registers = new ModbusObject[bytes.Length / 2];
			if (inverseRegisters)

			{
				int startAddress = address + registers.Length - 1;
				for (int i = 0; i < registers.Length; i++)

				{
					registers[i] = new ModbusObject
					{
						Address = (ushort)(startAddress - i),
						Type = ModbusObjectType.HoldingRegister,
						HiByte = bytes[i * 2],
						LoByte = bytes[i * 2 + 1]
					};
				}
			}
			else
			{
				for (int i = 0; i < registers.Length; i++)
				{
					registers[i] = new ModbusObject
					{
						Address = (ushort)(address + i),
						Type = ModbusObjectType.HoldingRegister,
						HiByte = bytes[i * 2],
						LoByte = bytes[i * 2 + 1]
					};
				}

			}
			return registers;
		}

		#endregion unsigned data types

		#region signed data types

		/// <summary>
		/// Converts a signed byte to a Modbus register.
		/// </summary>
		/// <param name="value">The signed byte to convert.</param>
		/// <param name="address">The Modbus register address.</param>
		/// <returns></returns>
		public static ModbusObject ToModbusRegister(this sbyte value, ushort address)
		{
			return new ModbusObject
			{
				Address = address,
				Type = ModbusObjectType.HoldingRegister,
				RegisterValue = (ushort)value
			};
		}

		/// <summary>
		/// Converts a signed short to a Modbus register.
		/// </summary>
		/// <param name="value">The short to convert.</param>
		/// <param name="address">The Modbus register address.</param>
		/// <returns></returns>
		public static ModbusObject ToModbusRegister(this short value, ushort address)
		{
			return new ModbusObject
			{
				Address = address,
				Type = ModbusObjectType.HoldingRegister,
				RegisterValue = (ushort)value
			};
		}

		/// <summary>
		/// Converts a signed integer to two Modbus registers.
		/// </summary>
		/// <param name="value">The integer to convert.</param>
		/// <param name="address">The Modbus register address.</param>
		/// <param name="inverseRegisters">Inverses the register order as required by some implementations. Default: false.</param>

		/// <returns></returns>
		public static ModbusObject[] ToModbusRegister(this int value, ushort address, bool inverseRegisters = false)

		{
			byte[] bytes = BitConverter.GetBytes(value);
			if (BitConverter.IsLittleEndian)
				Array.Reverse(bytes);

			var registers = new ModbusObject[bytes.Length / 2];
			if (inverseRegisters)

			{
				int startAddress = address + registers.Length - 1;
				for (int i = 0; i < registers.Length; i++)

				{
					registers[i] = new ModbusObject
					{
						Address = (ushort)(startAddress - i),
						Type = ModbusObjectType.HoldingRegister,
						HiByte = bytes[i * 2],
						LoByte = bytes[i * 2 + 1]
					};
				}
			}
			else
			{
				for (int i = 0; i < registers.Length; i++)
				{
					registers[i] = new ModbusObject
					{
						Address = (ushort)(address + i),
						Type = ModbusObjectType.HoldingRegister,
						HiByte = bytes[i * 2],
						LoByte = bytes[i * 2 + 1]
					};
				}

			}
			return registers;
		}

		/// <summary>
		/// Converts a signed long to four Modbus registers.
		/// </summary>
		/// <param name="value">The long to convert.</param>
		/// <param name="address">The Modbus register address.</param>
		/// <param name="inverseRegisters">Inverses the register order as required by some implementations. Default: false.</param>

		/// <returns></returns>
		public static ModbusObject[] ToModbusRegister(this long value, ushort address, bool inverseRegisters = false)

		{
			byte[] bytes = BitConverter.GetBytes(value);
			if (BitConverter.IsLittleEndian)
				Array.Reverse(bytes);

			var registers = new ModbusObject[bytes.Length / 2];
			if (inverseRegisters)

			{
				int startAddress = address + registers.Length - 1;
				for (int i = 0; i < registers.Length; i++)

				{
					registers[i] = new ModbusObject
					{
						Address = (ushort)(startAddress - i),
						Type = ModbusObjectType.HoldingRegister,
						HiByte = bytes[i * 2],
						LoByte = bytes[i * 2 + 1]
					};
				}
			}
			else
			{
				for (int i = 0; i < registers.Length; i++)
				{
					registers[i] = new ModbusObject
					{
						Address = (ushort)(address + i),
						Type = ModbusObjectType.HoldingRegister,
						HiByte = bytes[i * 2],
						LoByte = bytes[i * 2 + 1]
					};
				}

			}
			return registers;
		}

		#endregion signed data types

		#region floating point dat types

		/// <summary>
		/// Converts a single to two Modbus registers.
		/// </summary>
		/// <param name="value">The float to convert.</param>
		/// <param name="address">The Modbus register address.</param>
		/// <param name="inverseRegisters">Inverses the register order as required by some implementations. Default: false.</param>

		/// <returns></returns>
		public static ModbusObject[] ToModbusRegister(this float value, ushort address, bool inverseRegisters = false)

		{
			byte[] bytes = BitConverter.GetBytes(value);
			if (BitConverter.IsLittleEndian)
				Array.Reverse(bytes);

			var registers = new ModbusObject[bytes.Length / 2];
			if (inverseRegisters)

			{
				int startAddress = address + registers.Length - 1;
				for (int i = 0; i < registers.Length; i++)

				{
					registers[i] = new ModbusObject
					{
						Address = (ushort)(address + i),
						Type = ModbusObjectType.HoldingRegister,
						HiByte = bytes[i * 2],
						LoByte = bytes[i * 2 + 1]
					};
				}
			}
			else
			{
				var startAddress = address + registers.Length - 1;
				for (int i = 0; i < registers.Length; i++)
				{
					registers[i] = new ModbusObject
					{
						Address = (ushort)(startAddress - i),
						Type = ModbusObjectType.HoldingRegister,
						HiByte = bytes[i * 2],
						LoByte = bytes[i * 2 + 1]
					};
				}
			}
			return registers;
		}

		/// <summary>
		/// Converts a double to four Modbus registers.
		/// </summary>
		/// <param name="value">The double to convert.</param>
		/// <param name="address">The Modbus register address.</param>
		/// <param name="inverseRegisters">Inverses the register order as required by some implementations. Default: false.</param>

		/// <returns></returns>
		public static ModbusObject[] ToModbusRegister(this double value, ushort address, bool inverseRegisters = false)

		{
			byte[] bytes = BitConverter.GetBytes(value);
			if (BitConverter.IsLittleEndian)
				Array.Reverse(bytes);

			var registers = new ModbusObject[bytes.Length / 2];
			if (inverseRegisters)

			{
				int startAddress = address + registers.Length - 1;
				for (int i = 0; i < registers.Length; i++)

				{
					registers[i] = new ModbusObject
					{
						Address = (ushort)(startAddress - i),
						Type = ModbusObjectType.HoldingRegister,
						HiByte = bytes[i * 2],
						LoByte = bytes[i * 2 + 1]
					};
				}
			}
			else
			{
				for (int i = 0; i < registers.Length; i++)
				{
					registers[i] = new ModbusObject
					{
						Address = (ushort)(address + i),
						Type = ModbusObjectType.HoldingRegister,
						HiByte = bytes[i * 2],
						LoByte = bytes[i * 2 + 1]
					};
				}

			}
			return registers;
		}

		#endregion floating point dat types

		#region string

		/// <summary>
		/// Converts a string into Modbus registers.
		/// </summary>
		/// <param name="str">The string to convert.</param>
		/// <param name="address">The Modbus register address.</param>
		/// <param name="encoding">The string encoding. Default: <see cref="Encoding.UTF8"/>.</param>
		/// <param name="flipBytes">A value indicating whether the bytes within a register (hi/lo byte) should be fliped due to correct character order.</param>
		/// <param name="length">The zero-padded string length. If 0 (zero) the string will occupy the number of bytes equals to the actual lenght of the string.</param>

		/// <returns></returns>
		public static ModbusObject[] ToModbusRegister(this string str, ushort address, Encoding encoding = null, bool flipBytes = false, int length = 0)

		{
			if (str == null)
				throw new ArgumentNullException(nameof(str));

			if (encoding == null)
				encoding = Encoding.UTF8;

			byte[] bytes = encoding.GetBytes(str);
			if (length > 0)
				Array.Resize(ref bytes, length);

			var registers = new ModbusObject[(int)Math.Ceiling(bytes.Length / 2.0)];
			for (int i = 0; i < registers.Length; i++)
			{
				byte hi = flipBytes ? (i * 2 + 1 < bytes.Length) ? bytes[i * 2 + 1] : (byte)0 : bytes[i * 2];
				byte lo = flipBytes ? bytes[i * 2] : (i * 2 + 1 < bytes.Length) ? bytes[i * 2 + 1] : (byte)0;

				registers[i] = new ModbusObject
				{
					Address = (ushort)(address + i),
					Type = ModbusObjectType.HoldingRegister,
					HiByte = hi,
					LoByte = lo
				};
			}
			return registers;
		}

		#endregion string

		#endregion Data type to register

		#endregion Public extensions

		#region Internal extensions

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

		internal static string GetDescription(this Enum enumValue)
		{
			return enumValue.GetAttribute<DescriptionAttribute>()?.Description ?? enumValue.ToString();
		}

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

		#endregion Internal extensions
	}
}
