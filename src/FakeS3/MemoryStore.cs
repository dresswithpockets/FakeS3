using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FakeS3
{
    /// <summary>
    /// An <see cref="IBucketStore"/> implementation that stores buckets in-memory
    /// </summary>
    public class MemoryStore : IBucketStore
    {
        /// <inheritdoc />
        public void Dispose()
        {
            GC.SuppressFinalize(this);
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public IEnumerable<IBucket> Buckets => throw new NotImplementedException();

        /// <inheritdoc />
        public Task<IBucket?> GetBucketAsync(string name)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public Task<IBucket> CreateBucketAsync(string name)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public Task DeleteBucketAsync(string name)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public Task<IObject?> GetObjectAsync(IBucket bucket, string objectName)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public Task<IObject?> GetObjectAsync(string bucketName, string objectName)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public Task<IObject> CopyObjectAsync(
            string sourceBucketName,
            string sourceObjectName,
            string destBucketName,
            string destObjectName)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public Task<IObject> StoreObjectAsync(
            IBucket bucket,
            string objectName,
            ReadOnlyMemory<byte> data,
            ObjectMetadata metadata)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public Task<IObject> StoreObjectAsync(
            string bucketName,
            string objectName,
            ReadOnlyMemory<byte> data,
            ObjectMetadata metadata)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public Task<IEnumerable<string>> DeleteObjectsAsync(IBucket bucket, params string[] objectNames)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public Task<IEnumerable<string>> DeleteObjectsAsync(string bucketName, params string[] objectNames)
        {
            throw new NotImplementedException();
        }
    }
}