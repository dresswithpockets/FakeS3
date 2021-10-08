## FakeS3

FakeS3 is an implementation of an object store that mimics AWS S3. It provides a [FileStore](../api/FakeS3.FileStore.yml) and a [MemoryStore](../api/FakeS3.MemoryStore.yml) for storing objects on-disk or in-memory.

### Usage



## FakeS3.AWS

FakeS3.AWS provides a few utilities for creating an instance of `IAmazonS3` using an [IBucketStore](../api/FakeS3.IBucketStore.yml) implementation, such as [FileStore](../api/FakeS3.FileStore.yml) or [MemoryStore](../api/FakeS3.MemoryStore.yml).

### Usage

On-Disk storage:
```cs
using System;
using FakeS3.AWS;

// create a temporary folder to use as the on-disk s3 root
var tempRoot = Path.Join(Path.GetTempPath(), Path.GetRandomFileName());
Directory.CreateDirectory(tempRoot); 

// create IAmazonS3 instance that saves buckets to local folder
var client = FakeS3.CreateFileClient(tempRoot, false);

// use `client` directly or pass it to something like
// `Amazon.S3.Transfer.TransferUtility`
```

In-Memory storage (Note: in-memory provider is still WIP):
```cs
// create IAmazonS3 instance that stores buckets in memory
var client = FakeS3.CreateMemoryClient();

// use `client` directly or pass it to something like
// `Amazon.S3.Transfer.TransferUtility`
```
