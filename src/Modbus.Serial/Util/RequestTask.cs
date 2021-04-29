using System.Threading;
using System.Threading.Tasks;
using AMWD.Modbus.Serial.Protocol;

namespace AMWD.Modbus.Serial.Util
{
	/// <summary>
	/// Implements a structure to enqueue a request to perform including the option to cancel.
	/// </summary>
	internal class RequestTask
	{
		/// <summary>
		/// Gets or sets the enqueued request.
		/// </summary>
		public Request Request { get; set; }

		/// <summary>
		/// Gets or sets the task completion source to resolve when the request is done.
		/// </summary>
		public TaskCompletionSource<Response> TaskCompletionSource { get; set; }

		/// <summary>
		/// Gets or sets the registration to cancel this request.
		/// </summary>
		public CancellationTokenRegistration Registration { get; set; }
	}
}
