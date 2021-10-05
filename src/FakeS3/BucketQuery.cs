namespace FakeS3
{
    /// <summary>
    /// A query ran within a bucket
    /// </summary>
    /// <param name="Bucket">Bucket the query result is from</param>
    /// <param name="Options">Options the query was ran with</param>
    /// <param name="MatchSet">Set of matched results based on the query options</param>
    public record BucketQuery(IBucket Bucket, QueryOptions Options, MatchSet MatchSet);
}