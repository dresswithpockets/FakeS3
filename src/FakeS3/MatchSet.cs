using System.Collections.Generic;

namespace FakeS3
{
    public record MatchSet(IEnumerable<LiteObject> Matches, bool IsTruncated, IEnumerable<string> CommonPrefixes);
}