using System;
using System.Collections.Generic;
using System.IO;
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

        public IEnumerable<LiteBucket> Buckets { get; }
        public Task<LiteBucket?> GetBucketAsync(string name)
        {
            throw new NotImplementedException();
        }

        public Task<LiteBucket> CreateBucketAsync(string name)
        {
            throw new NotImplementedException();
        }

        public Task DeleteBucketAsync(string name)
        {
            throw new NotImplementedException();
        }

        public Task<LiteObject?> GetObjectAsync(LiteBucket bucket, string objectName)
        {
            throw new NotImplementedException();
        }

        public Task<LiteObject?> GetObjectAsync(string bucketName, string objectName)
        {
            throw new NotImplementedException();
        }

        public Task<(LiteObject source, LiteObject dest)> CopyObjectAsync(string sourceBucketName, string sourceObjectName, string destBucketName, string destObjectName)
        {
            throw new NotImplementedException();
        }

        public Task<LiteObject> StoreObjectAsync(LiteBucket bucket, string objectName, Span<byte> data)
        {
            throw new NotImplementedException();
        }

        public Task<LiteObject> StoreObjectAsync(string bucketName, string objectName, Span<byte> data)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<string>> DeleteObjectsAsync(LiteBucket bucket, params string[] objectNames)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<string>> DeleteObjectsAsync(string bucketName, params string[] objectNames)
        {
            throw new NotImplementedException();
        }
    }
}