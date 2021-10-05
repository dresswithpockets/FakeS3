using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

namespace FakeS3
{
    public interface IBucketStore : IDisposable
    {
        IEnumerable<LiteBucket> Buckets { get; }

        Task<LiteBucket?> GetBucketAsync(string name);

        Task<LiteBucket> CreateBucketAsync(string name);

        Task DeleteBucketAsync(string name);

        Task<LiteObject?> GetObjectAsync(LiteBucket bucket, string objectName);

        Task<LiteObject?> GetObjectAsync(string bucketName, string objectName);

        Task<LiteObject> CopyObjectAsync(
            string sourceBucketName,
            string sourceObjectName,
            string destBucketName,
            string destObjectName);

        Task<LiteObject> StoreObjectAsync(LiteBucket bucket, string objectName, Span<byte> data);

        Task<LiteObject> StoreObjectAsync(string bucketName, string objectName, Span<byte> data);
        
        // TODO: CombineObjectPartsAsync

        Task<IEnumerable<string>> DeleteObjectsAsync(LiteBucket bucket, params string[] objectNames);

        Task<IEnumerable<string>> DeleteObjectsAsync(string bucketName, params string[] objectNames);
    }
}