using System;
using System.Collections.Generic;

namespace FakeS3.Internal
{
    /// <inheritdoc cref="IObject" />
    internal record Object(
        string Name,
        IStoreStream? Io,
        ObjectMetadata Metadata) : IObject
    {
        // ReSharper disable once IntroduceOptionalParameters.Global
        public Object(string marker)
            : this(marker, default, default!)
        {
        }

        public int CompareTo(IObject? other)
        {
            if (ReferenceEquals(this, other)) return 0;
            if (ReferenceEquals(null, other)) return 1;
            return string.Compare(Name, other.Name, StringComparison.Ordinal);
        }
    }
}