using System;
using System.Threading.Tasks;
using Amazon.S3;
using Xunit;

namespace FakeS3.Tests;

public class MemoryStoreTests : IDisposable
{
    private readonly IAmazonS3 _memoryClient;
            
    public MemoryStoreTests() => _memoryClient = AWS.FakeS3.CreateMemoryClient();

    public void Dispose()
    {
        _memoryClient.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public Task CanUploadAndDownloadData() => CommonTests.CanUploadAndDownloadData(_memoryClient);

    [Fact]
    public Task ClientCanUploadAndDownloadDataInParts() => CommonTests.CanUploadAndDownloadDataInParts(_memoryClient);

    [Fact]
    public Task CanUploadAndDeleteData() => CommonTests.CanUploadAndDeleteData(_memoryClient);
}