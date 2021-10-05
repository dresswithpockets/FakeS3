using System.Collections.Generic;

namespace FakeS3
{
    public record MatchSet(IEnumerable<Object> Matches, bool IsTruncated, IEnumerable<string> CommonPrefixes);
}