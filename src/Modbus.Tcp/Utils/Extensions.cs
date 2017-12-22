using System.Threading.Tasks;

namespace Modbus.Tcp.Utils
{
	/// <summary>
	/// Contains some extensions to handle some features more easily.
	/// </summary>
	internal static class Extensions
	{
		#region Task handling

		/// <summary>
		/// Forgets about the result of the task. (Prevent compiler warning).
		/// </summary>
		/// <param name="task">The task to forget.</param>
		public async static void Forget(this Task task)
		{
			await task;
		}

		#endregion Task handling
	}
}
