using System;
using System.ComponentModel;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace AMWD.Modbus.Serial.Util
{
	/// <summary>
	/// Contains some extensions to handle some features more easily.
	/// </summary>
	internal static class Extensions
	{
		#region Enums

		/// <summary>
		/// Tries to return an attribute of an enum value.
		/// </summary>
		/// <typeparam name="T">The attribute type.</typeparam>
		/// <param name="enumValue">The enum value.</param>
		/// <returns>The first attribute of the type present or null.</returns>
		public static T GetAttribute<T>(this Enum enumValue)
			where T : Attribute
		{
			if (enumValue != null)
			{
				var fi = enumValue.GetType().GetField(enumValue.ToString());
				var attrs = (T[])fi?.GetCustomAttributes(typeof(T), inherit: false);
				return attrs?.FirstOrDefault();
			}
			return default(T);
		}

		/// <summary>
		/// Tries to read the description of an enum value.
		/// </summary>
		/// <param name="enumValue">The enum value.</param>
		/// <returns>The description or the <see cref="Enum.ToString()"/></returns>
		public static string GetDescription(this Enum enumValue)
		{
			return enumValue.GetAttribute<DescriptionAttribute>()?.Description ?? enumValue.ToString();
		}

		#endregion Enums

		#region Task handling

		/// <summary>
		/// Forgets about the result of the task. (Prevent compiler warning).
		/// </summary>
		/// <param name="task">The task to forget.</param>
		internal static async void Forget(this Task task)
		{
			try
			{
				await task;
			}
			catch
			{ /* Task forgotten, so keep everything quiet. */ }
		}

		#endregion Task handling

		#region Async fixes

		// idea found on: https://stackoverflow.com/a/54610437/11906695
		internal static async Task<int> ReadAsync(this SerialPort serialPort, byte[] buffer, int offset, int count, CancellationToken cancellationToken)
		{
			// serial port read/write timeouts seem to be ignored, so ensure the timeouts.
			using (var cts = new CancellationTokenSource(serialPort.ReadTimeout))
			using (cancellationToken.Register(() => cts.Cancel()))
			{
				var ctr = default(CancellationTokenRegistration);
				if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				{
					// The async stream implementation on windows is a bit broken.
					// this kicks it back to us.
					ctr = cts.Token.Register(() => serialPort.DiscardInBuffer());
				}

				try
				{
					return await serialPort.BaseStream.ReadAsync(buffer, offset, count, cts.Token);
				}
				catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
				{
					cancellationToken.ThrowIfCancellationRequested();
					return 0;
				}
				catch (OperationCanceledException) when (cts.IsCancellationRequested)
				{
					throw new TimeoutException("No bytes to read within the ReadTimeout.");
				}
				catch (IOException) when (cts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
				{
					throw new TimeoutException("No bytes to read within the ReadTimeout.");
				}
				finally
				{
					ctr.Dispose();
				}
			}
		}

		internal static async Task WriteAsync(this SerialPort serialPort, byte[] buffer, int offset, int count, CancellationToken cancellationToken)
		{
			// serial port read/write timeouts seem to be ignored, so ensure the timeouts.
			using (var cts = new CancellationTokenSource(serialPort.WriteTimeout))
			using (cancellationToken.Register(() => cts.Cancel()))
			{
				var ctr = default(CancellationTokenRegistration);
				if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				{
					// The async stream implementation on windows is a bit broken.
					// this kicks it back to us.
					ctr = cts.Token.Register(() => serialPort.DiscardOutBuffer());
				}

				try
				{
					await serialPort.BaseStream.WriteAsync(buffer, offset, count, cts.Token);
				}
				catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
				{
					cancellationToken.ThrowIfCancellationRequested();
				}
				catch (OperationCanceledException) when (cts.IsCancellationRequested)
				{
					throw new TimeoutException("No bytes written within the WriteTimeout.");
				}
				catch (IOException) when (cts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
				{
					throw new TimeoutException("No bytes written within the WriteTimeout.");
				}
				finally
				{
					ctr.Dispose();
				}
			}
		}

		#endregion Async fixes
	}
}
