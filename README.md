# FakeS3.Net

.NET Port of [fake-s3](https://github.com/jubos/fake-s3).

## Example Usage

```c#
using System;
using FakeS3;

// create a temporary folder to use as the on-disk s3 root
var tempRoot = Path.Join(Path.GetTempPath(), Path.GetRandomFileName());
Directory.CreateDirectory(tempRoot); 

// create IAmazonS3 instance that saves buckets to local folder
var client = FakeS3.CreateFileClient(tempRoot, false);

// use `client` directly or pass it to something like
// `Amazon.S3.Transfer.TransferUtility`
```