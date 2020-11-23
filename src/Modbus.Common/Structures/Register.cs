using System;
using System.Collections.Generic;
using System.Text;

namespace AMWD.Modbus.Common.Structures
{
	/// <summary>
	/// Represents an holding register to keep the modbus typical naming.
	/// </summary>
	/// <remarks>
	/// For programming use the abstract class <see cref="ModbusObject"/>.
	/// </remarks>
	public class Register : ModbusObject
	{
		#region Creates

		#region unsigned

		/// <summary>
		/// Initializes a new register from a byte.
		/// </summary>
		/// <param name="value">The byte value.</param>
		/// <param name="address">The register address.</param>
		/// <param name="isInput">Flag to create an input register.</param>
		/// <returns></returns>
		public static Register Create(byte value, ushort address, bool isInput = false)
		{
			return new Register
			{
				Type = isInput ? ModbusObjectType.InputRegister : ModbusObjectType.HoldingRegister,
				Address = address,
				RegisterValue = value
			};
		}

		/// <summary>
		/// Initializes a new register from a unsigned short.
		/// </summary>
		/// <param name="value">The uint16 value.</param>
		/// <param name="address">The register address.</param>
		/// <param name="isInput">Flag to create an input register.</param>
		/// <returns></returns>
		public static Register Create(ushort value, ushort address, bool isInput = false)
		{
			return new Register
			{
				Type = isInput ? ModbusObjectType.InputRegister : ModbusObjectType.HoldingRegister,
				Address = address,
				RegisterValue = value
			};
		}

		/// <summary>
		/// Initializes new registers from an unsigned int.
		/// </summary>
		/// <param name="value">The uint32 value.</param>
		/// <param name="address">The register address.</param>
		/// <param name="isInput">Flag to create an input register.</param>
		/// <returns></returns>
		public static List<Register> Create(uint value, ushort address, bool isInput = false)
		{
			if (address + 1 > Consts.MaxAddress)
			{
				throw new ArgumentOutOfRangeException(nameof(address));
			}

			var list = new List<Register>();
			byte[] blob = BitConverter.GetBytes(value);
			if (BitConverter.IsLittleEndian)
				Array.Reverse(blob);

			for (int i = 0; i < blob.Length / 2; i++)
			{
				int bytePos = i * 2;
				list.Add(new Register
				{
					Type = isInput ? ModbusObjectType.InputRegister : ModbusObjectType.HoldingRegister,
					Address = Convert.ToUInt16(address + i),
					HiByte = blob[bytePos],
					LoByte = blob[bytePos + 1]
				});
			}

			return list;
		}

		/// <summary>
		/// Initializes new registers from an unsigned long.
		/// </summary>
		/// <param name="value">The uint64 value.</param>
		/// <param name="address">The register address.</param>
		/// <param name="isInput">Flag to create an input register.</param>
		/// <returns></returns>
		public static List<Register> Create(ulong value, ushort address, bool isInput = false)
		{
			if (address + 3 > Consts.MaxAddress)
				throw new ArgumentOutOfRangeException(nameof(address));

			var list = new List<Register>();
			byte[] blob = BitConverter.GetBytes(value);
			if (BitConverter.IsLittleEndian)
				Array.Reverse(blob);

			for (int i = 0; i < blob.Length / 2; i++)
			{
				int bytePos = i * 2;
				list.Add(new Register
				{
					Type = isInput ? ModbusObjectType.InputRegister : ModbusObjectType.HoldingRegister,
					Address = Convert.ToUInt16(address + i),
					HiByte = blob[bytePos],
					LoByte = blob[bytePos + 1]
				});
			}

			return list;
		}

		#endregion unsigned

		#region signed

		/// <summary>
		/// Initializes a new register from a signed byte.
		/// </summary>
		/// <param name="value">The sbyte value.</param>
		/// <param name="address">The register address.</param>
		/// <param name="isInput">Flag to create an input register.</param>
		/// <returns></returns>
		public static Register Create(sbyte value, ushort address, bool isInput = false)
		{
			return new Register
			{
				Type = isInput ? ModbusObjectType.InputRegister : ModbusObjectType.HoldingRegister,
				Address = address,
				RegisterValue = (ushort)value
			};
		}

		/// <summary>
		/// Initializes a new register from a short.
		/// </summary>
		/// <param name="value">The int16 value.</param>
		/// <param name="address">The register address.</param>
		/// <param name="isInput">Flag to create an input register.</param>
		/// <returns></returns>
		public static Register Create(short value, ushort address, bool isInput = false)
		{
			byte[] blob = BitConverter.GetBytes(value);
			if (BitConverter.IsLittleEndian)
				Array.Reverse(blob);

			return new Register
			{
				Type = isInput ? ModbusObjectType.InputRegister : ModbusObjectType.HoldingRegister,
				Address = address,
				HiByte = blob[0],
				LoByte = blob[1]
			};
		}

