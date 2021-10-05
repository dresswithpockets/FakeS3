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
        private readonly Dictionary<string, Bucket> _bucketsMap = new();
        private readonly List<Bucket> _buckets = new();

        public IEnumerable<Bucket> Buckets => _buckets;

        public Task<Bucket?> GetBucketAsync(string name)
            => Task.FromResult(_bucketsMap.TryGetValue(name, out var bucket) ? bucket : null);

        public Task<Bucket> CreateBucketAsync(string name)
        {
            if (_bucketsMap.TryGetValue(name, out var bucket))
                throw new ArgumentException("A bucket with the same name already exists", nameof(name));

            Directory.CreateDirectory(Path.Join(_root, name));
            bucket = new Bucket(name, DateTime.UtcNow, Enumerable.Empty<Object>());
            _buckets.Add(bucket);
            _bucketsMap.Add(name, bucket);
            return Task.FromResult(bucket);
        }

        public Task DeleteBucketAsync(string name)
        {
            if (!_bucketsMap.TryGetValue(name, out var bucket))
                throw new ArgumentException("A bucket with that name does not exist", nameof(name));

            if (bucket.Objects.Count > 0)
                throw new BucketNotEmptyException(name);

            Directory.Delete(Path.Join(_root, name), true);
            return Task.CompletedTask;
        }

        public async Task<Object?> GetObjectAsync(Bucket bucket, string objectName)
        {
            var objectDir = Path.Join(_root, bucket.Name, objectName);
            if (!Directory.Exists(objectDir))
                return null;
            
            var contentFile = Path.Join(objectDir, "content");
            var metadataFile = Path.Join(objectDir, "metadata");

            var metadata = JsonDocument.Parse(await File.ReadAllTextAsync(metadataFile));

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

            return new Object(objectName)
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
        }

        public Task<Object?> GetObjectAsync(string bucketName, string objectName)
        {
            if (!_bucketsMap.TryGetValue(bucketName, out var bucket))
                throw new ArgumentException("A bucket with that name does not exist", nameof(bucketName));

            return GetObjectAsync(bucket, objectName);
        }

        public async Task<Object> CopyObjectAsync(string sourceBucketName, string sourceObjectName, string destBucketName, string destObjectName)
        {
            var srcDir = Path.Join(_root, sourceBucketName, sourceObjectName);
            var destDir = Path.Join(_root, destBucketName, destObjectName);

            var srcMetadataFile = Path.Join(srcDir, "metadata");
            var srcContentFile = Path.Join(srcDir, "content");
            var destMetadataFile = Path.Join(destDir, "metadata");
            var destContentFile = Path.Join(destDir, "content");

            if (!File.Exists(srcContentFile))
                throw new InvalidOperationException(
                    $"There is no data stored for the object: {sourceBucketName}/{sourceObjectName}");

            var srcMetadata = JsonDocument.Parse(await File.ReadAllTextAsync(srcMetadataFile));

            if (sourceBucketName != destBucketName || sourceObjectName != destObjectName)
            {
                File.Copy(srcMetadataFile, destMetadataFile);
                File.Copy(srcContentFile, destContentFile);
            }
            
            // TODO: metadata directive

            try
            {
                // create the destination bucket if it isn't already there, eat the exception if it is there
                await CreateBucketAsync(destBucketName);
            }
            catch (ArgumentException)
            {
            }
            
            var md5 = srcMetadata.RootElement.GetProperty("md5").GetString();
            var contentType = srcMetadata.RootElement.GetProperty("content-type").GetString();

            // TODO: accept content-disposition from parameters
            var contentDisposition = srcMetadata.RootElement.GetProperty("content-disposition").GetString();
            var contentEncoding = srcMetadata.RootElement.GetProperty("content-encoding").GetString();
            var size = srcMetadata.RootElement.GetProperty("size").GetInt32();
            var cacheControl = srcMetadata.RootElement.GetProperty("cache-control").GetString();

            var creationTime = srcMetadata.RootElement.GetProperty("created").GetDateTime();
            var modifiedTime = srcMetadata.RootElement.GetProperty("modified").GetDateTime();

            return new Object(destObjectName)
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
        }

        public async Task<Object> StoreObjectAsync(Bucket bucket, string objectName, ReadOnlyMemory<byte> data)
        {
            var dirname = Path.Join(_root, bucket.Name, objectName);

            var contentPath = Path.Join(dirname, "content");
            var metadataPath = Path.Join(dirname, "metadata");
            Directory.CreateDirectory(contentPath);
            Directory.CreateDirectory(metadataPath);

            await using var contentFile = File.OpenWrite(contentPath);
            await using var metadataFile = File.OpenWrite(metadataPath);

            await contentFile.WriteAsync(data);

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

            var storedObject = new Object(objectName)
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
            return storedObject;
        }

        public Task<Object> StoreObjectAsync(string bucketName, string objectName, ReadOnlyMemory<byte> data)
        {
            if (!_bucketsMap.TryGetValue(bucketName, out var bucket))
                throw new ArgumentException("A bucket with that name does not exist", nameof(bucketName));

            return StoreObjectAsync(bucket, objectName, data);
        }

        public Task<IEnumerable<string>> DeleteObjectsAsync(Bucket bucket, params string[] objectNames)
        {
            IEnumerable<string> DoDelete()
            {
                foreach (var objectName in objectNames)
                {
                    var dirName = Path.Join(_root, bucket.Name, objectName);
                    var obj = bucket.Find(objectName);
                    if (obj == null) continue;

                    bucket.Remove(obj);
                    Directory.Delete(dirName, true);
                    yield return dirName;
                }
            }

            return Task.FromResult(DoDelete());
        }

        public Task<IEnumerable<string>> DeleteObjectsAsync(string bucketName, params string[] objectNames)
        {
            if (!_bucketsMap.TryGetValue(bucketName, out var bucket))
                throw new ArgumentException("A bucket with that name does not exist", nameof(bucketName));

            return DeleteObjectsAsync(bucket, objectNames);
        }

        public FileStore(string root, bool quietMode)
        {
            _root = root;
            _quietMode = quietMode;

            Directory.CreateDirectory(root);
            foreach (var bucketName in Directory.GetDirectories(root, "*", SearchOption.TopDirectoryOnly))
            {
                var bucket = new Bucket(bucketName, DateTime.UtcNow, Array.Empty<Object>());
                _buckets.Add(bucket);
                _bucketsMap.Add(bucketName, bucket);
            }
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}