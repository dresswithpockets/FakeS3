using System;
using System.IO;
using System.Threading.Tasks;

namespace FakeS3
{
    internal class RateLimitedFile : IStoreStream
    {
        private readonly FileStream _fileStream;

        public int RateLimit { get; set; }

        public long ContentSize { get; }

        public Stream Stream => _fileStream;

        public RateLimitedFile(string file, int rateLimit)
        {
            ContentSize = new FileInfo(file).Length;
            _fileStream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
            RateLimit = rateLimit;
        }

        public Task<int> ReadAsync(byte[] array, int offset, int count)
            => _fileStream.ReadAsync(array, offset, Math.Min(count, RateLimit));

        public void Dispose()
        {
            _fileStream.Dispose();
            
            GC.SuppressFinalize(this);
        }
    }
}