		/// <summary>
		/// Initializes new registers from an int.
		/// </summary>
		/// <param name="value">The int32 value.</param>
		/// <param name="address">The register address.</param>
		/// <param name="isInput">Flag to create an input register.</param>
		/// <returns></returns>
		public static List<Register> Create(int value, ushort address, bool isInput = false)
		{
			if (address + 1 > Consts.MaxAddress)
				throw new ArgumentOutOfRangeException(nameof(address));

			var list = new List<Register>();
			byte[] blob = BitConverter.GetBytes(value);
			if (BitConverter.IsLittleEndian)
				Array.Reverse(blob);

			for (int i = 0; i < blob.Length / 2; i++)
			{
				int bytePos = i * 2;
				list.Add(new Register
				{
					Type = isInput ? ModbusObjectType.InputRegister : ModbusObjectType.HoldingRegister,
					Address = Convert.ToUInt16(address + i),
					HiByte = blob[bytePos],
					LoByte = blob[bytePos + 1]
				});
			}

			return list;
		}

		/// <summary>
		/// Initializes new registers from a long.
		/// </summary>
		/// <param name="value">The int64 value.</param>
		/// <param name="address">The register address.</param>
		/// <param name="isInput">Flag to create an input register.</param>
		/// <returns></returns>
		public static List<Register> Create(long value, ushort address, bool isInput = false)
		{
			if (address + 3 > Consts.MaxAddress)
				throw new ArgumentOutOfRangeException(nameof(address));

			var list = new List<Register>();
			byte[] blob = BitConverter.GetBytes(value);
			if (BitConverter.IsLittleEndian)
				Array.Reverse(blob);

			for (int i = 0; i < blob.Length / 2; i++)
			{
				int bytePos = i * 2;
				list.Add(new Register
				{
					Type = isInput ? ModbusObjectType.InputRegister : ModbusObjectType.HoldingRegister,
					Address = Convert.ToUInt16(address + i),
					HiByte = blob[bytePos],
					LoByte = blob[bytePos + 1]
				});
			}

			return list;
		}

		#endregion signed

		#region floating point

		/// <summary>
		/// Initializes new registers from a float.
		/// </summary>
		/// <param name="value">The single value.</param>
		/// <param name="address">The register address.</param>
		/// <param name="isInput">Flag to create an input register.</param>
		/// <returns></returns>
		public static List<Register> Create(float value, ushort address, bool isInput = false)
		{
			if (address + 1 > Consts.MaxAddress)
				throw new ArgumentOutOfRangeException(nameof(address));

			var list = new List<Register>();
			byte[] blob = BitConverter.GetBytes(value);
			if (BitConverter.IsLittleEndian)
				Array.Reverse(blob);

			for (int i = 0; i < blob.Length / 2; i++)
			{
				int bytePos = i * 2;
				list.Add(new Register
				{
					Type = isInput ? ModbusObjectType.InputRegister : ModbusObjectType.HoldingRegister,
					Address = Convert.ToUInt16(address + i),
					HiByte = blob[bytePos],
					LoByte = blob[bytePos + 1]
				});
			}

			return list;
		}

		/// <summary>
		/// Initializes new registers from a double.
		/// </summary>
		/// <param name="value">The double value.</param>
		/// <param name="address">The register address.</param>
		/// <param name="isInput">Flag to create an input register.</param>
		/// <returns></returns>
		public static List<Register> Create(double value, ushort address, bool isInput = false)
		{
			if (address + 3 > Consts.MaxAddress)
				throw new ArgumentOutOfRangeException(nameof(address));

			byte[] blob = BitConverter.GetBytes(value);
			if (BitConverter.IsLittleEndian)
				Array.Reverse(blob);

			var list = new List<Register>();
			for (int i = 0; i < blob.Length / 2; i++)
			{
				int bytePos = i * 2;
				list.Add(new Register
				{
					Type = isInput ? ModbusObjectType.InputRegister : ModbusObjectType.HoldingRegister,
					Address = Convert.ToUInt16(address + i),
					HiByte = blob[bytePos],
					LoByte = blob[bytePos + 1]
				});
			}

			return list;
		}

		#endregion floating point

		#region String

		/// <summary>
		/// Initializes new registers from a string.
		/// </summary>
		/// <param name="str">The string.</param>
		/// <param name="address">The register address.</param>
		/// <param name="encoding">The encoding of the string. Default: <see cref="Encoding.UTF8"/>.</param>
		/// <param name="isInput">Flag to create an input register.</param>
		/// <returns></returns>
		public static List<Register> Create(string str, ushort address, Encoding encoding = null, bool isInput = false)
		{
			if (encoding == null)
				encoding = Encoding.UTF8;

			var list = new List<Register>();
			byte[] blob = encoding.GetBytes(str);
			int numRegister = (int)Math.Ceiling(blob.Length / 2.0);

			if (address + numRegister > Consts.MaxAddress)
				throw new ArgumentOutOfRangeException(nameof(address));

			for (int i = 0; i < numRegister; i++)
			{
				int bytePos = i * 2;
				try
				{
					list.Add(new Register
					{
						Type = isInput ? ModbusObjectType.InputRegister : ModbusObjectType.HoldingRegister,
						Address = Convert.ToUInt16(address + i),
						HiByte = blob[bytePos],
						LoByte = blob[bytePos + 1]
					});
				}
				catch
				{
					list.Add(new Register
					{
						Type = isInput ? ModbusObjectType.InputRegister : ModbusObjectType.HoldingRegister,
						Address = Convert.ToUInt16(address + i),
						HiByte = blob[bytePos]
					});
				}
			}

			return list;
		}

		#endregion String

		#endregion Creates
	}
}
