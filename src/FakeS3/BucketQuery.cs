namespace FakeS3
{
    public record BucketQuery(IBucket Bucket, QueryOptions Options, MatchSet MatchSet);
}