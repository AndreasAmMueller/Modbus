using System;

namespace AMWD.Modbus.Common.Util
{
	/// <summary>
	/// Helper class for checksums.
	/// </summary>
	public static class Checksum
	{
		/// <summary>
		/// Calculates the CRC checksum with 16 bits of an array.
		/// </summary>
		/// <param name="array">The array with data.</param>
		/// <returns>CRC16 Checksum as byte array. [0] = low byte, [1] = high byte.</returns>
		public static byte[] CRC16(this byte[] array)
		{
			return array.CRC16(0, array.Length);
		}

		/// <summary>
		/// Calculates the CRC checksum with 16 bits of an array.
		/// </summary>
		/// <param name="array">The array with data.</param>
		/// <param name="start">The first byte to use.</param>
		/// <param name="length">The number of bytes to use.</param>
		/// <returns>CRC16 Checksum as byte array. [0] = low byte, [1] = high byte.</returns>
		public static byte[] CRC16(this byte[] array, int start, int length)
		{
			if (array == null || array.Length == 0)
			{
				throw new ArgumentNullException(nameof(array));
			}
			if (start < 0 || start >= array.Length)
			{
				throw new ArgumentOutOfRangeException(nameof(start));
			}
			if (length <= 0 || (start + length) > array.Length)
			{
				throw new ArgumentOutOfRangeException(nameof(length));
			}

			ushort crc16 = 0xFFFF;
			byte lsb;

			for (var i = start; i < (start + length); i++)
			{
				crc16 = (ushort)(crc16 ^ array[i]);
				for (var j = 0; j < 8; j++)
				{
					lsb = (byte)(crc16 & 1);
					crc16 = (ushort)(crc16 >> 1);
					if (lsb == 1)
					{
						crc16 = (ushort)(crc16 ^ 0xA001);
					}
				}
			}

			var b = new byte[2];
			b[0] = (byte)crc16;
			b[1] = (byte)(crc16 >> 8);

			return b;
		}
	}
}
