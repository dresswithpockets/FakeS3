using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Security;
using System.Xml;
using Amazon.Runtime;

namespace FakeS3
{
    internal sealed class FakeS3HttpClientFactory : HttpClientFactory
    {
        private readonly IBucketStore _bucketStore;
        
        public FakeS3HttpClientFactory(IBucketStore bucketStore) => _bucketStore = bucketStore;

        public override HttpClient CreateHttpClient(IClientConfig clientConfig)
            => new(new FakeS3HttpMessageHandler(_bucketStore), true);

        public override bool UseSDKHttpClientCaching(IClientConfig clientConfig) => false;
    }
}