﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace FakeS3
{
    public class LiteBucket
    {
        public string Name { get; }

        public DateTime Created { get; }

        public Dictionary<string, LiteObject> Objects { get; }
        private List<LiteObject> OrderedObjects { get; }
        
        private readonly object objectsLock = new();
        
        public LiteBucket(string name, DateTime created, IEnumerable<LiteObject> objects)
        {
            Name = name;
            Created = created;
            Objects = objects.ToDictionary(o => o.Name);
        }

        public LiteObject? Find(string objectName)
        {
            lock (objectsLock)
                return Objects.TryGetValue(objectName, out var obj) ? obj : null;
        }

        public bool Add(LiteObject obj)
        {
            lock (objectsLock)
            {
                if (!Objects.TryAdd(obj.Name, obj)) return false;
                OrderedObjects.Add(obj);
            }

            return true;
        }

        public bool Remove(LiteObject obj)
        {
            lock (objectsLock)
            {
                if (!Objects.Remove(obj.Name)) return false;
                OrderedObjects.Remove(obj);
            }

            return true;
        }

        public BucketQuery QueryForRange(QueryOptions options) => new(this, options, List(options));

        private MatchSet List(QueryOptions options)
        {
            var markerFound = false;
            LiteObject? pseudo = null;
            if (options.Marker != null)
            {
                markerFound = false;
                if (!Objects.ContainsKey(options.Marker))
                {
                    pseudo = new LiteObject(options.Marker);
                    lock (objectsLock)
                        OrderedObjects.Add(pseudo);
                }
            }

            string? basePrefix = null;
            var prefixOffset = 0;
            if (options.Delimiter != null)
            {
                basePrefix = options.Prefix ?? string.Empty;
                prefixOffset = basePrefix.Length;
            }
            
            IImmutableList<LiteObject> snapshot;
            lock (objectsLock)
                snapshot = OrderedObjects.ToImmutableList();
            
            var matches = new List<LiteObject>();
            var commonPrefixes = new List<string>();
            var isTruncated = false;
            var count = 0;
            string? lastChunk = null;
            foreach (var obj in snapshot)
            {
                if (markerFound && (options.Prefix == null || obj.Name.StartsWith(options.Prefix)))
                {
                    if (options.Delimiter != null)
                    {
                        var name = obj.Name;
                        var remainder = name[prefixOffset..];
                        var chunks = remainder.Split(options.Delimiter, 2);
                        if (chunks.Length > 1)
                        {
                            if (lastChunk != chunks[0])
                            {
                                count++;
                                if (count > options.MaxKeys)
                                {
                                    isTruncated = true;
                                    break;
                                }
                                
                                commonPrefixes.Add($"{basePrefix}{chunks[0]}{options.Delimiter}");
                                lastChunk = chunks[0];
                            }

                            continue;
                        }
                    }

                    count++;
                    if (count > options.MaxKeys)
                    {
                        isTruncated = true;
                        break;
                    }

                    matches.Add(obj);
                }

                if (options.Marker != null && options.Marker.Equals(obj.Name, StringComparison.Ordinal))
                    markerFound = true;
            }

            if (pseudo != null)
            {
                lock (objectsLock)
                    OrderedObjects.Remove(pseudo);
            }

            return new MatchSet(matches, isTruncated, commonPrefixes);
        }
    }
}