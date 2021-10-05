using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Amazon.Runtime;

namespace FakeS3
{
    internal sealed class LiteHttpMessageHandler : HttpMessageHandler
    {
        private readonly IBucketStore _bucketStore;
        
        public LiteHttpMessageHandler(IBucketStore bucketStore) => _bucketStore = bucketStore;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Options)
                return DoOptionsAsync(request, cancellationToken);
            var fakeRequest = new FakeS3Request(request);
            
            // TODO: Multipart PUT
            
            return fakeRequest.Type switch
            {
                RequestType.CreateBucket => DoCreateBucketAsync(fakeRequest),
                RequestType.ListBuckets => DoListBucketsAsync(fakeRequest),
                RequestType.LsBucket => DoLsBucketAsync(fakeRequest),
                RequestType.Head => DoHeadAsync(fakeRequest),
                RequestType.Store => DoStoreAsync(fakeRequest),
                RequestType.Copy => DoCopyAsync(fakeRequest),
                RequestType.Get => DoGetAsync(fakeRequest),
                RequestType.GetAcl => DoGetAclAsync(fakeRequest),
                RequestType.SetAcl => DoSetAclAsync(fakeRequest),
                RequestType.DeleteObject => DoDeleteObjectAsync(fakeRequest),
                RequestType.DeleteBucket => DoDeleteBucketAsync(fakeRequest),
                RequestType.DeleteObjects => DoDeleteObjectsAsync(fakeRequest),
                RequestType.Post => DoPostAsync(fakeRequest),
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        private async Task<HttpResponseMessage> DoPostAsync(FakeS3Request fakeRequest)
        {
            var key = fakeRequest.Query["key"];

            if (fakeRequest.Query["uploads"] != null)
            {
                var bytes = new byte[16];
                RandomNumberGenerator.Fill(bytes);
                var uploadId = BitConverter.ToString(bytes).Replace("-", "");
                var result = @$"<?xml version=""1.0"" encoding=""UTF-8""?>
<InitializeMultipartUploadResult>
  <Bucket>{fakeRequest.Bucket}</Bucket>
  <Key>{key}</Key>
  <UploadId>{uploadId}</UploadId>
</InitializeMultipartUploadResult>
";

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Headers =
                    {
                        { "Content-Type", "text/xml" },
                        { "Access-Control-Allow-Origin", "*" },
                        { "Access-Control-Allow-Origin", "*" },
                        { "Access-Control-Allow-Origin", "*" },
                    },
                    Content = new StringContent(result),
                };
            }


            if (fakeRequest.Query["uploadId"] != null)
            {
                var uploadId = fakeRequest.Query["uploadId"];
                var bucket = await _bucketStore.GetBucketAsync(fakeRequest.Bucket);
                /*TODO: _bucketStore.CombineObjectParts(
                    bucket,
                    uploadId,
                    fakeRequest.Object,
                    ParseCompleteMultipartUpload(fakeRequest),
                    fakeRequest);*/
                throw new NotImplementedException();
            }

            if (fakeRequest.HttpRequest.Headers.TryGetValues("Content-Type", out var values) &&
                Regex.IsMatch(values.First(), @"^multipart\/form-data; boundary=(.+)"))
            {
                // TODO: multipart form data/boundary
                throw new NotImplementedException();
            }

            return new HttpResponseMessage(HttpStatusCode.BadRequest);
        }

