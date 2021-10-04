using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Amazon.S3.Transfer;
using Microsoft.Win32.SafeHandles;

namespace FakeS3
{
    public interface IStoreStream
    {
        public Stream Stream { get; }

        public long ContentSize { get; }

        Task<int> ReadAsync(byte[] array, int offset, int count);
    }
    
    public class RateLimitedFile : IStoreStream, IDisposable
    {
        private readonly FileStream _fileStream;

        public int RateLimit { get; set; }

        public long ContentSize { get; }

        public Stream Stream => _fileStream;

        public RateLimitedFile(string file, int rateLimit)
        {
            ContentSize = new FileInfo(file).Length;
            _fileStream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
            RateLimit = rateLimit;
        }

        public Task<int> ReadAsync(byte[] array, int offset, int count)
            => _fileStream.ReadAsync(array, offset, Math.Min(count, RateLimit));

        public void Dispose()
        {
            _fileStream.Dispose();
            
            GC.SuppressFinalize(this);
        }
    }

    public class FileStore : IBucketStore
    {
        private readonly string _root;
        private readonly bool _quietMode;
        private readonly Dictionary<string, LiteBucket> _bucketsMap = new();
        private readonly List<LiteBucket> _buckets = new();

        // TODO: metadata

        public IEnumerable<LiteBucket> Buckets => _buckets;

        public FileStore(string root, bool quietMode)
        {
            _root = root;
            _quietMode = quietMode;

            Directory.CreateDirectory(root);
            foreach (var bucketName in Directory.GetDirectories(root, "*", SearchOption.TopDirectoryOnly))
            {
                var bucket = new LiteBucket(bucketName, DateTime.UtcNow, Array.Empty<LiteObject>());
                _buckets.Add(bucket);
                _bucketsMap.Add(bucketName, bucket);
            }
        }

        public void Dispose()
        {
            throw new System.NotImplementedException();
        }

        public bool TryGetBucket(string name, [MaybeNullWhen(false)] out LiteBucket bucket)
            => _bucketsMap.TryGetValue(name, out bucket);

        public bool TryCreateBucket(string name, [MaybeNullWhen(false)] out LiteBucket bucket)
        {
            if (_bucketsMap.TryGetValue(name, out bucket))
                return false;

            Directory.CreateDirectory(Path.Join(_root, name));
            bucket = new LiteBucket(name, DateTime.UtcNow, Enumerable.Empty<LiteObject>());
            _buckets.Add(bucket);
            _bucketsMap.Add(name, bucket);
            return true;
        }

        public bool TryDeleteBucket(string name, out bool bucketNotEmpty)
        {
            bucketNotEmpty = false;
            if (!TryGetBucket(name, out var bucket))
                return false;

            if (bucket.Objects.Count > 0)
            {
                bucketNotEmpty = true;
                return false;
            }

            Directory.Delete(Path.Join(_root, name), true);
            return true;
        }

        public bool TryGetObject(LiteBucket bucket, string objectName, [MaybeNullWhen(false)] out LiteObject liteObject)
        {
            var objectDir = Path.Join(_root, bucket.Name, objectName);
            if (!Directory.Exists(objectDir))
            {
                liteObject = null;
                return false;
            }
            
            var contentFile = Path.Join(objectDir, "content");
            var metadataFile = Path.Join(objectDir, "metadata");

            var metadata = JsonDocument.Parse(File.ReadAllText(metadataFile));

            var md5 = metadata.RootElement.GetProperty("md5").GetString();
            var contentType = "application/octet-stream";
            
            // TODO: accept content-type from parameters
            if (metadata.RootElement.TryGetProperty("content-type", out var property))
                contentType = property.GetString();
            
            // TODO: accept content-disposition from parameters
            var contentDisposition = metadata.RootElement.GetProperty("content-disposition").GetString();
            var contentEncoding = metadata.RootElement.GetProperty("content-encoding").GetString();
            var size = metadata.RootElement.GetProperty("size").GetInt32();
            var cacheControl = metadata.RootElement.GetProperty("cache-control").GetString();
            var metadataDict = metadata.RootElement.GetProperty("custom-metadata").EnumerateObject()
                .ToDictionary(prop => prop.Name, prop => prop.Value.GetString() ?? "");

            var creationTime = File.GetCreationTimeUtc(contentFile);
            var modifiedTime = File.GetLastWriteTimeUtc(contentFile);

            liteObject = new LiteObject(objectName)
            {
                Created = creationTime,
                Modified = modifiedTime,
                Io = new RateLimitedFile(contentFile, int.MaxValue), // TODO rate limit?
                Md5 = md5,
                Size = size,
                ContentDisposition = contentDisposition,
                ContentEncoding = contentEncoding,
                ContentType = contentType,
                CacheControl = cacheControl,
                CustomMetadata = metadataDict
            };
            return true;
        }

