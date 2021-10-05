using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FakeS3
{
    /// <summary>
    /// A store of buckets with which to query or manipulate
    /// </summary>
    public interface IBucketStore : IDisposable
    {
        /// <summary>
        /// All of the buckets in this store
        /// </summary>
        IEnumerable<Bucket> Buckets { get; }

        /// <summary>
        /// Obtains a bucket by name
        /// </summary>
        /// <param name="name">The name of the bucket to search for</param>
        /// <returns>The bucket found. `null` if no bucket is found</returns>
        Task<Bucket?> GetBucketAsync(string name);

        /// <summary>
        /// Create a new bucket with the given name
        /// </summary>
        /// <param name="name">Name of the new bucket</param>
        /// <returns>The newly created bucket</returns>
        /// <exception cref="ArgumentException">A bucket with the same <paramref name="name" /> already exists</exception>
        Task<Bucket> CreateBucketAsync(string name);

        /// <summary>
        /// Delete an existing bucket with the given name
        /// </summary>
        /// <param name="name">Name of the bucket to delete</param>
        /// <exception cref="ArgumentException">There is no bucket with the given <paramref name="name" /></exception>
        /// <exception cref="BucketNotEmptyException">The bucket is not empty and cannot be deleted</exception>
        Task DeleteBucketAsync(string name);

        /// <summary>
        /// Get an existing object with the given name within the bucket provided
        /// </summary>
        /// <param name="bucket">The bucket to query for the object within</param>
        /// <param name="objectName">The name of the object to query for</param>
        /// <returns>The object found. `null` if no object is found within the bucket provided.</returns>
        Task<Object?> GetObjectAsync(Bucket bucket, string objectName);

        /// <summary>
        /// Get an existing object with the given name within the bucket specified by the bucket name provided
        /// </summary>
        /// <param name="bucketName">The name of the bucket to query for the object within</param>
        /// <param name="objectName">The name of the object to query for</param>
        /// <returns>The object found. `null` if no object is found within the bucket provided.</returns>
        /// <exception cref="ArgumentException">There is no bucket with the given <paramref name="bucketName" /></exception>
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