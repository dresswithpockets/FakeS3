using System.Collections.Generic;

namespace FakeS3
{
    public record MatchSet(IEnumerable<IObject> Matches, bool IsTruncated, IEnumerable<string> CommonPrefixes);
}