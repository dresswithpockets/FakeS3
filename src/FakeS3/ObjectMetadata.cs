using System;
using System.Collections.Generic;

namespace FakeS3
{
    public record ObjectMetadata(
        string Md5,
        string ContentType,
        string? ContentDisposition,
        string? CacheControl,
        string? ContentEncoding,
        long Size,
        DateTime Created,
        DateTime Modified,
        IDictionary<string, string> AmazonMetadata,
        IDictionary<string, string> CustomMetadata);
}