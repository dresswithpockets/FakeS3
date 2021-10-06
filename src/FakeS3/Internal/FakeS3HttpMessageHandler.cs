using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace FakeS3.Internal
{
    [SuppressMessage("ReSharper", "MemberCanBeMadeStatic.Local")]
    internal sealed class FakeS3HttpMessageHandler : HttpMessageHandler
    {
        private readonly IBucketStore _bucketStore;
        
        public FakeS3HttpMessageHandler(IBucketStore bucketStore) => _bucketStore = bucketStore;

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
                var uploadId = bytes.ToHexString();
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
                Debug.Assert(fakeRequest.Bucket != null);
                
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
            Debug.Assert(fakeRequest.Bucket != null);

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

        private Task<HttpResponseMessage> DoListBucketsAsync(FakeS3Request fakeRequest)
        {
            var buckets = _bucketStore.Buckets;
            var result = XmlAdapter.Buckets(buckets.ToArray());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Headers = { { "Content-Type", "application/xml" } },
                Content = new StringContent(result)
            });
        }

        private async Task<HttpResponseMessage> DoLsBucketAsync(FakeS3Request fakeRequest)
        {
            Debug.Assert(fakeRequest.Bucket != null);

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
            Debug.Assert(fakeRequest.Bucket != null);

            // lazily create bucket if it doesnt exist... TODO: return proper error

            var bucket = await _bucketStore.GetBucketAsync(fakeRequest.Bucket) ??
                         await _bucketStore.CreateBucketAsync(fakeRequest.Bucket);

            Debug.Assert(fakeRequest.HttpRequest.Content != null);
            Debug.Assert(fakeRequest.Object != null);

            var objectData = await fakeRequest.HttpRequest.Content.ReadAsByteArrayAsync();
            var storedObject = await _bucketStore.StoreObjectAsync(bucket, fakeRequest.Object, objectData);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Headers =
                {
                    { "Content-Type", "text/xml" },
                    { "Access-Control-Allow-Origin", "*" },
                    { "ETag", $"\"{storedObject.Metadata.Md5}\"" }
                },
                Content = new StringContent("")
            };
        }

        private async Task<HttpResponseMessage> DoCopyAsync(FakeS3Request fakeRequest)
        {
            Debug.Assert(fakeRequest.SrcBucket != null);
            Debug.Assert(fakeRequest.SrcObject != null);
            Debug.Assert(fakeRequest.Bucket != null);
            Debug.Assert(fakeRequest.Object != null);
            
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
            Debug.Assert(fakeRequest.Bucket != null);
            
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
            
            Debug.Assert(fakeRequest.Object != null);

            var @object = await _bucketStore.GetObjectAsync(bucket, fakeRequest.Object);
            if (@object == null)
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
                if (ifNoneMatch == $"\"{@object.Metadata.Md5}\"" || ifNoneMatch == "*")
                    return new HttpResponseMessage(HttpStatusCode.NotModified);
            }

            if (fakeRequest.HttpRequest.Headers.TryGetValues("If-Modified-Since", out var ifModifiedSinceEnumerable))
            {
                var ifModifiedSince =
                    DateTime.ParseExact(ifModifiedSinceEnumerable.First(), "r", null).ToUniversalTime();
                if (ifModifiedSince >= @object.Metadata.Modified)
                    return new HttpResponseMessage(HttpStatusCode.NotModified);
            }

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Headers = {{"Content-Type", @object.Metadata.ContentType}}
            };

            if (@object.Metadata.ContentEncoding != null)
            {
                response.Headers.Add("X-Content-Encoding", @object.Metadata.ContentEncoding);
                response.Headers.Add("Content-Encoding", @object.Metadata.ContentEncoding);
            }

            response.Headers.Add("Content-Disposition", @object.Metadata.ContentDisposition ?? "attachment");
            response.Headers.Add("Last-Modified", @object.Metadata.Modified.ToString("r"));
            response.Headers.Add("Etag", $"\"{@object.Metadata.Md5}\"");
            response.Headers.Add("Accept-Ranges", "bytes");
            response.Headers.Add("Last-Ranges", "bytes");
            response.Headers.Add("Access-Control-Allow_origin", "*");

            foreach (var (key, value) in @object.Metadata.CustomMetadata)
                response.Headers.Add($"x-amz-meta-{key}", value);

            if (response.Headers.TryGetValues("range", out var rangeEnumerable))
            {
                var range = rangeEnumerable.First();
                // TOOD: range query support
                throw new NotImplementedException();
            }
            
            Debug.Assert(@object.Io != null);
            
            response.Headers.Add("Content-Length", @object.Io.ContentSize.ToString());

            if (@object.Metadata.CacheControl != null)
                response.Headers.Add("Cache-Control", @object.Metadata.CacheControl);
            
            if (fakeRequest.HttpMethod == HttpMethod.Head)
            {
                response.Content = new StringContent("");
                @object.Io.Dispose();
                return response;
            }

            response.Content = new StreamContent(@object.Io.Stream);
            return response;
        }

        private Task<HttpResponseMessage> DoGetAclAsync(FakeS3Request fakeRequest)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Headers = { { "Content-Type", "application/xml" } },
                Content = new StringContent(XmlAdapter.Acl())
            });
        }

        private Task<HttpResponseMessage> DoSetAclAsync(FakeS3Request fakeRequest)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Headers =
                {
                    { "Content-Type", "text/xml" },
                    { "Access-Control-Allow-Origin", "*" }
                },
                Content = new StringContent("")
            });
        }

        private async Task<HttpResponseMessage> DoDeleteObjectAsync(FakeS3Request fakeRequest)
        {
            Debug.Assert(fakeRequest.Bucket != null);
            Debug.Assert(fakeRequest.Object != null);
            
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
            Debug.Assert(fakeRequest.Bucket != null);
            
            await _bucketStore.DeleteBucketAsync(fakeRequest.Bucket);
            return new HttpResponseMessage(HttpStatusCode.NoContent)
            {
                Content = new StringContent("")
            };
        }

        private async Task<HttpResponseMessage> DoDeleteObjectsAsync(FakeS3Request fakeRequest)
        {
            Debug.Assert(fakeRequest.Bucket != null);
            
            // TODO: handle if bucket not found or objects not found
            var bucket = await _bucketStore.GetBucketAsync(fakeRequest.Bucket);
            var body = await fakeRequest.HttpRequest.Content!.ReadAsStringAsync();
            await _bucketStore.DeleteObjectsAsync(bucket!, XmlAdapter.KeysFromDeleteObjects(body).ToArray());
            return new HttpResponseMessage(HttpStatusCode.NoContent)
            {
                Content = new StringContent("")
            };
        }

        private Task<HttpResponseMessage> DoOptionsAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            response.Headers.Add("Access-Control-Allow-Methods", "PUT, POST, HEAD, GET, OPTIONS");
            response.Headers.Add("Access-Control-Allow-Headers",
                "Accept, Content-Type, Authorization, Content-Length, ETag, X-CSRF-Token, Content-Disposition");
            response.Headers.Add("Access-Control-Expose-Headers", "Authorization, Content-Length");
            return Task.FromResult(response);
        }
        
        private static async Task<ObjectMetadata> CreateMetadataFromRequestAsync(FakeS3Request request)
        {
            Debug.Assert(request.HttpRequest.Content != null);

            var contentStream = await request.HttpRequest.Content.ReadAsStreamAsync();
            using var md5Hasher = MD5.Create();
            var contentHash = await md5Hasher.ComputeHashAsync(contentStream);

            var contentType = request.HttpRequest.Headers.GetValues("Content-Type").First();
            var contentDisposition = request.HttpRequest.Headers.TryGetValues("Content-Disposition", out var values)
                ? values.First()
                : null;
            var cacheControl = request.HttpRequest.Headers.TryGetValues("Cache-Control", out values)
                ? values.First()
                : null;
            var contentEncoding = request.HttpRequest.Headers.GetValues("Content-Encoding").First();

            var amazonMetadata = new Dictionary<string, string>();
            var customMetadata = new Dictionary<string, string>();

            foreach (var (key, headerValues) in request.HttpRequest.Headers)
            {
                var match = Regex.Match(key, @"^x-amz-([^-]+)-(.*)$");
                if (!match.Success) continue;

                var value = string.Join(", ", headerValues);

                if (match.Groups.Count == 3 && match.Groups[1].Value == "meta")
                {
                    customMetadata.Add(match.Groups[2].Value, value);
                    continue;
                }
                
                amazonMetadata.Add(key.Replace("a-amz-", ""), value);
            }

            return new ObjectMetadata(
                contentHash.ToHexString(),
                contentType,
                contentDisposition,
                cacheControl,
                contentEncoding,
                contentStream.Length,
                DateTime.UtcNow, // TODO: Get Created Time From File/Memory?
                DateTime.UtcNow, // TODO: Get Modified Time From File/Memory?
                amazonMetadata,
                customMetadata
            );
        }
    }
}