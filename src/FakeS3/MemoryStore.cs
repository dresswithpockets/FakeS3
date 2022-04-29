using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using FakeS3.Internal;
using Object = FakeS3.Internal.Object;

namespace FakeS3
{
    /// <summary>
    /// An <see cref="IBucketStore"/> implementation that stores buckets in-memory
    /// </summary>
    public class MemoryStore : IBucketStore
    {
        private readonly List<IBucket> _buckets = new();
        private readonly Dictionary<string, IBucket> _bucketsMap = new();
        private readonly Dictionary<string, Dictionary<string, RateLimitedMemoryStream>> _memoryMap = new();

        /// <inheritdoc />
        public void Dispose()
        {
            foreach (var (_, memory) in _memoryMap.SelectMany(m => m.Value))
                memory.Dispose();

            GC.SuppressFinalize(this);
        }

        /// <inheritdoc />
        public IEnumerable<IBucket> Buckets => _buckets;

        /// <inheritdoc />
        public Task<IBucket?> GetBucketAsync(string name)
            => Task.FromResult(_bucketsMap.TryGetValue(name, out var bucket) ? bucket : null);

        /// <inheritdoc />
        public Task<IBucket> CreateBucketAsync(string name)
        {
            if (_bucketsMap.TryGetValue(name, out var bucket))
                throw new ArgumentException("A bucket with the same name already exists", nameof(name));

            _memoryMap.Add(name, new Dictionary<string, RateLimitedMemoryStream>());
            
            bucket = new Bucket(name, DateTime.UtcNow, Enumerable.Empty<IObject>());
            _buckets.Add(bucket);
            _bucketsMap.Add(name, bucket);
            return Task.FromResult(bucket);
        }

        /// <inheritdoc />
        public Task DeleteBucketAsync(string name)
        {
            if (!_bucketsMap.TryGetValue(name, out var bucket))
                throw new ArgumentException("A bucket with that name does not exist", nameof(name));

            if (bucket.Objects.Count > 0)
                throw new BucketNotEmptyException(name);

            foreach (var (_, memory) in _memoryMap[name])
                memory.Dispose();
            _memoryMap.Remove(name);
            
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task<IObject?> GetObjectAsync(IBucket bucket, string objectName)
        {
            if (!_memoryMap[bucket.Name].TryGetValue(objectName, out var memoryStream))
                return Task.FromResult<IObject?>(null);

            return bucket.Find(objectName) is not Object realObject
                ? Task.FromResult<IObject?>(null)
                : Task.FromResult<IObject?>(realObject with {Io = new RateLimitedMemoryStream(memoryStream.Stream)});
        }

        /// <inheritdoc />
        public Task<IObject?> GetObjectAsync(string bucketName, string objectName)
        {
            if (!_bucketsMap.TryGetValue(bucketName, out var bucket))
                throw new ArgumentException("A bucket with that name does not exist", nameof(bucketName));

            return GetObjectAsync(bucket, objectName);
        }

        /// <inheritdoc />
        public async Task<IObject> CopyObjectAsync(
            string sourceBucketName,
            string sourceObjectName,
            string destBucketName,
            string destObjectName)
        {
            var srcObject = await GetObjectAsync(sourceBucketName, sourceObjectName);
            var destBucket = await GetBucketAsync(sourceBucketName);
            
            // TODO: throw exceptions if the buckets arent found?

            var srcBucketMemory = _memoryMap[sourceBucketName];
            if (!_memoryMap.TryGetValue(destBucketName, out var destBucketMemory))
                _memoryMap.Add(destBucketName, destBucketMemory = new Dictionary<string, RateLimitedMemoryStream>());
            
            if (!srcBucketMemory.TryGetValue(sourceObjectName, out var srcMemoryStream))
                throw new InvalidOperationException(
                    $"There is no data stored for the object: {sourceBucketName}/{sourceObjectName}");

            if (sourceBucketName != destBucketName)
            {
                try
                {
                    destBucket = await CreateBucketAsync(destBucketName);
                }
                catch (ArgumentException)
                {
                    destBucket = await GetBucketAsync(destBucketName);
                }
            }

            var destObject = destBucket.Find(destObjectName);
            
            if (sourceBucketName != destBucketName || sourceObjectName != destObjectName)
            {
                // do by-memory copy on srcMemoryStream
                if (!destBucketMemory.TryGetValue(destObjectName, out var destMemoryStream))
                    destBucketMemory.Add(destObjectName, destMemoryStream = new RateLimitedMemoryStream());
                
                destMemoryStream.Reset();
                await srcMemoryStream.Stream.CopyToAsync(destMemoryStream.Stream);

                destObject = new Object(destObjectName, null, srcObject.Metadata);
            }

            return destObject;
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

        /// <inheritdoc />
        public Task<IObject> CombineObjectPartsAsync(
            IBucket bucket,
            string uploadId,
            string objectName,
            IEnumerable<PartInfo> parts,
            ObjectMetadata metadata)
        {
            throw new NotImplementedException();
        }
    }
}