using System;
using System.Text;

namespace Modbus.Tcp.Utils
{
	/// <summary>
	/// Implements a more flexible handling of a byte array.
	/// </summary>
	internal class DataBuffer
	{
		#region Fields

		private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

		#endregion Fields

		#region Constructors

		/// <summary>
		/// Initializes a new instance of the <see cref="DataBuffer"/> class.
		/// </summary>
		public DataBuffer()
		{
			Buffer = new byte[0];
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="DataBuffer"/> class with the buffer length given.
		/// </summary>
		/// <param name="byteCount">Length of the new buffer.</param>
		public DataBuffer(int byteCount)
		{
			if (byteCount < 0)
				throw new ArgumentOutOfRangeException(nameof(byteCount));

			Buffer = new byte[byteCount];
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="DataBuffer"/> class using the bytes a existing buffer.
		/// </summary>
		/// <param name="bytes">New buffer as byte array.</param>
		public DataBuffer(byte[] bytes)
		{
			Buffer = bytes ?? throw new ArgumentNullException(nameof(bytes));
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="DataBuffer"/> class as copy of an existing one.
		/// </summary>
		/// <param name="buffer">The DataBuffer to copy.</param>
		public DataBuffer(DataBuffer buffer)
		{
			if (buffer == null)
				throw new ArgumentNullException(nameof(buffer));

			Buffer = new byte[buffer.Length];
			Array.Copy(buffer.Buffer, Buffer, Length);
		}

		#endregion Constructors

		#region Properties

		/// <summary>
		/// Gets the buffer as byte array.
		/// </summary>
		public byte[] Buffer { get; private set; }

		/// <summary>
		/// Gets the length of the buffer.
		/// </summary>
		public int Length => Buffer.Length;

		/// <summary>
		/// Gets or sets a value that indicates whether the values are stored little- or bigendian.
		/// </summary>
		public bool IsLittleEndian { get; set; }

		/// <summary>
		/// Gets or sets a byte at the index.
		/// </summary>
		/// <param name="index">The index to read/write the byte.</param>
		/// <returns>The value.</returns>
		public byte this[int index]
		{
			get
			{
				return GetByte(index);
			}
			set
			{
				SetByte(index, value);
			}
		}

		#endregion Properties

		#region Setter

		/// <summary>
		/// Sets the bytes at the specified position.
		/// </summary>
		/// <param name="index">The index to start with the bytes.</param>
		/// <param name="bytes">The bytes to set.</param>
		public void SetBytes(int index, byte[] bytes)
		{
			if (index < 0 || Length <= index)
				throw new ArgumentOutOfRangeException(nameof(index));
			if (Length < index + bytes.Length)
				throw new ArgumentOutOfRangeException("Buffer too small.");

			Array.Copy(bytes, 0, Buffer, index, bytes.Length);
		}

		/// <summary>
		/// Sets the bytes at the specified position.
		/// </summary>
		/// <param name="index">The index to start with the bytes.</param>
		/// <param name="block">The DataBlock for the bytes.</param>
		public void SetBytes(int index, DataBuffer block)
		{
			SetBytes(index, block.Buffer);
		}

		#region Unsigned

		/// <summary>
		/// Sets a byte at the specified position.
		/// </summary>
		/// <param name="index">The index.</param>
		/// <param name="value">The value.</param>
		public void SetByte(int index, byte value)
		{
			if (index < 0 || Length <= index)
				throw new ArgumentOutOfRangeException(nameof(index));

			Buffer[index] = value;
		}

		/// <summary>
		/// Sets a boolean at the specific position.
		/// </summary>
		/// <param name="index">The index.</param>
		/// <param name="value">The value.</param>
		public void SetBoolean(int index, bool value)
		{
			SetByte(index, (byte)(value ? 1 : 0));
		}

		/// <summary>
		/// Sets a unsigned short at the specified position.
		/// </summary>
		/// <param name="index">The index.</param>
		/// <param name="value">The value.</param>
		public void SetUInt16(int index, ushort value)
		{
			byte[] blob = BitConverter.GetBytes(value);
			InternalSwap(blob);
			SetBytes(index, blob);
		}

		/// <summary>
		/// Sets an unsigned int at the specified position.
		/// </summary>
		/// <param name="index">The index.</param>
		/// <param name="value">The value.</param>
		public void SetUInt32(int index, uint value)
		{
			byte[] blob = BitConverter.GetBytes(value);
			InternalSwap(blob);
			SetBytes(index, blob);
		}

		/// <summary>
		/// Sets a unsigned long at the specified position.
		/// </summary>
		/// <param name="index">The index.</param>
		/// <param name="value">The value.</param>
		public void SetUInt64(int index, ulong value)
		{
			byte[] blob = BitConverter.GetBytes(value);
			InternalSwap(blob);
			SetBytes(index, blob);
		}

		#endregion Unsigned

		#region Signed

		/// <summary>
		/// Sets an sbyte at the specified position.
		/// </summary>
		/// <param name="index">The index.</param>
		/// <param name="value">The value.</param>
		public void SetSByte(int index, sbyte value)
		{
			if (index < 0 || Length <= index)
				throw new ArgumentOutOfRangeException(nameof(index));

			Buffer[index] = Convert.ToByte(value);
		}

		/// <summary>
		/// Sets a short at the specified position.
		/// </summary>
		/// <param name="index">The index.</param>
		/// <param name="value">The value.</param>
		public void SetInt16(int index, short value)
		{
			byte[] blob = BitConverter.GetBytes(value);
			InternalSwap(blob);
			SetBytes(index, blob);
		}

		/// <summary>
		/// Sets an int at the specified position.
		/// </summary>
		/// <param name="index">The index.</param>
		/// <param name="value">The value.</param>
		public void SetInt32(int index, int value)
		{
			byte[] blob = BitConverter.GetBytes(value);
			InternalSwap(blob);
			SetBytes(index, blob);
		}

		/// <summary>
		/// Sets a long at the specified position.
		/// </summary>
		/// <param name="index">The index.</param>
		/// <param name="value">The value.</param>
		public void SetInt64(int index, long value)
		{
			byte[] blob = BitConverter.GetBytes(value);
			InternalSwap(blob);
			SetBytes(index, blob);
		}

		#endregion Signed

		#region Floating point

		/// <summary>
		/// Sets a float at the specified position.
		/// </summary>
		/// <param name="index">The index.</param>
		/// <param name="value">The value.</param>
		public void SetSingle(int index, float value)
		{
			byte[] blob = BitConverter.GetBytes(value);
			InternalSwap(blob);
			SetBytes(index, blob);
		}

		/// <summary>
		/// Sets a double at the specified position.
		/// </summary>
		/// <param name="index">The index.</param>
		/// <param name="value">The value.</param>
		public void SetDouble(int index, double value)
		{
			byte[] blob = BitConverter.GetBytes(value);
			InternalSwap(blob);
			SetBytes(index, blob);
		}

		#endregion Floating point

		#region Date and Time

		/// <summary>
		/// Sets a timespan (long) at the specified position.
		/// </summary>
		/// <param name="index">The index.</param>
		/// <param name="value">The value.</param>
		public void SetTimeSpan(int index, TimeSpan value)
		{
			SetInt64(index, value.Ticks);
		}

		/// <summary>
		/// Sets a timestamp (DateTime => TimeSpan) at the specified position.
		/// </summary>
		/// <param name="index">The index.</param>
		/// <param name="value">The value.</param>
		public void SetDateTime(int index, DateTime value)
		{
			var dt = value.ToUniversalTime();
			var ts = value.Subtract(UnixEpoch);
			SetTimeSpan(index, ts);
		}

		#endregion Date and Time

		#region Text

		/// <summary>
		/// Sets a char at the specified position.
		/// </summary>
		/// <param name="index">The index.</param>
		/// <param name="value">The value.</param>
		public void SetChar(int index, char value)
		{
			SetByte(index, Convert.ToByte(value));
		}

		/// <summary>
		/// Sets a string at the specified position.
		/// </summary>
		/// <param name="index">The index to start.</param>
		/// <param name="value">The value.</param>
		/// <param name="encoding">The encoding to use. (Default: <see cref="Encoding.UTF8"/>)</param>
		/// <returns>The number of needed bytes.</returns>
		public int SetString(int index, string value, Encoding encoding = null)
		{
			if (encoding == null)
				encoding = Encoding.UTF8;

			byte[] blob = encoding.GetBytes(value);
			SetBytes(index, blob);
			return blob.Length;
		}

		#endregion Text

		#endregion Setter

		#region Add

		#region Unsigned

		/// <summary>
		/// Adds the bytes.
		/// </summary>
		/// <param name="bytes">The bytes.</param>
		public void AddBytes(byte[] bytes)
		{
			var newBytes = new byte[Length + bytes.Length];
			Array.Copy(Buffer, 0, newBytes, 0, Length);
			Array.Copy(bytes, 0, newBytes, Length, bytes.Length);

			Buffer = newBytes;
		}

		/// <summary>
		/// Adds a byte.
		/// </summary>
		/// <param name="value">The value.</param>
		public void AddByte(byte value)
		{
			AddBytes(new[] { value });
		}

		/// <summary>
		/// Adds a boolean.
		/// </summary>
		/// <param name="value">The value.</param>
		public void AddBoolean(bool value)
		{
			AddByte((byte)(value ? 1 : 0));
		}

		/// <summary>
		/// Adds an unsigned short.
		/// </summary>
		/// <param name="value">The Value.</param>
		public void AddUInt16(ushort value)
		{
			var blob = BitConverter.GetBytes(value);
			InternalSwap(blob);
			AddBytes(blob);
		}

		/// <summary>
		/// Adds an unsigned integer.
		/// </summary>
		/// <param name="value">The value.</param>
		public void AddUInt32(uint value)
		{
			var blob = BitConverter.GetBytes(value);
			InternalSwap(blob);
			AddBytes(blob);
		}

		/// <summary>
		/// Adds an unsigned long.
		/// </summary>
		/// <param name="value">The value.</param>
		public void AddUInt64(ulong value)
		{
			var blob = BitConverter.GetBytes(value);
			InternalSwap(blob);
			AddBytes(blob);
		}

		#endregion Unsigned

		#region Signed

		/// <summary>
		/// Adds a signed byte.
		/// </summary>
		/// <param name="value">The value.</param>
		public void AddSByte(sbyte value)
		{
			AddBytes(new[] { Convert.ToByte(value) });
		}

		/// <summary>
		/// Adds a short.
		/// </summary>
		/// <param name="value">The value.</param>
		public void AddInt16(short value)
		{
			var blob = BitConverter.GetBytes(value);
			InternalSwap(blob);
			AddBytes(blob);
		}

		/// <summary>
		/// Adds an integer.
		/// </summary>
		/// <param name="value">The value.</param>
		public void AddInt32(int value)
		{
			var blob = BitConverter.GetBytes(value);
			InternalSwap(blob);
			AddBytes(blob);
		}

		/// <summary>
		/// Adds a long.
		/// </summary>
		/// <param name="value">The value.</param>
		public void AddInt64(long value)
		{
			var blob = BitConverter.GetBytes(value);
			InternalSwap(blob);
			AddBytes(blob);
		}

		#endregion Signed

		#region Floating point

		/// <summary>
		/// Adds a float.
		/// </summary>
		/// <param name="value">The vlaue.</param>
		public void AddSingle(float value)
		{
			var blob = BitConverter.GetBytes(value);
			InternalSwap(blob);
			AddBytes(blob);
		}

		/// <summary>
		/// Adds a double.
		/// </summary>
		/// <param name="value">The value.</param>
		public void AddDouble(double value)
		{
			var blob = BitConverter.GetBytes(value);
			InternalSwap(blob);
			AddBytes(blob);
		}

		#endregion Floating point

		#region Date and Time

		/// <summary>
		/// Adds a timespan (long).
		/// </summary>
		/// <param name="value">The value.</param>
		public void AddTimeSpan(TimeSpan value)
		{
			AddInt64(value.Ticks);
		}

		/// <summary>
		/// Adds a timestamp (DateTime => TimeSpan).
		/// </summary>
		/// <param name="value">The value.</param>
		public void AddDateTime(DateTime value)
		{
			var dt = value.ToUniversalTime();
			var ts = value.Subtract(UnixEpoch);
			AddTimeSpan(ts);
		}

		#endregion Date and Time

		#region Text

		/// <summary>
		/// Adds a char.
		/// </summary>
		/// <param name="value">The value.</param>
		public void AddChar(char value)
		{
			AddByte(Convert.ToByte(value));
		}

		/// <summary>
		/// Adds a string.
		/// </summary>
		/// <param name="value">The value.</param>
		/// <param name="encoding">The encoding to use. (Default: <see cref="Encoding.UTF8"/>)</param>
		/// <returns>The number of needed bytes.</returns>
		public int AddString(string value, Encoding encoding = null)
		{
			if (encoding == null)
				encoding = Encoding.UTF8;

			byte[] blob = encoding.GetBytes(value);
			AddBytes(blob);
			return blob.Length;
		}

		#endregion Text

		#endregion Add

		#region Getter

		/// <summary>
		/// Returns a sequence of bytes.
		/// </summary>
		/// <param name="index">The index to start.</param>
		/// <param name="count">The number of bytes.</param>
		/// <returns>The byte sequence.</returns>
		public byte[] GetBytes(int index, int count)
		{
			if (index < 0 || Length <= index)
				throw new ArgumentOutOfRangeException(nameof(index));
			if (Length < index + count)
				throw new ArgumentOutOfRangeException(nameof(count));

			byte[] bytes = new byte[count];
			Array.Copy(Buffer, index, bytes, 0, count);

			return bytes;
		}

		#region Unsigned

		/// <summary>
		/// Returns a byte.
		/// </summary>
		/// <param name="index">The index.</param>
		/// <returns>The byte.</returns>
		public byte GetByte(int index)
		{
			if (index < 0 || Length <= index)
				throw new ArgumentOutOfRangeException(nameof(index));

			return Buffer[index];
		}

		/// <summary>
		/// Returns a boolean.
		/// </summary>
		/// <param name="index">The index.</param>
		/// <returns>The boolean.</returns>
		public bool GetBoolean(int index)
		{
			return GetByte(index) > 0;
		}

		/// <summary>
		/// Returns a unsigned short.
		/// </summary>
		/// <param name="index">The index.</param>
		/// <returns>The value.</returns>
		public ushort GetUInt16(int index)
		{
			byte[] blob = GetBytes(index, 2);
			InternalSwap(blob);
			return BitConverter.ToUInt16(blob, 0);
		}

		/// <summary>
		/// Returns a unsigned int.
		/// </summary>
		/// <param name="index">The index.</param>
		/// <returns>The value.</returns>
		public uint GetUInt32(int index)
		{
			byte[] blob = GetBytes(index, 4);
			InternalSwap(blob);
			return BitConverter.ToUInt32(blob, 0);
		}

		/// <summary>
		/// Returns a unsigned long.
		/// </summary>
		/// <param name="index">The index.</param>
		/// <returns>The value.</returns>
		public ulong GetUInt64(int index)
		{
			byte[] blob = GetBytes(index, 8);
			InternalSwap(blob);
			return BitConverter.ToUInt64(blob, 0);
		}

		#endregion Unsigned

		#region Signed

		/// <summary>
		/// Returns a signed byte.
		/// </summary>
		/// <param name="index">The index.</param>
		/// <returns>The value.</returns>
		public sbyte GetSByte(int index)
		{
			if (index < 0 || Length <= index)
				throw new ArgumentOutOfRangeException(nameof(index));

			return Convert.ToSByte(Buffer[index]);
		}

		/// <summary>
		/// Returns a short.
		/// </summary>
		/// <param name="index">The index.</param>
		/// <returns>The value.</returns>
		public short GetInt16(int index)
		{
			byte[] blob = GetBytes(index, 2);
			InternalSwap(blob);
			return BitConverter.ToInt16(blob, 0);
		}

		/// <summary>
		/// Returns a int.
		/// </summary>
		/// <param name="index">The index.</param>
		/// <returns>The value.</returns>
		public int GetInt32(int index)
		{
			byte[] blob = GetBytes(index, 4);
			InternalSwap(blob);
			return BitConverter.ToInt32(blob, 0);
		}

		/// <summary>
		/// Returns a long.
		/// </summary>
		/// <param name="index">The index.</param>
		/// <returns>The value.</returns>
		public long GetInt64(int index)
		{
			byte[] blob = GetBytes(index, 8);
			InternalSwap(blob);
			return BitConverter.ToInt64(blob, 0);
		}

		#endregion Signed

		#region Floating point

		/// <summary>
		/// Returns a float.
		/// </summary>
		/// <param name="index">The index.</param>
		/// <returns>The value.</returns>
		public float GetSingle(int index)
		{
			byte[] blob = GetBytes(index, 4);
			InternalSwap(blob);
			return BitConverter.ToSingle(blob, 0);
		}

		/// <summary>
		/// Returns a double.
		/// </summary>
		/// <param name="index">The index.</param>
		/// <returns>The value.</returns>
		public double GetDouble(int index)
		{
			byte[] blob = GetBytes(index, 8);
			InternalSwap(blob);
			return BitConverter.ToDouble(blob, 0);
		}

		#endregion Floating point

		#region Date and Time

		/// <summary>
		/// Returns a <see cref="TimeSpan"/>.
		/// </summary>
		/// <param name="index">The index.</param>
		/// <returns>The value.</returns>
		public TimeSpan GetTimeSpan(int index)
		{
			return TimeSpan.FromTicks(GetInt64(index));
		}

		/// <summary>
		/// Returns a <see cref="DateTime"/>.
		/// </summary>
		/// <param name="index">The index.</param>
		/// <param name="localTime">The value indicates whether the local time shold be returned.</param>
		/// <returns>The value.</returns>
		public DateTime GetDateTime(int index, bool localTime = false)
		{
			var ts = GetTimeSpan(index);
			var dt = UnixEpoch.Add(ts);
			return localTime ? dt.ToLocalTime() : dt;
		}

		#endregion Date and Time

		#region Text

		/// <summary>
		/// Returns a char.
		/// </summary>
		/// <param name="index">The index.</param>
		/// <returns>The char.</returns>
		public char GetChar(int index)
		{
			return Convert.ToChar(GetByte(index));
		}

		/// <summary>
		/// Returns a string.
		/// </summary>
		/// <param name="index">The index to start.</param>
		/// <param name="count">The number of bytes to use.</param>
		/// <param name="encoding">The encoding to use. (Default: <see cref="Encoding.UTF8"/>)</param>
		/// <returns>The string.</returns>
		public string GetString(int index, int count, Encoding encoding = null)
		{
			if (encoding == null)
				encoding = Encoding.UTF8;

			byte[] blob = GetBytes(index, count);
			return encoding.GetString(blob);
		}

		#endregion Text

		#endregion Getter

		#region Public methods

		/// <summary>
		/// Compares a byte array to a sequence of the buffer.
		/// </summary>
		/// <param name="index">The index to start the comparison.</param>
		/// <param name="bytes">The byte array.</param>
		/// <returns>true if the array equals the sequence otherwise false.</returns>
		public bool IsEqual(int index, byte[] bytes)
		{
			if (Length < index + bytes.Length)
				return false;

			for (int i = 0; i < bytes.Length; i++)
			{
				if (Buffer[index + i] != bytes[i])
					return false;
			}

			return true;
		}

		/// <summary>
		/// Resizes the Buffer.
		/// </summary>
		/// <param name="size">The new size of the Buffer.</param>
		public void ResizeTo(int size)
		{
			if (size < 0)
				throw new ArgumentOutOfRangeException(nameof(size));

			byte[] newBuffer = new byte[size];
			int len = Math.Min(size, Length);
			Array.Copy(Buffer, newBuffer, len);
			Buffer = newBuffer;
		}

		#endregion Public methods

		#region Overrides

		/// <inheritdoc/>
		public override bool Equals(object obj)
		{
			var block = obj as DataBuffer;
			if (block == null)
				return false;

			if (block.IsLittleEndian != IsLittleEndian)
				return false;

			if (block.Length != Length)
				return false;

			return IsEqual(0, block.Buffer);
		}

		/// <inheritdoc/>
		public override int GetHashCode()
		{
			return base.GetHashCode()
				^ Length.GetHashCode()
				^ Buffer.GetHashCode()
				^ IsLittleEndian.GetHashCode();
		}

		/// <inheritdoc/>
		public override string ToString()
		{
			var sb = new StringBuilder();
			sb.AppendLine($"DataBuffer | Length: {Length} Bytes | LittleEndian: {IsLittleEndian}");

			sb.Append("         0  1  2  3  4  5  6  7  8  9  A  B  C  D  E  F");
			for (int i = 0; i < Length; i++)
			{
				if (i % 16 == 0)
				{
					sb.AppendLine();
					sb.Append("0x" + i.ToString("X4"));
					sb.Append(" ");
				}
				var hex = Buffer[i].ToString("X2");
				sb.Append($" {hex}");
			}
			sb.AppendLine();

			return sb.ToString();
		}

		#endregion Overrides

		#region Protected methods

		/// <summary>
		/// Swaps the bytes when needed for the <see cref="BitConverter"/>.
		/// </summary>
		/// <param name="array">The byte array.</param>
		protected virtual void InternalSwap(byte[] array)
		{
			if (IsLittleEndian != BitConverter.IsLittleEndian)
			{
				Array.Reverse(array);
			}
		}

		#endregion Protected methods
	}
}
