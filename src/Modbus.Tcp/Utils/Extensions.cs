using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Modbus.Tcp.Utils
{
	/// <summary>
	/// Contains some extensions to handle some features more easily.
	/// </summary>
	public static class Extensions
	{
		#region Register handling

		#region To Register

		#region Unsigned data types to register(s)

		/// <summary>
		/// Converts a byte into a Modbus register.
		/// </summary>
		/// <param name="value">The byte value.</param>
		/// <param name="address">The register address.</param>
		/// <returns></returns>
		public static Register ToRegister(this byte value, int address)
		{
			if (address < Consts.MinAddress || Consts.MaxAddress < address)
			{
				throw new ArgumentOutOfRangeException(nameof(address));
			}

			return new Register
			{
				Address = Convert.ToUInt16(address),
				Value = value
			};
		}

		/// <summary>
		/// Converts a word into a Modbus register.
		/// </summary>
		/// <param name="value">The unsigned short value.</param>
		/// <param name="address">The register address.</param>
		/// <returns></returns>
		public static Register ToRegister(this ushort value, int address)
		{
			if (address < Consts.MinAddress || Consts.MaxAddress < address)
			{
				throw new ArgumentOutOfRangeException(nameof(address));
			}

			return new Register
			{
				Address = Convert.ToUInt16(address),
				Value = value
			};
		}

		/// <summary>
		/// Converts a dword into two Modbus registers.
		/// </summary>
		/// <param name="value">The unsigned int value.</param>
		/// <param name="address">The register address.</param>
		/// <returns></returns>
		public static List<Register> ToRegisters(this uint value, int address)
		{
			if (address < Consts.MinAddress || Consts.MaxAddress < (address + 1))
			{
				throw new ArgumentOutOfRangeException(nameof(address));
			}

			var list = new List<Register>();

			var blob = BitConverter.GetBytes(value);
			if (BitConverter.IsLittleEndian)
			{
				Array.Reverse(blob);
			}

			for (int i = 0; i < blob.Length / 2; i++)
			{
				list.Add(new Register
				{
					Address = Convert.ToUInt16(address + i),
					HiByte = blob[(i * 2)],
					LoByte = blob[(i * 2) + 1]
				});
			}

			return list;
		}

		/// <summary>
		/// Converts a qword into four Modbus registers.
		/// </summary>
		/// <param name="value">The unsigned long value.</param>
		/// <param name="address">The register address.</param>
		/// <returns></returns>
		public static List<Register> ToRegisters(this ulong value, int address)
		{
			if (address < Consts.MinAddress || Consts.MaxAddress < (address + 3))
			{
				throw new ArgumentOutOfRangeException(nameof(address));
			}

			var list = new List<Register>();

			var blob = BitConverter.GetBytes(value);
			if (BitConverter.IsLittleEndian)
			{
				Array.Reverse(blob);
			}

			for (int i = 0; i < blob.Length / 2; i++)
			{
				list.Add(new Register
				{
					Address = Convert.ToUInt16(address + i),
					HiByte = blob[(i * 2)],
					LoByte = blob[(i * 2) + 1]
				});
			}

			return list;
		}

		#endregion Unsigned data types to register(s)

		#region Signed data types to register(s)

		/// <summary>
		/// Converts a signed byte into a Modbus register.
		/// </summary>
		/// <param name="value">The signed byte value.</param>
		/// <param name="address">The register address.</param>
		/// <returns></returns>
		public static Register ToRegister(this sbyte value, int address)
		{
			if (address < Consts.MinAddress || Consts.MaxAddress < address)
			{
				throw new ArgumentOutOfRangeException(nameof(address));
			}

			return new Register
			{
				Address = Convert.ToUInt16(address),
				Value = (ushort)value
			};
		}

		/// <summary>
		/// Converts a short into a Modbus register.
		/// </summary>
		/// <param name="value">The short value.</param>
		/// <param name="address">The register address.</param>
		/// <returns></returns>
		public static Register ToRegister(this short value, int address)
		{
			if (address < Consts.MinAddress || Consts.MaxAddress < address)
			{
				throw new ArgumentOutOfRangeException(nameof(address));
			}

			var blob = BitConverter.GetBytes(value);
			if (BitConverter.IsLittleEndian)
			{
				Array.Reverse(blob);
			}

			return new Register
			{
				Address = Convert.ToUInt16(address),
				HiByte = blob[0],
				LoByte = blob[1]
			};
		}

		/// <summary>
		/// Converts an int into two Modbus registers.
		/// </summary>
		/// <param name="value">The int value.</param>
		/// <param name="address">The register address.</param>
		/// <returns></returns>
		public static List<Register> ToRegisters(this int value, int address)
		{
			if (address < Consts.MinAddress || Consts.MaxAddress < (address + 1))
			{
				throw new ArgumentOutOfRangeException(nameof(address));
			}

			var list = new List<Register>();

			var blob = BitConverter.GetBytes(value);
			if (BitConverter.IsLittleEndian)
			{
				Array.Reverse(blob);
			}

			for (int i = 0; i < blob.Length / 2; i++)
			{
				list.Add(new Register
				{
					Address = Convert.ToUInt16(address + i),
					HiByte = blob[(i * 2)],
					LoByte = blob[(i * 2) + 1]
				});
			}

			return list;
		}

		/// <summary>
		/// Converts a long into four Modbus registers.
		/// </summary>
		/// <param name="value">The long value.</param>
		/// <param name="address">The register address.</param>
		/// <returns></returns>
		public static List<Register> ToRegisters(this long value, int address)
		{
			if (address < Consts.MinAddress || Consts.MaxAddress < (address + 3))
			{
				throw new ArgumentOutOfRangeException(nameof(address));
			}

			var list = new List<Register>();

			var blob = BitConverter.GetBytes(value);
			if (BitConverter.IsLittleEndian)
			{
				Array.Reverse(blob);
			}

			for (int i = 0; i < blob.Length / 2; i++)
			{
				list.Add(new Register
				{
					Address = Convert.ToUInt16(address + i),
					HiByte = blob[(i * 2)],
					LoByte = blob[(i * 2) + 1]
				});
			}

			return list;
		}

		#endregion Signed data types to register(s)

		#region Floating point

		/// <summary>
		/// Converts a single into two Modbus registers.
		/// </summary>
		/// <param name="value">The float value.</param>
		/// <param name="address">The register address.</param>
		/// <returns></returns>
		public static List<Register> ToRegister(this float value, int address)
		{
			if (address < Consts.MinAddress || Consts.MaxAddress < (address + 1))
			{
				throw new ArgumentOutOfRangeException(nameof(address));
			}

			var list = new List<Register>();

			var blob = BitConverter.GetBytes(value);
			if (BitConverter.IsLittleEndian)
			{
				Array.Reverse(blob);
			}

			for (int i = 0; i < blob.Length / 2; i++)
			{
				list.Add(new Register
				{
					Address = Convert.ToUInt16(address + i),
					HiByte = blob[(i * 2)],
					LoByte = blob[(i * 2) + 1]
				});
			}

			return list;
		}

		/// <summary>
		/// Converts a double into four Modbus registers.
		/// </summary>
		/// <param name="value">The double value.</param>
		/// <param name="address">The register address.</param>
		/// <returns></returns>
		public static List<Register> ToRegister(this double value, int address)
		{
			if (address < Consts.MinAddress || Consts.MaxAddress < (address + 3))
			{
				throw new ArgumentOutOfRangeException(nameof(address));
			}

			var list = new List<Register>();

			var blob = BitConverter.GetBytes(value);
			if (BitConverter.IsLittleEndian)
			{
				Array.Reverse(blob);
			}

			for (int i = 0; i < blob.Length / 2; i++)
			{
				list.Add(new Register
				{
					Address = Convert.ToUInt16(address + i),
					HiByte = blob[(i * 2)],
					LoByte = blob[(i * 2) + 1]
				});
			}

			return list;
		}

		#endregion Floating point

		#region String to registers

		/// <summary>
		/// Converts a string into Modbus registers.
		/// </summary>
		/// <param name="str">The text.</param>
		/// <param name="address">The register address.</param>
		/// <param name="encoding">The encoding used to convert the string. Default: <see cref="Encoding.UTF8"/>.</param>
		/// <returns></returns>
		public static List<Register> ToRegisters(this string str, int address, Encoding encoding = null)
		{
			if (encoding == null)
			{
				encoding = Encoding.UTF8;
			}

			var list = new List<Register>();
			var blob = encoding.GetBytes(str);
			var numRegister = (int)Math.Ceiling(blob.Length / 2.0);

			if (address < Consts.MinAddress || Consts.MaxAddress < (address + numRegister))
			{
				throw new ArgumentOutOfRangeException(nameof(address));
			}

			for (int i = 0; i < numRegister; i++)
			{
				try
				{
					list.Add(new Register
					{
						Address = Convert.ToUInt16(address + i),
						HiByte = blob[(i * 2)],
						LoByte = blob[(i * 2) + 1]
					});
				}
				catch
				{
					list.Add(new Register
					{
						Address = Convert.ToUInt16(address + i),
						HiByte = blob[(i * 2)]
					});
				}
			}

			return list;
		}

		#endregion String to registers

		#endregion To Register

		#region From Register

		#region To unsigned data types

		/// <summary>
		/// Converts a register value into a byte.
		/// </summary>
		/// <param name="register">The register.</param>
		/// <returns></returns>
		public static byte GetByte(this Register register)
		{
			return (byte)register.Value;
		}

		/// <summary>
		/// Converts a register into a word.
		/// </summary>
		/// <param name="register">The register.</param>
		/// <returns></returns>
		public static ushort GetUInt16(this Register register)
		{
			return register.Value;
		}

		/// <summary>
		/// Converts two registers into a dword.
		/// </summary>
		/// <param name="list">The list of registers (min. 2).</param>
		/// <param name="startIndex">The start index. Default: 0.</param>
		/// <returns></returns>
		public static uint GetUInt32(this IEnumerable<Register> list, int startIndex = 0)
		{
			var registers = list.Skip(startIndex).Take(2).ToArray();
			var blob = new byte[registers.Length * 2];

			for (int i = 0; i < registers.Length; i++)
			{
				blob[i * 2] = registers[i].HiByte;
				blob[i * 2 + 1] = registers[i].LoByte;
			}

			if (BitConverter.IsLittleEndian)
			{
				Array.Reverse(blob);
			}

			return BitConverter.ToUInt32(blob, 0);
		}

		/// <summary>
		/// Converts four registers into a qword.
		/// </summary>
		/// <param name="list">The list of registers (min. 4).</param>
		/// <param name="startIndex">The start index. Default: 0.</param>
		/// <returns></returns>
		public static ulong GetUInt64(this IEnumerable<Register> list, int startIndex = 0)
		{
			var registers = list.Skip(startIndex).Take(4).ToArray();
			var blob = new byte[registers.Length * 2];

			for (int i = 0; i < registers.Length; i++)
			{
				blob[i * 2] = registers[i].HiByte;
				blob[i * 2 + 1] = registers[i].LoByte;
			}

			if (BitConverter.IsLittleEndian)
			{
				Array.Reverse(blob);
			}

			return BitConverter.ToUInt64(blob, 0);
		}

		#endregion To unsigned data types

		#region To signed data types

		/// <summary>
		/// Converts a register into a signed byte.
		/// </summary>
		/// <param name="register">The register.</param>
		/// <returns></returns>
		public static sbyte GetSByte(this Register register)
		{
			return (sbyte)register.Value;
		}

		/// <summary>
		/// Converts a register into a short.
		/// </summary>
		/// <param name="register">The register.</param>
		/// <returns></returns>
		public static short GetInt16(this Register register)
		{
			var blob = new[] { register.HiByte, register.LoByte };
			if (BitConverter.IsLittleEndian)
			{
				Array.Reverse(blob);
			}

			return BitConverter.ToInt16(blob, 0);
		}

		/// <summary>
		/// Converts two registers into an int.
		/// </summary>
		/// <param name="list">A list of registers (min. 2).</param>
		/// <param name="startIndex">The start index. Default: 0.</param>
		/// <returns></returns>
		public static int GetInt32(this IEnumerable<Register> list, int startIndex = 0)
		{
			var registers = list.Skip(startIndex).Take(2).ToArray();
			var blob = new byte[registers.Length * 2];

			for (int i = 0; i < registers.Length; i++)
			{
				blob[i * 2] = registers[i].HiByte;
				blob[i * 2 + 1] = registers[i].LoByte;
			}

			if (BitConverter.IsLittleEndian)
			{
				Array.Reverse(blob);
			}

			return BitConverter.ToInt32(blob, 0);
		}

		/// <summary>
		/// Converts four registers into a long.
		/// </summary>
		/// <param name="list">A list of registers (min. 4).</param>
		/// <param name="startIndex">The start index. Default: 0.</param>
		/// <returns></returns>
		public static long GetInt64(this IEnumerable<Register> list, int startIndex = 0)
		{
			var registers = list.Skip(startIndex).Take(4).ToArray();
			var blob = new byte[registers.Length * 2];

			for (int i = 0; i < registers.Length; i++)
			{
				blob[i * 2] = registers[i].HiByte;
				blob[i * 2 + 1] = registers[i].LoByte;
			}

			if (BitConverter.IsLittleEndian)
			{
				Array.Reverse(blob);
			}

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
		public static float GetSingle(this IEnumerable<Register> list, int startIndex = 0)
		{
			var registers = list.Skip(startIndex).Take(2).ToArray();
			var blob = new byte[registers.Length * 2];

			for (int i = 0; i < registers.Length; i++)
			{
				blob[i * 2] = registers[i].HiByte;
				blob[i * 2 + 1] = registers[i].LoByte;
			}

			if (BitConverter.IsLittleEndian)
			{
				Array.Reverse(blob);
			}

			return BitConverter.ToSingle(blob, 0);
		}

		/// <summary>
		/// Converts four registers into a double.
		/// </summary>
		/// <param name="list">A list of registers (min. 4).</param>
		/// <param name="startIndex">The start index. Default: 0.</param>
		/// <returns></returns>
		public static double GetDouble(this IEnumerable<Register> list, int startIndex = 0)
		{
			var registers = list.Skip(startIndex).Take(4).ToArray();
			var blob = new byte[registers.Length * 2];

			for (int i = 0; i < registers.Length; i++)
			{
				blob[i * 2] = registers[i].HiByte;
				blob[i * 2 + 1] = registers[i].LoByte;
			}

			if (BitConverter.IsLittleEndian)
			{
				Array.Reverse(blob);
			}

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
		public static string GetString(this IEnumerable<Register> list, int length, int index = 0, Encoding encoding = null)
		{
			if (encoding == null)
			{
				encoding = Encoding.UTF8;
			}

			var registers = list.Skip(index).Take(length).ToArray();
			var blob = new byte[registers.Length * 2];

			for (int i = 0; i < registers.Length; i++)
			{
				blob[i * 2] = registers[i].HiByte;
				blob[i * 2 + 1] = registers[i].LoByte;
			}

			return encoding.GetString(blob).Trim(new[] { ' ', '\t', '\0', '\r', '\n' });
		}

		#endregion To string

		#endregion From Register

		#endregion Register handling

		#region Task handling

		/// <summary>
		/// Forgets about the result of the task. (Prevent compiler warning).
		/// </summary>
		/// <param name="task">The task to forget.</param>
		internal async static void Forget(this Task task)
		{
			await task;
		}

		#endregion Task handling
	}
}
