using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace FakeS3
{
    public interface IBucketStore : IDisposable
    {
        IEnumerable<LiteBucket> Buckets { get; }

        bool TryGetBucket(string name, [MaybeNullWhen(false)] out LiteBucket bucket);

        bool TryCreateBucket(string name, [MaybeNullWhen(false)] out LiteBucket bucket);

        bool TryDeleteBucket(string name, out bool bucketNotEmpty);

        bool TryGetObject(LiteBucket bucket, string objectName, [MaybeNullWhen(false)] out LiteObject liteObject);

        bool TryCopyObject(string sourceBucketName, string sourceObjectName, string destBucketName,
            string destObjectName, [MaybeNullWhen(false)] out LiteObject copiedObject);

        bool TryStoreObject(LiteBucket bucket, string objectName, byte[] objectData,
            [MaybeNullWhen(false)] out LiteObject storedObject);

        // TODO: combine object parts?

        IEnumerable<string> TryDeleteObjects(LiteBucket bucket, params string[] objectNames);
    }
}