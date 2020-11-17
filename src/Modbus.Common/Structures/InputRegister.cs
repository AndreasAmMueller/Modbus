using System;
using System.Collections.Generic;
using System.Text;

namespace AMWD.Modbus.Common.Structures
{
	/// <summary>
	/// Represents an input register to keep the modbus typical naming.
	/// </summary>
	/// <remarks>
	/// For programming use the abstract class <see cref="ModbusRegister"/>.
	/// </remarks>
	public class InputRegister : ModbusRegister
	{
		/// <inheritdoc/>
		public override ValueType Type => ValueType.InputRegister;


		#region Creates

		#region unsigned

		/// <summary>
		/// Initializes a new register from a byte.
		/// </summary>
		/// <param name="value">The byte value.</param>
		/// <param name="address">The register address.</param>
		/// <returns></returns>
		public static InputRegister Create(byte value, ushort address)
		{
			return new InputRegister
			{
				Address = address,
				Value = value
			};
		}

		/// <summary>
		/// Initializes a new register from a unsigned short.
		/// </summary>
		/// <param name="value">The uint16 value.</param>
		/// <param name="address">The register address.</param>
		/// <returns></returns>
		public static InputRegister Create(ushort value, ushort address)
		{
			return new InputRegister
			{
				Address = address,
				Value = value
			};
		}

		/// <summary>
		/// Initializes new registers from an unsigned int.
		/// </summary>
		/// <param name="value">The uint32 value.</param>
		/// <param name="address">The register address.</param>
		/// <returns></returns>
		public static List<InputRegister> Create(uint value, ushort address)
		{
			if (address + 1 > Consts.MaxAddress)
			{
				throw new ArgumentOutOfRangeException(nameof(address));
			}

			var list = new List<InputRegister>();
			byte[] blob = BitConverter.GetBytes(value);
			if (BitConverter.IsLittleEndian)
				Array.Reverse(blob);

			for (int i = 0; i < blob.Length / 2; i++)
			{
				list.Add(new InputRegister
				{
					Address = Convert.ToUInt16(address + i),
					HiByte = blob[(i * 2)],
					LoByte = blob[(i * 2) + 1]
				});
			}

			return list;
		}

