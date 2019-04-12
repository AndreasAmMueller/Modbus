using System;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Security.Permissions;

namespace AMWD.Modbus.Serial.Util
{
	/// <summary>
	/// Represents a unix specific IO exception.
	/// Found on https://stackoverflow.com/a/10388107
	/// </summary>
	[Serializable]
	public class UnixIOException : ExternalException
	{
		/// <summary>
		/// Initializes a new instance of a <see cref="UnixIOException"/> class.
		/// </summary>
		[SecurityPermission(SecurityAction.Demand, UnmanagedCode = true)]
		public UnixIOException()
			: this(Marshal.GetLastWin32Error())
		{ }

		/// <summary>
		/// Initializes a new instance of a <see cref="UnixIOException"/> class.
		/// </summary>
		/// <param name="error">The error number.</param>
		[SecurityPermission(SecurityAction.Demand, UnmanagedCode = true)]
		public UnixIOException(int error)
			: this(error, GetErrorMessage(error))
		{ }

		/// <summary>
		/// Initializes a new instance of a <see cref="UnixIOException"/> class.
		/// </summary>
		/// <param name="message">The error message.</param>
		[SecurityPermission(SecurityAction.Demand, UnmanagedCode = true)]
		public UnixIOException(string message)
			: this(Marshal.GetLastWin32Error(), message)
		{ }

		/// <summary>
		/// Initializes a new instance of a <see cref="UnixIOException"/> class.
		/// </summary>
		/// <param name="error">The error number.</param>
		/// <param name="message">The error message.</param>
		public UnixIOException(int error, string message)
			: base(message)
		{
			NativeErrorCode = error;
		}

		/// <summary>
		/// Initializes a new instance of a <see cref="UnixIOException"/> class.
		/// </summary>
		/// <param name="message">The error message.</param>
		/// <param name="innerException">An inner exception.</param>
		public UnixIOException(string message, Exception innerException)
			: base(message, innerException)
		{ }

		/// <summary>
		/// Initializes a new instance of a <see cref="UnixIOException"/> class.
		/// </summary>
		/// <param name="info">The serialization information.</param>
		/// <param name="context">The stream context.</param>
		protected UnixIOException(SerializationInfo info, StreamingContext context)
			: base(info, context)
		{
			NativeErrorCode = info.GetInt32("NativeErrorCode");
		}

		/// <summary>
		/// Gets the native error code set by the unix system.
		/// </summary>
		public int NativeErrorCode { get; }

		/// <inheritdoc/>
		public override void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			if (info == null)
			{
				throw new ArgumentNullException(nameof(info));
			}

			info.AddValue("NativeErrorCode", NativeErrorCode);
			base.GetObjectData(info, context);
		}

		private static string GetErrorMessage(int errno)
		{
			try
			{
				var ptr = UnsafeNativeMethods.StrError(errno);
				return Marshal.PtrToStringAnsi(ptr);
			}
			catch
			{
				return $"Unknown error (0x{errno:x})";
			}
		}
	}
}
