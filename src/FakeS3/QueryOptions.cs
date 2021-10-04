namespace FakeS3
{
    public record QueryOptions(string? Marker, string? Prefix, string? Delimiter, int MaxKeys = 1000);
}