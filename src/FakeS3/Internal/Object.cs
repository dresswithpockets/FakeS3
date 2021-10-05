using System;
using System.Collections.Generic;

namespace FakeS3.Internal
{
    /// <inheritdoc cref="IObject" />
    internal record Object(
        string Name,
        long Size,
        DateTime Created,
        DateTime Modified,
        string Md5,
        IStoreStream? Io,
        string ContentType,
        string? ContentDisposition,
        string? ContentEncoding,
        IDictionary<string, string> CustomMetadata,
        string? CacheControl) : IComparable<Object>, IObject
    {
        // ReSharper disable once IntroduceOptionalParameters.Global
        public Object(string marker)
            : this(marker, default, default, default, default!, default, default!, default, default, default!, default)
        {
        }

        public int CompareTo(Object? other)
        {
            if (ReferenceEquals(this, other)) return 0;
            if (ReferenceEquals(null, other)) return 1;
            return string.Compare(Name, other.Name, StringComparison.Ordinal);
        }
    }
}