using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using FakeS3.Internal;
using Object = FakeS3.Internal.Object;

namespace FakeS3
{
    /// <summary>
    /// An <see cref="IBucketStore"/> implementation that stores buckets on-disk as files
    /// </summary>
    public class FileStore : IBucketStore
    {
        private readonly string _root;
        private readonly bool _quietMode;
        private readonly Dictionary<string, Bucket> _bucketsMap = new();
        private readonly List<Bucket> _buckets = new();

        /// <inheritdoc />
        public IEnumerable<IBucket> Buckets => _buckets;

        /// <inheritdoc />
        public Task<IBucket?> GetBucketAsync(string name)
            => Task.FromResult<IBucket?>(_bucketsMap.TryGetValue(name, out var bucket) ? bucket : null);

        /// <inheritdoc />
        public Task<IBucket> CreateBucketAsync(string name)
        {
            if (_bucketsMap.TryGetValue(name, out var bucket))
                throw new ArgumentException("A bucket with the same name already exists", nameof(name));

            Directory.CreateDirectory(Path.Join(_root, name));
            bucket = new Bucket(name, DateTime.UtcNow, Enumerable.Empty<Object>());
            _buckets.Add(bucket);
            _bucketsMap.Add(name, bucket);
            return Task.FromResult<IBucket>(bucket);
        }

        /// <inheritdoc />
        public Task DeleteBucketAsync(string name)
        {
            if (!_bucketsMap.TryGetValue(name, out var bucket))
                throw new ArgumentException("A bucket with that name does not exist", nameof(name));

            if (bucket.Objects.Count > 0)
                throw new BucketNotEmptyException(name);

            Directory.Delete(Path.Join(_root, name), true);
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public async Task<IObject?> GetObjectAsync(IBucket bucket, string objectName)
        {
            var objectDir = Path.Join(_root, bucket.Name, objectName);
            if (!Directory.Exists(objectDir))
                return null;

            var contentFile = Path.Join(objectDir, "content");
            var metadataFile = Path.Join(objectDir, "metadata");

            var metadata = JsonSerializer.Deserialize<ObjectMetadata>(await File.ReadAllTextAsync(metadataFile));

            var creationTime = File.GetCreationTimeUtc(contentFile);
            var modifiedTime = File.GetLastWriteTimeUtc(contentFile);

            // TODO rate limit?
            return new Object(objectName, new RateLimitedFile(contentFile, int.MaxValue),
                metadata with { Created = creationTime, Modified = modifiedTime });
        }

        /// <inheritdoc />
        public Task<IObject?> GetObjectAsync(string bucketName, string objectName)
        {
            if (!_bucketsMap.TryGetValue(bucketName, out var bucket))
                throw new ArgumentException("A bucket with that name does not exist", nameof(bucketName));

            return GetObjectAsync(bucket, objectName);
        }

        /// <inheritdoc />
        public async Task<IObject> CopyObjectAsync(
            string sourceBucketName,
            string sourceObjectName,
            string destBucketName,
            string destObjectName)
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

            var metadata =
                JsonSerializer.Deserialize<ObjectMetadata>(await File.ReadAllTextAsync(destMetadataFile,
                    Encoding.UTF8));

            return new Object(destObjectName, null, metadata);
        }

        /// <inheritdoc />
        public async Task<IObject> StoreObjectAsync(
            IBucket bucket,
            string objectName,
            ReadOnlyMemory<byte> data,
            ObjectMetadata metadata)
        {
            var dirname = Path.Join(_root, bucket.Name, objectName);

            Directory.CreateDirectory(dirname);

            var contentPath = Path.Join(dirname, "content");
            var metadataPath = Path.Join(dirname, "metadata");

            await using var contentFile = File.OpenWrite(contentPath);
            await using var metadataFile = File.OpenWrite(metadataPath);

            await contentFile.WriteAsync(data);
            await metadataFile.WriteAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(metadata)));

            var storedObject = new Object(objectName, null, metadata);
            bucket.Add(storedObject);
            return storedObject;
        }

        /// <inheritdoc />
        public Task<IObject> StoreObjectAsync(
            string bucketName,
            string objectName,
            ReadOnlyMemory<byte> data,
            ObjectMetadata metadata)
        {
            if (!_bucketsMap.TryGetValue(bucketName, out var bucket))
                throw new ArgumentException("A bucket with that name does not exist", nameof(bucketName));

            return StoreObjectAsync(bucket, objectName, data, metadata);
        }

        /// <inheritdoc />
        public Task<IEnumerable<string>> DeleteObjectsAsync(IBucket bucket, params string[] objectNames)
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
                    yield return dirName; // todo(ashley) this is supposed to be object name?
                }
            }

            return Task.FromResult(DoDelete());
        }

        /// <inheritdoc />
        public Task<IEnumerable<string>> DeleteObjectsAsync(string bucketName, params string[] objectNames)
        {
            if (!_bucketsMap.TryGetValue(bucketName, out var bucket))
                throw new ArgumentException("A bucket with that name does not exist", nameof(bucketName));

            return DeleteObjectsAsync(bucket, objectNames);
        }

        /// <inheritdoc />
        public async Task<IObject> CombineObjectPartsAsync(
            IBucket bucket,
            string uploadId,
            string objectName,
            IEnumerable<PartInfo> parts,
            ObjectMetadata metadata)
        {
            var uploadPath = Path.Join(_root, bucket.Name);
            var basePath = Path.Join(uploadPath, $"{uploadId}_{objectName}");

            // todo(ashley): do this on a temporary file stream instead of a memory stream
            var completeFile = new MemoryStream();
            var partPaths = new List<string>();

            foreach (var (number, etag) in parts)
            {
                var partPath = $"{basePath}_part{number}";
                var contentPath = Path.Join(partPath, "content");

                await using var contentStream = File.OpenRead(contentPath);
                var chunk = new byte[contentStream.Length];
                await contentStream.ReadAsync(chunk);

                using var md5Hasher = MD5.Create();
                var contentHash = md5Hasher.ComputeHash(chunk).ToHexString();

                if (contentHash != etag)
                    throw new InvalidOperationException("Invalid file chunk");

                await completeFile.WriteAsync(chunk);
                partPaths.Add(partPath);
            }

            var realObject = await StoreObjectAsync(bucket, objectName, completeFile.ToArray(), metadata);

            // clean up parts
            foreach (var path in partPaths)
                Directory.Delete(path);

            return realObject;
        }

        /// <summary>
        /// Create a new FileStore
        /// </summary>
        /// <param name="root">The root directory to store buckets and objects</param>
        /// <param name="quietMode">Whether or not to perform some actions quietly/without logging</param>
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

        /// <inheritdoc />
        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }

    public record PartInfo(int Number, string ETag);
}