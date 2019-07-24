using System;
using System.Threading;
using System.Threading.Tasks;

namespace AMWD.Modbus.Common.Util
{
	// Source: https://stackoverflow.com/a/21851153
	internal class FakeSynchronizationContext : SynchronizationContext
	{
		private static readonly ThreadLocal<FakeSynchronizationContext> context =
			new ThreadLocal<FakeSynchronizationContext>(() => new FakeSynchronizationContext());

		private FakeSynchronizationContext()
		{
		}

		public static FakeSynchronizationContext Instance => context.Value;

		public static void Execute(Action action)
		{
			var savedContext = Current;
			SetSynchronizationContext(Instance);
			try
			{
				action();
			}
			finally
			{
				SetSynchronizationContext(savedContext);
			}
		}

		public static TResult Execute<TResult>(Func<TResult> action)
		{
			var savedContext = Current;
			SetSynchronizationContext(Instance);
			try
			{
				return action();
			}
			finally
			{
				SetSynchronizationContext(savedContext);
			}
		}

		#region SynchronizationContext methods

		public override SynchronizationContext CreateCopy()
		{
			return this;
		}

		public override void OperationStarted()
		{
			throw new NotImplementedException("OperationStarted");
		}

		public override void OperationCompleted()
		{
			throw new NotImplementedException("OperationCompleted");
		}

		public override void Post(SendOrPostCallback d, object state)
		{
			throw new NotImplementedException("Post");
		}

		public override void Send(SendOrPostCallback d, object state)
		{
			throw new NotImplementedException("Send");
		}

		#endregion SynchronizationContext methods
	}

	/// <summary>
	/// Source: https://stackoverflow.com/a/21851153
	/// </summary>
	public static class FakeSynchronizationContextExtensions
	{
		/// <summary>
		/// Transitions the underlying <see cref="Task"/> into the
		/// <see cref="TaskStatus.RanToCompletion"/> state. The awaiting code is executed
		/// asynchronously so it won't block the caller of this method.
		/// </summary>
		/// <typeparam name="T">The type of the result.</typeparam>
		/// <param name="tcs"></param>
		/// <param name="result">The result value to bind to this <see cref="Task"/>.</param>
		public static void SetResultAsync<T>(this TaskCompletionSource<T> tcs, T result)
		{
			FakeSynchronizationContext.Execute(() => tcs.SetResult(result));
		}

		/// <summary>
		/// Attempts to transition the underlying <see cref="Task"/> into the
		/// <see cref="TaskStatus.RanToCompletion"/> state. The awaiting code is executed
		/// asynchronously so it won't block the caller of this method.
		/// </summary>
		/// <typeparam name="T">The type of the result.</typeparam>
		/// <param name="tcs"></param>
		/// <param name="result">The result value to bind to this <see cref="Task"/>.</param>
		/// <returns>True if the operation was successful; otherwise, false.</returns>
		public static bool TrySetResultAsync<T>(this TaskCompletionSource<T> tcs, T result)
		{
			return FakeSynchronizationContext.Execute(() => tcs.TrySetResult(result));
		}
	}
}
