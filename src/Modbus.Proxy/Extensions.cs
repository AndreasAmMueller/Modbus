using System;
using System.Threading;

namespace AMWD.Modbus.Proxy
{
	internal static class Extensions
	{
		public static IDisposable GetReadLock(this ReaderWriterLockSlim rwLock, int millisecondsTimeout = -1)
		{
			if (!rwLock.TryEnterReadLock(millisecondsTimeout))
				throw new TimeoutException("Trying to enter a read lock.");

			return new DisposableReaderWriterLockSlim(rwLock, DisposableReaderWriterLockSlim.LockMode.ReadLock);
		}

		public static IDisposable GetReadLock(this ReaderWriterLockSlim rwLock, TimeSpan timeSpan)
		{
			if (!rwLock.TryEnterReadLock(timeSpan))
				throw new TimeoutException("Trying to enter a read lock.");

			return new DisposableReaderWriterLockSlim(rwLock, DisposableReaderWriterLockSlim.LockMode.ReadLock);
		}

		public static IDisposable GetUpgradableReadLock(this ReaderWriterLockSlim rwLock, int millisecondsTimeout = -1)
		{
			if (!rwLock.TryEnterUpgradeableReadLock(millisecondsTimeout))
				throw new TimeoutException("Trying to enter an upgradable read lock.");

			return new DisposableReaderWriterLockSlim(rwLock, DisposableReaderWriterLockSlim.LockMode.UpgradableReadLock);
		}

		public static IDisposable GetUpgradableReadLock(this ReaderWriterLockSlim rwLock, TimeSpan timeSpan)
		{
			if (!rwLock.TryEnterUpgradeableReadLock(timeSpan))
				throw new TimeoutException("Trying to enter an upgradable read lock.");

			return new DisposableReaderWriterLockSlim(rwLock, DisposableReaderWriterLockSlim.LockMode.UpgradableReadLock);
		}

		public static IDisposable GetWriteLock(this ReaderWriterLockSlim rwLock, int millisecondsTimeout = -1)
		{
			if (!rwLock.TryEnterWriteLock(millisecondsTimeout))
				throw new TimeoutException("Trying to enter a write lock.");

			return new DisposableReaderWriterLockSlim(rwLock, DisposableReaderWriterLockSlim.LockMode.WriteLock);
		}

		public static IDisposable GetWriteLock(this ReaderWriterLockSlim rwLock, TimeSpan timeSpan)
		{
			if (!rwLock.TryEnterWriteLock(timeSpan))
				throw new TimeoutException("Trying to enter a write lock.");

			return new DisposableReaderWriterLockSlim(rwLock, DisposableReaderWriterLockSlim.LockMode.WriteLock);
		}

		private class DisposableReaderWriterLockSlim : IDisposable
		{
			private readonly ReaderWriterLockSlim rwLock;
			private LockMode mode;

			public DisposableReaderWriterLockSlim(ReaderWriterLockSlim rwLock, LockMode mode)
			{
				this.rwLock = rwLock;
				this.mode = mode;
			}

			public void Dispose()
			{
				if (rwLock == null || mode == LockMode.None)
					return;

				if (mode == LockMode.ReadLock)
					rwLock.ExitReadLock();

				if (mode == LockMode.UpgradableReadLock && rwLock.IsWriteLockHeld)
					rwLock.ExitWriteLock();

				if (mode == LockMode.UpgradableReadLock)
					rwLock.ExitUpgradeableReadLock();

				if (mode == LockMode.WriteLock)
					rwLock.ExitWriteLock();

				mode = LockMode.None;
			}

			public enum LockMode
			{
				None,
				ReadLock,
				UpgradableReadLock,
				WriteLock
			}
		}
	}
}
