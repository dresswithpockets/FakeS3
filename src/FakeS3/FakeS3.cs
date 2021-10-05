using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Amazon;
using Amazon.Runtime;
using Amazon.Runtime.Internal;
using Amazon.Runtime.SharedInterfaces;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;

namespace FakeS3
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
