using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Xml.Linq;

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

            if (fakeRequest.Query["uploadId"] == null)
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

            return fakeRequest.Type == RequestType.Copy
                ? DoMultipartCopyAsync(fakeRequest)
                : DoMultipartPutAsync(fakeRequest);
        }

        private async Task<HttpResponseMessage> DoMultipartPutAsync(FakeS3Request fakeRequest)
        {
            Debug.Assert(fakeRequest.Bucket != null);
            Debug.Assert(fakeRequest.HttpRequest.Content != null);
            
            var bucket = await _bucketStore.GetBucketAsync(fakeRequest.Bucket) ??
                         await _bucketStore.CreateBucketAsync(fakeRequest.Bucket);

            var data = await fakeRequest.HttpRequest.Content.ReadAsByteArrayAsync();

            var partNumber = fakeRequest.Query["partNumber"];
            var uploadId = fakeRequest.Query["uploadId"];
            var partName = $"{uploadId}_{fakeRequest.Object}_part{partNumber}";

            var realObject = await _bucketStore.StoreObjectAsync(bucket, partName, data,
                await CreateMetadataFromRequestAsync(fakeRequest));

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Headers =
                {
                    { "ETag", $"\"{realObject.Metadata.Md5}\"" },
                    { "Access-Control-Allow-Origin", "*" },
                    { "Access-Control-Allow-Headers", "Accept, Content-Type, Authorization, Content-Length, ETag, X-CSRF-Token, Content-Disposition" },
                    { "Access-Control-Expose-Headers", "ETag" },
                },
                Content = new StringContent("")
            };
        }

        private async Task<HttpResponseMessage> DoMultipartCopyAsync(FakeS3Request fakeRequest)
        {
            var partNumber = fakeRequest.Query["partNumber"];
            var uploadId = fakeRequest.Query["uploadId"];
            var partName = $"{uploadId}_{fakeRequest.Object}_part{partNumber}";

            var realObject = await _bucketStore.CopyObjectAsync(fakeRequest.SrcBucket, fakeRequest.SrcObject,
                fakeRequest.Bucket, partName);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Headers =
                {
                    { "Access-Control-Allow-Origin", "*" },
                    { "Access-Control-Allow-Headers", "Accept, Content-Type, Authorization, Content-Length, ETag, X-CSRF-Token, Content-Disposition" },
                    { "Access-Control-Expose-Headers", "ETag" },
                },
                Content = new StringContent(XmlAdapter.CopyObjectResult(realObject))
                {
                    Headers = { { "Content-Type", "text/xml" } }
                }
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
                        { "Access-Control-Allow-Origin", "*" },
                        { "Access-Control-Allow-Headers", "Accept, Content-Type, Authorization, Content-Length, ETag, X-CSRF-Token, Content-Disposition" },
                        { "Access-Control-Expose-Headers", "ETag" },
                    },
                    Content = new StringContent(result)
                    {
                        Headers = { { "Content-Type", "text/xml" } }
                    },
                };
            }
            
            if (fakeRequest.Query["uploadId"] != null)
            {
                Debug.Assert(fakeRequest.Bucket != null);
                Debug.Assert(fakeRequest.Object != null);
                
                var uploadId = fakeRequest.Query["uploadId"];
                var bucket = await _bucketStore.GetBucketAsync(fakeRequest.Bucket);
                var realObject = await _bucketStore.CombineObjectPartsAsync(
                    bucket,
                    uploadId,
                    fakeRequest.Object,
                    await ParseCompleteMultipartUploadAsync(fakeRequest),
                    await CreateMetadataFromRequestAsync(fakeRequest));

                var result = XmlAdapter.CompleteMultipartResult(realObject);
                
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Headers =
                    {
                        { "Access-Control-Allow-Origin", "*" },
                        { "Access-Control-Allow-Headers", "Accept, Content-Type, Authorization, Content-Length, ETag, X-CSRF-Token, Content-Disposition" },
                        { "Access-Control-Expose-Headers", "ETag" },
                    },
                    Content = new StringContent(result)
                    {
                        Headers = { { "Content-Type", "text/xml" } }
                    },
                };
            }

            if (fakeRequest.HttpRequest.Content.Headers.TryGetValues("Content-Type", out var values) &&
                Regex.IsMatch(values.First(), @"^multipart\/form-data; boundary=(.+)"))
            {
                Debug.Assert(fakeRequest.HttpRequest.Content != null);
                Debug.Assert(fakeRequest.Bucket != null);
                
                var successActionRedirect = fakeRequest.Query["success_action_redirect"];
                var successActionStatus = fakeRequest.Query["success_action_status"];

                var body = await fakeRequest.HttpRequest.Content.ReadAsStringAsync();
                
                var filename = "default";
                var filenameMatch = Regex.Match(body, "filename=\"(.*)\"");
                if (filenameMatch.Groups.Count > 1)
                    filename = filenameMatch.Groups[1].Value;

                key = key.Replace("${filename}", filename);

                var bucket = await _bucketStore.GetBucketAsync(fakeRequest.Bucket) ??
                             await _bucketStore.CreateBucketAsync(fakeRequest.Bucket);
                
                var objectData = await fakeRequest.HttpRequest.Content.ReadAsByteArrayAsync();
                var realObject = await _bucketStore.StoreObjectAsync(bucket, key, objectData,
                    await CreateMetadataFromRequestAsync(fakeRequest));

                var response = new HttpResponseMessage()
                {
                    Headers =
                    {
                        { "Access-Control-Allow-Origin", "*" },
                        { "Access-Control-Allow-Headers", "Accept, Content-Type, Authorization, Content-Length, ETag, X-CSRF-Token, Content-Disposition" },
                        { "Access-Control-Expose-Headers", "ETag" },
                        { "ETag", $"\"{realObject.Metadata.Md5}\"" }
                    }
                };

                if (successActionRedirect == null)
                {
                    response.StatusCode =
                        successActionStatus != null && int.TryParse(successActionStatus, out var successStatus)
                            ? (HttpStatusCode) successStatus
                            : HttpStatusCode.NoContent;

                    response.Content = new StringContent(
                        new XDocument(
                            new XDeclaration("1.0", "UTF-8", null),
                            new XElement("PostResponse",
                                new XElement("Location", $"http://{fakeRequest.Bucket}.localhost:{1234}/{key}"),
                                new XElement("Bucket", fakeRequest.Bucket),
                                new XElement("Key", key),
                                new XElement("ETag", $"\"{realObject.Metadata.Md5}\"")
                                )
                            ).ToString())
                    {
                        Headers = { { "Content-Type", "text/xml" } }
                    };
                    
                    return response;
                }

                var objectParams = $"bucket={fakeRequest.Bucket}&key={key}";
                
                var locationUri = new UriBuilder(new Uri(successActionRedirect));
                var originalLocationParams = HttpUtility.UrlDecode(locationUri.Query);
                locationUri.Query = HttpUtility.UrlEncode($"{originalLocationParams}&{objectParams}");
                
                response.StatusCode = HttpStatusCode.RedirectMethod;
                response.Content = new StringContent("");
                response.Headers.Add("Location", locationUri.Uri.AbsoluteUri);
                return response;
            }

            return new HttpResponseMessage(HttpStatusCode.BadRequest);
        }

        private async Task<IEnumerable<PartInfo>> ParseCompleteMultipartUploadAsync(FakeS3Request fakeRequest)
        {
            Debug.Assert(fakeRequest.HttpRequest.Content != null);

            var xml = await fakeRequest.HttpRequest.Content.ReadAsStringAsync();
            var doc = XDocument.Parse(xml);
            return doc
                .Elements("CompleteMultipartUpload/Part")
                .Select(p => 
                    new PartInfo(
                        int.Parse(p.Element("PartNumber")!.Value),
                        p.Element("ETag")!.Value
                        )
                );
        }

        private async Task<HttpResponseMessage> DoCreateBucketAsync(FakeS3Request fakeRequest)
        {
            Debug.Assert(fakeRequest.Bucket != null);

            await _bucketStore.CreateBucketAsync(fakeRequest.Bucket);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Headers =
                {
                    { "Access-Control-Allow-Origin", "*" }
                },
                Content = new StringContent("", Encoding.UTF8, "text/xml")
            };
        }

        private Task<HttpResponseMessage> DoListBucketsAsync(FakeS3Request fakeRequest)
        {
            var buckets = _bucketStore.Buckets;
            var result = XmlAdapter.Buckets(buckets.ToArray());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(result, Encoding.UTF8, "application/xml")
            });
        }

        private async Task<HttpResponseMessage> DoLsBucketAsync(FakeS3Request fakeRequest)
        {
            Debug.Assert(fakeRequest.Bucket != null);

            var bucket = await _bucketStore.GetBucketAsync(fakeRequest.Bucket);
            if (bucket == null)
                return new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Content = new StringContent(XmlAdapter.ErrorNoSuchBucket(fakeRequest.Bucket), Encoding.UTF8, "application/xml")
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
                Content = new StringContent(result, Encoding.UTF8, "application/xml")
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
            var storedObject = await _bucketStore.StoreObjectAsync(bucket, fakeRequest.Object, objectData,
                await CreateMetadataFromRequestAsync(fakeRequest));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Headers =
                {
                    { "Access-Control-Allow-Origin", "*" },
                    { "ETag", $"\"{storedObject.Metadata.Md5}\"" }
                },
                Content = new StringContent("", null, "text/xml")
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
                    { "Access-Control-Allow-Origin", "*" }
                },
                Content = new StringContent(result)
                {
                    Headers = { { "Content-Type", "text/xml" } }
                },
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
                        { "Access-Control-Allow-Origin", "*" }
                    },
                    Content = new StringContent(XmlAdapter.ErrorNoSuchBucket(fakeRequest.Bucket))
                    {
                        Headers = { { "Content-Type", "application/xml" } }
                    },
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
                        { "Access-Control-Allow-Origin", "*" }
                    },
                    Content = new StringContent(XmlAdapter.ErrorNoSuchKey(fakeRequest.Object))
                    {
                        Headers = { { "Content-Type", "application/xml" } }
                    },
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

            var response = new HttpResponseMessage(HttpStatusCode.OK);
            
            response.Headers.ETag = new EntityTagHeaderValue($"\"{@object.Metadata.Md5}\"");
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

            if (@object.Metadata.CacheControl != null)
                response.Headers.Add("Cache-Control", @object.Metadata.CacheControl);
            
            if (fakeRequest.HttpMethod == HttpMethod.Head)
            {
                response.Content = new StringContent("");
                @object.Io.Dispose();
                return response;
            }

            response.Content = new StreamContent(@object.Io.Stream)
            {
                Headers =
                {
                    {"Content-Type", @object.Metadata.ContentType},
                    {"Content-Length", @object.Io.ContentSize.ToString()},
                    {"Content-Disposition", @object.Metadata.ContentDisposition ?? "attachment"},
                    {"Last-Modified", @object.Metadata.Modified.ToString("r")}
                }
            };

            if (@object.Metadata.ContentEncoding != null)
            {
                response.Headers.Add("X-Content-Encoding", @object.Metadata.ContentEncoding);
                response.Content.Headers.Add("Content-Encoding", @object.Metadata.ContentEncoding);
            }
            
            return response;
        }

        private Task<HttpResponseMessage> DoGetAclAsync(FakeS3Request fakeRequest)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(XmlAdapter.Acl())
                {
                    Headers = { { "Content-Type", "application/xml" } }
                },
            });
        }

        private Task<HttpResponseMessage> DoSetAclAsync(FakeS3Request fakeRequest)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Headers =
                {
                    { "Access-Control-Allow-Origin", "*" }
                },
                Content = new StringContent("")
                {
                    Headers = { { "Content-Type", "text/xml" } }
                },
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

            var contentType = request.HttpRequest.Content.Headers.GetValues("Content-Type").First();
            var contentDisposition = request.HttpRequest.Content.Headers.TryGetValues("Content-Disposition", out var values)
                ? values.First()
                : null;
            var cacheControl = request.HttpRequest.Headers.TryGetValues("Cache-Control", out values)
                ? values.First()
                : null;
            var contentEncoding = request.HttpRequest.Content.Headers.TryGetValues("Content-Encoding", out values)
                ? values.First()
                : null;

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