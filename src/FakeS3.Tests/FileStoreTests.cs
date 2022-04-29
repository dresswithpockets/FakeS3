using System;
using System.IO;
using System.Threading.Tasks;
using Amazon.S3;
using Xunit;

namespace FakeS3.Tests;

public class FileStoreTests : IDisposable
{
    private readonly IAmazonS3 _fileClient;

    public FileStoreTests()
    {
        var testDir = Path.Join(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(testDir);
        _fileClient = AWS.FakeS3.CreateFileClient(testDir, false);
    }

    public void Dispose()
    {
        _fileClient.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public Task CanUploadAndDownloadData() => CommonTests.CanUploadAndDownloadData(_fileClient);

    [Fact]
    public Task CanUploadAndDownloadDataInParts() => CommonTests.CanUploadAndDownloadDataInParts(_fileClient);

    [Fact]
    public Task CanUploadAndDeleteData() => CommonTests.CanUploadAndDeleteData(_fileClient);
}