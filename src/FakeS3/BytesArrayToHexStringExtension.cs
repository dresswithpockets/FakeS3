using System;
using System.Collections.Generic;
using System.Linq;

namespace FakeS3
{
    public static class BytesArrayToHexStringExtension
    {
        public static string ToHexString(this IEnumerable<byte> data)
            => BitConverter.ToString(data.ToArray()).ToLower().Replace("-", "");
    }
}