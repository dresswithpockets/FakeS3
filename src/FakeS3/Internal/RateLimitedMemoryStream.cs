using System;
using System.IO;
using System.Threading.Tasks;

namespace FakeS3.Internal
{
    internal class RateLimitedMemoryStream : IStoreStream
    {
        private readonly MemoryStream _stream;

        public int RateLimit { get; set; }

        public long ContentSize => _stream.Length;

        public Stream Stream => _stream;

        public RateLimitedMemoryStream(int initialCapacity = 0) => _stream = new MemoryStream(initialCapacity);

        public Task<int> ReadAsync(byte[] array, int offset, int count)
            => _stream.ReadAsync(array, offset, Math.Min(count, RateLimit));

        public void Reset() => _stream.SetLength(0);

        public void Dispose()
        {
            _stream.Dispose();
            
            GC.SuppressFinalize(this);
        }
    }
}