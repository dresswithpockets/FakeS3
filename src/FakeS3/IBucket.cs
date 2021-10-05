using System;
using System.Collections.Generic;

namespace FakeS3
{
    /// <summary>
    /// 
    /// </summary>
    public interface IBucket
    {
        string Name { get; }

        DateTime Created { get; }

        Dictionary<string, IObject> Objects { get; }
        
        IObject? Find(string objectName);

        bool Add(IObject obj);
        
        bool Remove(IObject obj);
        
        BucketQuery QueryForRange(QueryOptions options);
    }
}