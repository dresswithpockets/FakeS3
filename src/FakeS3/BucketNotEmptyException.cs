using System;

namespace FakeS3
{
    /// <summary>
    /// An operation that depends on a bucket being empty was attempted on a bucket that is not empty
    /// </summary>
    public class BucketNotEmptyException : Exception
    {
        /// <summary>
        /// Name of the bucket that is not empty
        /// </summary>
        public string Bucket { get; }

        /// <summary>
        /// Create new BucketNotEmptyException
        /// </summary>
        /// <param name="bucket">Name of the bucket that is not empty</param>
        public BucketNotEmptyException(string bucket) : base($"Bucket '{bucket}' is not empty and cannot be deleted.")
        {
            Bucket = bucket;
        }
    }
}