using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace AMWD.Modbus.Tcp.Util
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
		public static async void Forget(this Task task)
		{
			try
			{
				await task;
			}
			catch
			{ /* Task forgotten, so keep everything quiet. */ }
		}

		#endregion Task handling
	}
}
