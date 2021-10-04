using System;

namespace FakeS3
{
    public class BucketNotEmptyException : Exception
    {
        public string Bucket { get; }

        public BucketNotEmptyException(string bucket) : base($"Bucket '{bucket}' is not empty and cannot be deleted.")
        {
            Bucket = bucket;
        }
    }
}