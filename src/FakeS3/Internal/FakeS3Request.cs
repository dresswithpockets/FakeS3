using System;
using System.Collections.Specialized;
using System.Linq;
using System.Net.Http;
using System.Web;

namespace FakeS3.Internal
{
    internal class FakeS3Request
    {
        public FakeS3Request(HttpRequestMessage request)
        {
            HttpRequest = request;
            Path = request.RequestUri?.AbsolutePath ?? "";
            IsPathStyle = true;

            // TODO: if root_hostname?

            Query = HttpUtility.ParseQueryString(QueryString);

            string[] elems;
            if (HttpMethod == HttpMethod.Get || HttpMethod == HttpMethod.Head)
            {
                if (Path == "/" && IsPathStyle)
                {
                    Type = RequestType.ListBuckets;
                    goto normalizeEnd;
                }

                if (IsPathStyle)
                {
                    elems = Path[1..].Split('/');
                    Bucket = elems[0];
                }
                else
                {
                    elems = Path.Split('/');
                }

                if (elems.Length < 2)
                {
                    Type = RequestType.LsBucket;
                    goto normalizeEnd;
                }

                Type = Query["acl"] == "" ? RequestType.GetAcl : RequestType.Get;
                Object = string.Join(",", elems[1..]);
            }
            else if (HttpMethod == HttpMethod.Put)
            {
                if (Path == "/")
                {
                    if (string.IsNullOrEmpty(Bucket)) goto putEnd;
                    Type = RequestType.CreateBucket;
                    goto putEnd;
                }

                Type = QueryString.Contains("?acl") ? RequestType.SetAcl : RequestType.Store;
                
                if (IsPathStyle)
                {
                    elems = Path[1..].Split('/');
                    Bucket = elems[0];

                    if (elems.Length == 1)
                    {
                        Type = RequestType.CreateBucket;
                        goto putEnd;
                    }

                    Object = string.Join("/", elems[1..]);
                    goto putEnd;
                }

                Object = Path[1..^1];

                putEnd:
                string[] copySources;
                if (!request.Headers.TryGetValues("x-amz-copy-source", out var cs) ||
                    (copySources = cs.ToArray()).Length != 1) return;
                
                var copySource = copySources[0];
                copySource = HttpUtility.UrlDecode(copySource);
                var sourceElems = copySource.Split('/');
                var rootOffset = sourceElems[0] == "" ? 1 : 0;
                SrcBucket = sourceElems[rootOffset];
                SrcObject = string.Join("/", sourceElems[(1 + rootOffset)..]);
                Type = RequestType.Copy;
                
                // ReSharper disable once RedundantJumpStatement
                goto normalizeEnd;
            }
            else if (HttpMethod == HttpMethod.Post && QueryString != "delete")
            {
                Type = RequestType.Post;
                Path = Query["key"] ?? "";
                if (IsPathStyle)
                {
                    elems = Path[1..].Split('/');
                    Bucket = elems[0];
                    if (elems.Length >= 2)
                        Object = string.Join("/", elems[1..^1]);

                    goto normalizeEnd;
                }

                Object = Path[1..^1];
            }
            else if (HttpMethod == HttpMethod.Delete || (HttpMethod == HttpMethod.Post && QueryString == "delete"))
            {
                if (Path == "/" && IsPathStyle)
                {
                    // TODO: 404?
                    throw new NotImplementedException();
                }

                if (IsPathStyle)
                {
                    elems = Path[1..].Split('/');
                    Bucket = elems[0];
                }
                else
                {
                    elems = Path.Split('/');
                }

                switch (elems.Length)
                {
                    case 0:
                        Type = IsPathStyle ? RequestType.DeleteObjects : RequestType.DeleteBucket;
                        break;
                    case 1:
                        Type = QueryString == "delete" ? RequestType.DeleteObjects : RequestType.DeleteBucket;
                        break;
                    default:
                        Type = RequestType.DeleteObject;
                        Object = string.Join("/", elems[1..]);
                        break;
                }

                // ReSharper disable once RedundantJumpStatement
                goto normalizeEnd;
            }

            normalizeEnd:
            // TODO: validate request
            ;
        }

        public RequestType Type { get; }

        public string? Bucket { get; }

        public string? Object { get; }

        public string? SrcBucket { get; }

        public string? SrcObject { get; }

        public HttpRequestMessage HttpRequest { get; }

        public string Path { get; }

        public bool IsPathStyle { get; }

        public string QueryString => HttpRequest.RequestUri?.Query ?? "";

        public NameValueCollection Query { get; }

        public HttpMethod HttpMethod => HttpRequest.Method;
    }

    internal enum RequestType
    {
        CreateBucket,
        ListBuckets,
        LsBucket,
        Head,
        Store,
        Copy,
        Get,
        GetAcl,
        SetAcl,
        DeleteObject,
        DeleteBucket,
        DeleteObjects,
        Post
    }
}