		/// <summary>
		/// Initializes new registers from an unsigned long.
		/// </summary>
		/// <param name="value">The uint64 value.</param>
		/// <param name="address">The register address.</param>
		/// <returns></returns>
		public static List<InputRegister> Create(ulong value, ushort address)
		{
			if (address + 3 > Consts.MaxAddress)
				throw new ArgumentOutOfRangeException(nameof(address));

			var list = new List<InputRegister>();
			byte[] blob = BitConverter.GetBytes(value);
			if (BitConverter.IsLittleEndian)
				Array.Reverse(blob);

			for (int i = 0; i < blob.Length / 2; i++)
			{
				list.Add(new InputRegister
				{
					Address = Convert.ToUInt16(address + i),
					HiByte = blob[(i * 2)],
					LoByte = blob[(i * 2) + 1]
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
		/// <returns></returns>
		public static InputRegister Create(sbyte value, ushort address)
		{
			return new InputRegister
			{
				Address = address,
				Value = (ushort)value
			};
		}

		/// <summary>
		/// Initializes a new register from a short.
		/// </summary>
		/// <param name="value">The int16 value.</param>
		/// <param name="address">The register address.</param>
		/// <returns></returns>
		public static InputRegister Create(short value, ushort address)
		{
			byte[] blob = BitConverter.GetBytes(value);
			if (BitConverter.IsLittleEndian)
				Array.Reverse(blob);

			return new InputRegister
			{
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
		/// <returns></returns>
		public static List<InputRegister> Create(int value, ushort address)
		{
			if (address + 1 > Consts.MaxAddress)
				throw new ArgumentOutOfRangeException(nameof(address));

			var list = new List<InputRegister>();
			byte[] blob = BitConverter.GetBytes(value);
			if (BitConverter.IsLittleEndian)
				Array.Reverse(blob);

			for (int i = 0; i < blob.Length / 2; i++)
			{
				list.Add(new InputRegister
				{
					Address = Convert.ToUInt16(address + i),
					HiByte = blob[(i * 2)],
					LoByte = blob[(i * 2) + 1]
				});
			}

			return list;
		}

		/// <summary>
		/// Initializes new registers from a long.
		/// </summary>
		/// <param name="value">The int64 value.</param>
		/// <param name="address">The register address.</param>
		/// <returns></returns>
		public static List<InputRegister> Create(long value, ushort address)
		{
			if (address + 3 > Consts.MaxAddress)
				throw new ArgumentOutOfRangeException(nameof(address));

			var list = new List<InputRegister>();
			byte[] blob = BitConverter.GetBytes(value);
			if (BitConverter.IsLittleEndian)
				Array.Reverse(blob);

			for (int i = 0; i < blob.Length / 2; i++)
			{
				list.Add(new InputRegister
				{
					Address = Convert.ToUInt16(address + i),
					HiByte = blob[(i * 2)],
					LoByte = blob[(i * 2) + 1]
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
		/// <returns></returns>
		public static List<InputRegister> Create(float value, ushort address)
		{
			if (address + 1 > Consts.MaxAddress)
				throw new ArgumentOutOfRangeException(nameof(address));

			var list = new List<InputRegister>();
			byte[] blob = BitConverter.GetBytes(value);
			if (BitConverter.IsLittleEndian)
				Array.Reverse(blob);

			for (int i = 0; i < blob.Length / 2; i++)
			{
				list.Add(new InputRegister
				{
					Address = Convert.ToUInt16(address + i),
					HiByte = blob[(i * 2)],
					LoByte = blob[(i * 2) + 1]
				});
			}

			return list;
		}

		/// <summary>
		/// Initializes new registers from a double.
		/// </summary>
		/// <param name="value">The double value.</param>
		/// <param name="address">The register address.</param>
		/// <returns></returns>
		public static List<InputRegister> Create(double value, ushort address)
		{
			if (address + 3 > Consts.MaxAddress)
				throw new ArgumentOutOfRangeException(nameof(address));

			byte[] blob = BitConverter.GetBytes(value);
			if (BitConverter.IsLittleEndian)
				Array.Reverse(blob);

			var list = new List<InputRegister>();
			for (int i = 0; i < blob.Length / 2; i++)
			{
				list.Add(new InputRegister
				{
					Address = Convert.ToUInt16(address + i),
					HiByte = blob[(i * 2)],
					LoByte = blob[(i * 2) + 1]
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
		/// <returns></returns>
		public static List<InputRegister> Create(string str, ushort address, Encoding encoding = null)
		{
			if (encoding == null)
				encoding = Encoding.UTF8;

			var list = new List<InputRegister>();
			byte[] blob = encoding.GetBytes(str);
			int numRegister = (int)Math.Ceiling(blob.Length / 2.0);

			if (address + numRegister > Consts.MaxAddress)
				throw new ArgumentOutOfRangeException(nameof(address));

			for (int i = 0; i < numRegister; i++)
			{
				try
				{
					list.Add(new InputRegister
					{
						Address = Convert.ToUInt16(address + i),
						HiByte = blob[(i * 2)],
						LoByte = blob[(i * 2) + 1]
					});
				}
				catch
				{
					list.Add(new InputRegister
					{
						Address = Convert.ToUInt16(address + i),
						HiByte = blob[(i * 2)]
					});
				}
			}

			return list;
		}

		#endregion String

		#endregion Creates

		#region Overrides

		/// <inheritdoc/>
		public override string ToString()
		{
			return $"Input Register #{Address} | Hi: {HiByte:X2} Lo: {LoByte:X2} | {Value}";
		}

		#endregion Overrides
	}
}