        public bool TryCopyObject(
            string sourceBucketName,
            string sourceObjectName,
            string destBucketName,
            string destObjectName,
            [MaybeNullWhen(false)] out LiteObject copiedObject)
        {
            var srcDir = Path.Join(_root, sourceBucketName, sourceObjectName);
            var destDir = Path.Join(_root, destBucketName, destObjectName);

            var srcMetadataFile = Path.Join(srcDir, "metadata");
            var srcContentFile = Path.Join(srcDir, "content");
            var destMetadataFile = Path.Join(destDir, "metadata");
            var destContentFile = Path.Join(destDir, "content");

            if (!File.Exists(srcContentFile))
            {
                copiedObject = null;
                return false;
            }

            var srcMetadata = JsonDocument.Parse(File.ReadAllText(srcMetadataFile));

            if (sourceBucketName != destBucketName || sourceObjectName != destObjectName)
            {
                File.Copy(srcMetadataFile, destMetadataFile);
                File.Copy(srcContentFile, destContentFile);
            }
            
            // TODO: metadata directive

            if (!TryGetBucket(sourceBucketName, out var srcBucket))
                TryCreateBucket(sourceBucketName, out srcBucket);

            if (!TryGetBucket(destBucketName, out var destBucket))
                TryCreateBucket(destBucketName, out destBucket);
            
            var md5 = srcMetadata.RootElement.GetProperty("md5").GetString();
            var contentType = srcMetadata.RootElement.GetProperty("content-type").GetString();

            // TODO: accept content-disposition from parameters
            var contentDisposition = srcMetadata.RootElement.GetProperty("content-disposition").GetString();
            var contentEncoding = srcMetadata.RootElement.GetProperty("content-encoding").GetString();
            var size = srcMetadata.RootElement.GetProperty("size").GetInt32();
            var cacheControl = srcMetadata.RootElement.GetProperty("cache-control").GetString();

            var creationTime = srcMetadata.RootElement.GetProperty("created").GetDateTime();
            var modifiedTime = srcMetadata.RootElement.GetProperty("modified").GetDateTime();

            copiedObject = new LiteObject(destObjectName)
            {
                Created = creationTime,
                Modified = modifiedTime,
                Md5 = md5,
                Size = size,
                ContentType = contentType,
                ContentDisposition = contentDisposition,
                ContentEncoding = contentEncoding,
                CacheControl = cacheControl
            };

            return true;
        }

        public bool TryStoreObject(
            LiteBucket bucket,
            string objectName,
            byte[] objectData,
            [MaybeNullWhen(false)] out LiteObject storedObject)
        {
            var dirname = Path.Join(_root, bucket.Name, objectName);

            var contentPath = Path.Join(dirname, "content");
            var metadataPath = Path.Join(dirname, "metadata");
            Directory.CreateDirectory(contentPath);
            Directory.CreateDirectory(metadataPath);

            using var contentFile = File.OpenWrite(contentPath);
            using var metadataFile = File.OpenWrite(metadataPath);

            contentFile.Write(objectData);

            // TODO: create metadata

            JsonDocument srcMetadata = default!;
            
            var md5 = srcMetadata.RootElement.GetProperty("md5").GetString();
            var contentType = srcMetadata.RootElement.GetProperty("content-type").GetString();

            // TODO: accept content-disposition from parameters
            var contentDisposition = srcMetadata.RootElement.GetProperty("content-disposition").GetString();
            var contentEncoding = srcMetadata.RootElement.GetProperty("content-encoding").GetString();
            var size = srcMetadata.RootElement.GetProperty("size").GetInt32();
            var cacheControl = srcMetadata.RootElement.GetProperty("cache-control").GetString();

            var creationTime = srcMetadata.RootElement.GetProperty("created").GetDateTime();
            var modifiedTime = srcMetadata.RootElement.GetProperty("modified").GetDateTime();

            storedObject = new LiteObject(objectName)
            {
                Created = creationTime,
                Modified = modifiedTime,
                Md5 = md5,
                Size = size,
                ContentType = contentType,
                ContentDisposition = contentDisposition,
                ContentEncoding = contentEncoding,
                CacheControl = cacheControl
            };
            bucket.Add(storedObject);
            return true;
        }

        public IEnumerable<string> TryDeleteObjects(LiteBucket bucket, params string[] objectNames)
        {
            foreach (var objectName in objectNames)
            {
                var dirName = Path.Join(_root, bucket.Name, objectName);
                var obj = bucket.Find(objectName);
                if (obj == null) continue;
                
                bucket.Remove(obj);
                yield return dirName;
            }
        }
    }
}