using System.Threading;
using System.Threading.Tasks;
using AMWD.Modbus.Serial.Protocol;

namespace AMWD.Modbus.Serial.Util
{
	internal class RequestTask
	{
		public Request Request { get; set; }

		public TaskCompletionSource<Response> TaskCompletionSource { get; set; }

		public CancellationTokenRegistration Registration { get; set; }
	}
}
