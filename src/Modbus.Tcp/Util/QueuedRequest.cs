using System.Threading;
using System.Threading.Tasks;
using AMWD.Modbus.Tcp.Protocol;

namespace AMWD.Modbus.Tcp.Util
{
	internal class QueuedRequest
	{
		public ushort TransactionId { get; set; }

		public CancellationTokenRegistration Registration { get; set; }

		public TaskCompletionSource<Response> TaskCompletionSource { get; set; }

		public CancellationTokenSource CancellationTokenSource { get; set; }

		public CancellationTokenSource TimeoutCancellationTokenSource { get; set; }
	}
}
