# FakeS3.Net

.NET Port of [fake-s3](https://github.com/jubos/fake-s3).

## Example Usage

On-Disk storage:
```c#
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
```c#
// create IAmazonS3 instance that stores buckets in memory
var client = FakeS3.CreateMemoryClient();

// use `client` directly or pass it to something like
// `Amazon.S3.Transfer.TransferUtility`
```