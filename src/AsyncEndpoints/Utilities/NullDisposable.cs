using System;

namespace AsyncEndpoints.Utilities
{
    internal class NullDisposable : IDisposable
    {
        public static readonly NullDisposable Instance = new NullDisposable();
        
        public void Dispose()
        {
            // No-op
        }
    }
}