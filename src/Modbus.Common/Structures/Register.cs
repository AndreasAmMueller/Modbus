using System;
using System.Collections.Generic;
using System.Text;

namespace Modbus.Common.Structures
{
	/// <summary>
	/// Represents a register on a Modbus device.
	/// </summary>
	public class Register
	{
		#region Creates

		#region unsigned

		/// <summary>
		/// Initializes a new register from a byte.
		/// </summary>
		/// <param name="value">The byte value.</param>
		/// <param name="address">The register address.</param>
		/// <returns></returns>
		public static Register Create(byte value, ushort address)
		{
			return new Register
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
		public static Register Create(ushort value, ushort address)
		{
			return new Register
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
		public static List<Register> Create(uint value, ushort address)
		{
			if (address + 1 > Consts.MaxAddress)
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
		/// Initializes new registers from an unsigned long.
		/// </summary>
		/// <param name="value">The uint64 value.</param>
		/// <param name="address">The register address.</param>
		/// <returns></returns>
		public static List<Register> Create(ulong value, ushort address)
		{
			if (address + 3 > Consts.MaxAddress)
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

		#endregion unsigned

		#region signed

		/// <summary>
		/// Initializes a new register from a signed byte.
		/// </summary>
		/// <param name="value">The sbyte value.</param>
		/// <param name="address">The register address.</param>
		/// <returns></returns>
		public static Register Create(sbyte value, ushort address)
		{
			return new Register
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
		public static Register Create(short value, ushort address)
		{
			var blob = BitConverter.GetBytes(value);
			if (BitConverter.IsLittleEndian)
			{
				Array.Reverse(blob);
			}

			return new Register
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
		public static List<Register> Create(int value, ushort address)
		{
			if (address + 1 > Consts.MaxAddress)
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
		/// Initializes new registers from a long.
		/// </summary>
		/// <param name="value">The int64 value.</param>
		/// <param name="address">The register address.</param>
		/// <returns></returns>
		public static List<Register> Create(long value, ushort address)
		{
			if (address + 3 > Consts.MaxAddress)
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

		#endregion signed

		#region floating point

		/// <summary>
		/// Initializes new registers from a float.
		/// </summary>
		/// <param name="value">The single value.</param>
		/// <param name="address">The register address.</param>
		/// <returns></returns>
		public static List<Register> Create(float value, ushort address)
		{
			if (address + 1 > Consts.MaxAddress)
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
		/// Initializes new registers from a double.
		/// </summary>
		/// <param name="value">The double value.</param>
		/// <param name="address">The register address.</param>
		/// <returns></returns>
		public static List<Register> Create(double value, ushort address)
		{
			if (address + 3 > Consts.MaxAddress)
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

		#endregion floating point

		#region String

		/// <summary>
		/// Initializes new registers from a string.
		/// </summary>
		/// <param name="str">The string.</param>
		/// <param name="address">The register address.</param>
		/// <param name="encoding">The encoding of the string. Default: <see cref="Encoding.UTF8"/>.</param>
		/// <returns></returns>
		public static List<Register> Create(string str, ushort address, Encoding encoding = null)
		{
			if (encoding == null)
			{
				encoding = Encoding.UTF8;
			}

			var list = new List<Register>();
			var blob = encoding.GetBytes(str);
			var numRegister = (int)Math.Ceiling(blob.Length / 2.0);

			if (address + numRegister > Consts.MaxAddress)
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

		#endregion

		#endregion Creates

		#region Properties

		/// <summary>
		/// Gets or sets the address.
		/// </summary>
		public ushort Address { get; set; }

		/// <summary>
		/// Gets or sets the High-Byte of the register.
		/// </summary>
		public byte HiByte { get; set; }

		/// <summary>
		/// Gets or sets the Low-Byte of the register.
		/// </summary>
		public byte LoByte { get; set; }

		/// <summary>
		/// Gets or sets the value of the register as WORD.
		/// </summary>
		public ushort Value
		{
			get
			{
				var blob = new[] { HiByte, LoByte };
				if (BitConverter.IsLittleEndian)
				{
					Array.Reverse(blob);
				}
				return BitConverter.ToUInt16(blob, 0);
			}
			set
			{
				var blob = BitConverter.GetBytes(value);
				if (BitConverter.IsLittleEndian)
				{
					Array.Reverse(blob);
				}
				HiByte = blob[0];
				LoByte = blob[1];
			}
		}

		#endregion Properties

		#region Overrides

		/// <inheritdoc/>
		public override string ToString()
		{
			return $"Register#{Address} | Hi: {HiByte.ToString("X2")} Lo: {LoByte.ToString("X2")} | {Value}";
		}

		/// <inheritdoc/>
		public override bool Equals(object obj)
		{
			var reg = obj as Register;
			if (reg == null)
			{
				return false;
			}

			return reg.Address == Address &&
				reg.HiByte == HiByte &&
				reg.LoByte == LoByte;
		}

		/// <inheritdoc/>
		public override int GetHashCode()
		{
			return base.GetHashCode() ^
				Address.GetHashCode() ^
				HiByte.GetHashCode() ^
				LoByte.GetHashCode();
		}

		#endregion Overrides
	}
}
