namespace FakeS3
{
    public record BucketQuery(Bucket Bucket, QueryOptions Options, MatchSet MatchSet);
}