using Amazon.S3;
using FakeS3.AWS.Internal;

namespace FakeS3.AWS
{
    public static class FakeS3
    {
        public static IAmazonS3 CreateFakeClient(IBucketStore bucketStore)
            => new AmazonS3Client(null, new AmazonS3Config
            {
                HttpClientFactory = new FakeS3HttpClientFactory(bucketStore),
                CacheHttpClient = false
            });

        public static IAmazonS3 CreateFileClient(string root, bool quietMode)
            => CreateFakeClient(new FileStore(root, quietMode));

        public static IAmazonS3 CreateMemoryClient()
            => CreateFakeClient(new MemoryStore());
    }
}