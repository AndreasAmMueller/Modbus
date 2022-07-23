using System.Threading;
using System.Threading.Tasks;
using AMWD.Modbus.SerialOverTCP.Protocol;

namespace AMWD.Modbus.SerialOverTCP.Util
{
	internal class QueuedRequest
	{

		public CancellationTokenRegistration Registration { get; set; }

		public TaskCompletionSource<Response> TaskCompletionSource { get; set; }

		public CancellationTokenSource CancellationTokenSource { get; set; }

		public CancellationTokenSource TimeoutCancellationTokenSource { get; set; }
	}
}
