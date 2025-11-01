using System;

namespace AsyncEndpoints.Utilities
{
	internal class NullDisposable : IDisposable
	{
		public static readonly NullDisposable Instance = new();
		private bool _disposed = false;

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposed)
			{
				_disposed = true;
			}
		}
	}
}
