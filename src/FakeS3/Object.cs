using System;
using System.Collections.Generic;

namespace FakeS3
{
    public class Object : IComparable<Object>
    {
        public Object(string name) => Name = name;

        public string Name { get; }

        public int Size { get; init; }

        public DateTime Created { get; init; }

        public DateTime Modified { get; init; }

        public string Md5 { get; init; }

        public IStoreStream Io { get; init; }

        public string ContentType { get; init; }

        public string? ContentDisposition { get; init; }

        public string? ContentEncoding { get; init; }

        public Dictionary<string, string> CustomMetadata { get; init; }

        public string? CacheControl { get; init; }

        public int CompareTo(Object? other)
        {
            if (ReferenceEquals(this, other)) return 0;
            if (ReferenceEquals(null, other)) return 1;
            return string.Compare(Name, other.Name, StringComparison.Ordinal);
        }
    }
}