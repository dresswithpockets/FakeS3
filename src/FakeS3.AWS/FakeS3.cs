using Amazon.S3;
using FakeS3.AWS.Internal;

namespace FakeS3.AWS
{
    /// <summary>
    /// Utilities for creating an instance of <see cref="IAmazonS3"/> for use with implementations of
    /// <see cref="IBucketStore"/>; such as <see cref="FileStore"/> and <see cref="MemoryStore"/>.
    /// </summary>
    public static class FakeS3
    {
        /// <summary>
        /// Create an instance of <see cref="IAmazonS3"/> that uses the provided <see cref="IBucketStore"/> to
        /// query, manipulate, and store S3 buckets and objects.
        /// </summary>
        /// <param name="bucketStore">The <see cref="IBucketStore"/> to store S3 buckets and objects with.</param>
        /// <returns>An instance of <see cref="IAmazonS3"/></returns>
        public static IAmazonS3 CreateClient(IBucketStore bucketStore)
            => new AmazonS3Client(null, new AmazonS3Config
            {
                HttpClientFactory = new FakeS3HttpClientFactory(bucketStore),
                CacheHttpClient = false
            });

        /// <summary>
        /// Creates an instance of <see cref="IAmazonS3"/> that stores S3 buckets on disk as files.
        /// </summary>
        /// <param name="root">The root directory to store S3 buckets and objects in</param>
        /// <param name="quietMode"></param>
        /// <returns>An instance of <see cref="IAmazonS3"/> that stores S3 buckets on disk as files.</returns>
        public static IAmazonS3 CreateFileClient(string root, bool quietMode)
            => CreateClient(new FileStore(root, quietMode));

        /// <summary>
        /// Creates an instance of <see cref="IAmazonS3"/> that stores S3 buckets in memory.
        /// </summary>
        /// <returns>An instance of <see cref="IAmazonS3"/> that stores S3 buckets in memory.</returns>
        public static IAmazonS3 CreateMemoryClient()
            => CreateClient(new MemoryStore());
    }
}