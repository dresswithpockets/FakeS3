using System;
using System.Collections.Generic;

namespace FakeS3
{
    public interface IObject : IComparable<IObject>
    {
        string Name { get; }
        IStoreStream? Io { get; }
        ObjectMetadata Metadata { get; }
    }
}