        private async Task<HttpResponseMessage> DoCreateBucketAsync(FakeS3Request fakeRequest)
        {
            await _bucketStore.CreateBucketAsync(fakeRequest.Bucket);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Headers =
                {
                    { "Content-Type", "text/xml" },
                    { "Access-Control-Allow-Origin", "*" }
                },
                Content = new StringContent("")
            };
        }

        private async Task<HttpResponseMessage> DoListBucketsAsync(FakeS3Request fakeRequest)
        {
            var buckets = _bucketStore.Buckets;
            var result = XmlAdapter.Buckets(buckets.ToArray());
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Headers = { { "Content-Type", "application/xml" } },
                Content = new StringContent(result)
            };
        }

        private async Task<HttpResponseMessage> DoLsBucketAsync(FakeS3Request fakeRequest)
        {
            var bucket = await _bucketStore.GetBucketAsync(fakeRequest.Bucket);
            if (bucket == null)
                return new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Headers = {{"Content-Type", "application/xml"}},
                    Content = new StringContent(XmlAdapter.ErrorNoSuchBucket(fakeRequest.Bucket))
                };
            
            var maxKeysStr = fakeRequest.Query["max-keys"];
            var query = new QueryOptions(
                fakeRequest.Query["marker"] ?? null,
                fakeRequest.Query["prefix"] ?? null,
                fakeRequest.Query["delimiter"] ?? null,
                maxKeysStr == null ? 1000 : int.Parse(maxKeysStr));
                
            var bucketQuery = bucket.QueryForRange(query);
            var result = XmlAdapter.BucketQuery(bucketQuery);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Headers = { { "Content-Type", "application/xml" } },
                Content = new StringContent("")
            };
        }

        private Task<HttpResponseMessage> DoHeadAsync(FakeS3Request fakeRequest)
        {
            throw new NotImplementedException();
        }

        private async Task<HttpResponseMessage> DoStoreAsync(FakeS3Request fakeRequest)
        {
            // lazily create bucket if it doesnt exist... TODO: return proper error

            var bucket = await _bucketStore.GetBucketAsync(fakeRequest.Bucket) ??
                         await _bucketStore.CreateBucketAsync(fakeRequest.Bucket);

            Debug.Assert(fakeRequest.HttpRequest.Content != null);
            
            var objectData = await fakeRequest.HttpRequest.Content.ReadAsByteArrayAsync();
            var storedObject = await _bucketStore.StoreObjectAsync(bucket, fakeRequest.Object, objectData);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Headers =
                {
                    { "Content-Type", "text/xml" },
                    { "Access-Control-Allow-Origin", "*" },
                    { "ETag", $"\"{storedObject?.Md5}\"" }
                },
                Content = new StringContent("")
            };
        }

        private async Task<HttpResponseMessage> DoCopyAsync(FakeS3Request fakeRequest)
        {
            var copiedObject = await _bucketStore.CopyObjectAsync(fakeRequest.SrcBucket,
                fakeRequest.SrcObject, fakeRequest.Bucket, fakeRequest.Object);
            var result = XmlAdapter.CopyObjectResult(copiedObject);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Headers =
                {
                    { "Content-Type", "text/xml" },
                    { "Access-Control-Allow-Origin", "*" }
                },
                Content = new StringContent(result)
            };
        }

        private async Task<HttpResponseMessage> DoGetAsync(FakeS3Request fakeRequest)
        {
            var bucket = await _bucketStore.GetBucketAsync(fakeRequest.Bucket);
            if (bucket == null)
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Headers =
                    {
                        { "Content-Type", "application/xml" },
                        { "Access-Control-Allow-Origin", "*" }
                    },
                    Content = new StringContent(XmlAdapter.ErrorNoSuchBucket(fakeRequest.Bucket))
                };
            }

            var liteObject = await _bucketStore.GetObjectAsync(bucket, fakeRequest.Object);
            if (liteObject == null)
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Headers =
                    {
                        { "Content-Type", "application/xml" },
                        { "Access-Control-Allow-Origin", "*" }
                    },
                    Content = new StringContent(XmlAdapter.ErrorNoSuchKey(fakeRequest.Object))
                };
            }

            if (fakeRequest.HttpRequest.Headers.TryGetValues("If-None-Match", out var ifNoneMatchEnumerable))
            {
                var ifNoneMatch = ifNoneMatchEnumerable.First();
                if (ifNoneMatch == $"\"{liteObject.Md5}\"" || ifNoneMatch == "*")
                    return new HttpResponseMessage(HttpStatusCode.NotModified);
            }

            if (fakeRequest.HttpRequest.Headers.TryGetValues("If-Modified-Since", out var ifModifiedSinceEnumerable))
            {
                var ifModifiedSince =
                    DateTime.ParseExact(ifModifiedSinceEnumerable.First(), "r", null).ToUniversalTime();
                if (ifModifiedSince >= liteObject.Modified)
                    return new HttpResponseMessage(HttpStatusCode.NotModified);
            }

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Headers = {{"Content-Type", liteObject.ContentType}}
            };

            if (liteObject.ContentEncoding != null)
            {
                response.Headers.Add("X-Content-Encoding", liteObject.ContentEncoding);
                response.Headers.Add("Content-Encoding", liteObject.ContentEncoding);
            }

            response.Headers.Add("Content-Disposition", liteObject.ContentDisposition ?? "attachment");
            response.Headers.Add("Last-Modified", liteObject.Modified.ToString("r"));
            response.Headers.Add("Etag", $"\"{liteObject.Md5}\"");
            response.Headers.Add("Accept-Ranges", "bytes");
            response.Headers.Add("Last-Ranges", "bytes");
            response.Headers.Add("Access-Control-Allow_origin", "*");

            foreach (var (key, value) in liteObject.CustomMetadata)
                response.Headers.Add($"x-amz-meta-{key}", value);

            if (response.Headers.TryGetValues("range", out var rangeEnumerable))
            {
                var range = rangeEnumerable.First();
                // TOOD: range query support
                throw new NotImplementedException();
            }
            
            response.Headers.Add("Content-Length", liteObject.Io.ContentSize.ToString());

            if (liteObject.CacheControl != null)
                response.Headers.Add("Cache-Control", liteObject.CacheControl);
            
            if (fakeRequest.HttpMethod == HttpMethod.Head)
            {
                response.Content = new StringContent("");
                liteObject.Io.Dispose();
                return response;
            }

            response.Content = new StreamContent(liteObject.Io.Stream);
            return response;
        }

        private async Task<HttpResponseMessage> DoGetAclAsync(FakeS3Request fakeRequest)
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Headers = { { "Content-Type", "application/xml" } },
                Content = new StringContent(XmlAdapter.Acl())
            };
        }

        private async Task<HttpResponseMessage> DoSetAclAsync(FakeS3Request fakeRequest)
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Headers =
                {
                    { "Content-Type", "text/xml" },
                    { "Access-Control-Allow-Origin", "*" }
                },
                Content = new StringContent("")
            };
        }

        private async Task<HttpResponseMessage> DoDeleteObjectAsync(FakeS3Request fakeRequest)
        {
            // TODO: handle if bucket not found or objects not found
            var bucket = await _bucketStore.GetBucketAsync(fakeRequest.Bucket);
            await _bucketStore.DeleteObjectsAsync(bucket!, fakeRequest.Object);
            return new HttpResponseMessage(HttpStatusCode.NoContent)
            {
                Content = new StringContent("")
            };
        }

        private async Task<HttpResponseMessage> DoDeleteBucketAsync(FakeS3Request fakeRequest)
        {
            await _bucketStore.DeleteBucketAsync(fakeRequest.Bucket);
            return new HttpResponseMessage(HttpStatusCode.NoContent)
            {
                Content = new StringContent("")
            };
        }

        private async Task<HttpResponseMessage> DoDeleteObjectsAsync(FakeS3Request fakeRequest)
        {
            // TODO: handle if bucket not found or objects not found
            var bucket = await _bucketStore.GetBucketAsync(fakeRequest.Bucket);
            var body = await fakeRequest.HttpRequest.Content!.ReadAsStringAsync();
            await _bucketStore.DeleteObjectsAsync(bucket!, XmlAdapter.KeysFromDeleteObjects(body).ToArray());
            return new HttpResponseMessage(HttpStatusCode.NoContent)
            {
                Content = new StringContent("")
            };
        }

        private async Task<HttpResponseMessage> DoOptionsAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            response.Headers.Add("Access-Control-Allow-Methods", "PUT, POST, HEAD, GET, OPTIONS");
            response.Headers.Add("Access-Control-Allow-Headers",
                "Accept, Content-Type, Authorization, Content-Length, ETag, X-CSRF-Token, Content-Disposition");
            response.Headers.Add("Access-Control-Expose-Headers", "Authorization, Content-Length");
            return response;
        }
    }
    
    internal sealed class LiteHttpClientFactory : HttpClientFactory
    {
        private readonly IBucketStore _bucketStore;
        
        public LiteHttpClientFactory(IBucketStore bucketStore) => _bucketStore = bucketStore;

        public override HttpClient CreateHttpClient(IClientConfig clientConfig)
            => new(new LiteHttpMessageHandler(_bucketStore), true);

        public override bool UseSDKHttpClientCaching(IClientConfig clientConfig) => false;
    }
}