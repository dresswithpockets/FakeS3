using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FakeS3
{
    public class MemoryStore : IBucketStore
    {
        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public IEnumerable<Bucket> Buckets { get; }
        
        public Task<Bucket?> GetBucketAsync(string name)
        {
            throw new NotImplementedException();
        }

        public Task<Bucket> CreateBucketAsync(string name)
        {
            throw new NotImplementedException();
        }

        public Task DeleteBucketAsync(string name)
        {
            throw new NotImplementedException();
        }

        public Task<Object?> GetObjectAsync(Bucket bucket, string objectName)
        {
            throw new NotImplementedException();
        }

        public Task<Object?> GetObjectAsync(string bucketName, string objectName)
        {
            throw new NotImplementedException();
        }

        public Task<Object> CopyObjectAsync(string sourceBucketName, string sourceObjectName, string destBucketName, string destObjectName)
        {
            throw new NotImplementedException();
        }

        public Task<Object> StoreObjectAsync(Bucket bucket, string objectName, ReadOnlyMemory<byte> data)
        {
            throw new NotImplementedException();
        }

        public Task<Object> StoreObjectAsync(string bucketName, string objectName, ReadOnlyMemory<byte> data)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<string>> DeleteObjectsAsync(Bucket bucket, params string[] objectNames)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<string>> DeleteObjectsAsync(string bucketName, params string[] objectNames)
        {
            throw new NotImplementedException();
        }
    }
}