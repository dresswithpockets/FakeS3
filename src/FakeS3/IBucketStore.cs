using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

namespace FakeS3
{
    public interface IBucketStore : IDisposable
    {
        IEnumerable<Bucket> Buckets { get; }

        Task<Bucket?> GetBucketAsync(string name);

        Task<Bucket> CreateBucketAsync(string name);

        Task DeleteBucketAsync(string name);

        Task<Object?> GetObjectAsync(Bucket bucket, string objectName);

        Task<Object?> GetObjectAsync(string bucketName, string objectName);

        Task<Object> CopyObjectAsync(
            string sourceBucketName,
            string sourceObjectName,
            string destBucketName,
            string destObjectName);

        Task<Object> StoreObjectAsync(Bucket bucket, string objectName, ReadOnlyMemory<byte> data);

        Task<Object> StoreObjectAsync(string bucketName, string objectName, ReadOnlyMemory<byte> data);
        
        // TODO: CombineObjectPartsAsync

        Task<IEnumerable<string>> DeleteObjectsAsync(Bucket bucket, params string[] objectNames);

        Task<IEnumerable<string>> DeleteObjectsAsync(string bucketName, params string[] objectNames);
    }
}