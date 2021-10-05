using System;
using System.IO;
using System.Threading.Tasks;

namespace FakeS3
{
    /// <summary>
    /// A wrapper of a Stream of the data associated with a store or object
    /// </summary>
    public interface IStoreStream : IDisposable
    {
        /// <summary>
        /// The data stream for the object
        /// </summary>
        public Stream Stream { get; }

        /// <summary>
        /// The amount of data in bytes of the stream
        /// </summary>
        public long ContentSize { get; }

        /// <summary>
        /// Read data from the stream
        /// </summary>
        /// <param name="array">An array to fill with the data from the stream</param>
        /// <param name="offset">The byte offset to start reading from</param>
        /// <param name="count">The maximum number of bytes to read</param>
        /// <returns>The amount of bytes read</returns>
        Task<int> ReadAsync(byte[] array, int offset, int count);
    }
}