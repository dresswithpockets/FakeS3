using System;
using System.Collections.Generic;

namespace FakeS3
{
    public interface IObject
    {
        string Name { get; }
        long Size { get; }
        DateTime Created { get; }
        DateTime Modified { get; }
        string Md5 { get; }
        IStoreStream? Io { get; }
        string ContentType { get; }
        string? ContentDisposition { get; }
        string? ContentEncoding { get; }
        IDictionary<string, string> CustomMetadata { get; }
        string? CacheControl { get; }
    }
}