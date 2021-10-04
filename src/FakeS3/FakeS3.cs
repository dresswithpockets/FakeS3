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

namespace FakeS3
{
    public class FakeS3 : AmazonS3Client
    {
        public static AmazonS3Client CreateFakeClient(IBucketStore bucketStore)
            => new(null, new AmazonS3Config
            {
                HttpClientFactory = new LiteHttpClientFactory(bucketStore),
                CacheHttpClient = false
            });

        public static AmazonS3Client CreateFileClient(string root, bool quietMode)
            => CreateFakeClient(new FileStore(root, quietMode));

        //public static AmazonS3Client CreateMemoryClient() => CreateFakeClient(new MemoryStore());
    }
}