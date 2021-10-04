namespace FakeS3
{
    public record BucketQuery(LiteBucket Bucket, QueryOptions Options, MatchSet MatchSet);
}