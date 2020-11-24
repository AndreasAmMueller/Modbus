using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace UnitTests
{
	/// <summary>
	/// Contains some extensions to handle some features more easily.
	/// </summary>
	internal static class Extensions
	{
		#region Enums

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

		public static string GetDescription(this Enum enumValue)
		{
			return enumValue.GetAttribute<DescriptionAttribute>()?.Description ?? enumValue.ToString();
		}

		#endregion Enums

		#region Task handling

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

		#region Exception

		public static string GetMessage(this Exception exception)
		{
			return exception.InnerException?.Message ?? exception.Message;
		}

		#endregion Exception
	}
}
