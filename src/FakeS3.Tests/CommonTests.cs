using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using Xunit;

namespace FakeS3.Tests;

public static class CommonTests
{
    private const string SimpleTestInput = "Test Input";
    
    public static async Task CanUploadAndDownloadData(IAmazonS3 amazonS3)
    {
        var transferUtility = new TransferUtility(amazonS3);
        await using var inputStream = new MemoryStream(Encoding.UTF8.GetBytes(SimpleTestInput));

        var uploadRequest = new TransferUtilityUploadRequest
        {
            BucketName = "testBucket",
            Key = "testObject",
            InputStream = inputStream,
            ContentType = "text/plain",
        };
        await transferUtility.UploadAsync(uploadRequest);

        var downloadRequest = new TransferUtilityOpenStreamRequest
        {
            BucketName = "testBucket",
            Key = "testObject"
        };
        var outputStream = await transferUtility.OpenStreamAsync(downloadRequest);

        using var outputReader = new StreamReader(outputStream);
        var output = await outputReader.ReadToEndAsync();

        Assert.Equal(SimpleTestInput, output);
    }

    public static async Task CanUploadAndDownloadDataInParts(IAmazonS3 amazonS3)
    {
        var input = new byte[64];
        RandomNumberGenerator.Fill(input);

        var transferUtility = new TransferUtility(amazonS3);
        await using var inputStream = new MemoryStream(input);

        var uploadRequest = new TransferUtilityUploadRequest
        {
            BucketName = "testBucket",
            Key = "testObject",
            InputStream = inputStream,
            ContentType = "text/plain",
            PartSize = 32
        };
        await transferUtility.UploadAsync(uploadRequest);

        var downloadRequest = new TransferUtilityOpenStreamRequest
        {
            BucketName = "testBucket",
            Key = "testObject"
        };
        var outputStream = await transferUtility.OpenStreamAsync(downloadRequest);
        var output = new Memory<byte>(new byte[64]);
        var bytesRead = await outputStream.ReadAsync(output, CancellationToken.None);

        Assert.Equal(input.Length, bytesRead);

        for (var i = 0; i < input.Length; i++)
            Assert.Equal(input[i], output.Span[i]);
    }
    
    public static async Task CanUploadAndDeleteData(IAmazonS3 amazonS3)
    {
        // since this test doesnt delete the data afterwards, its a helpful setup case!
        await CanUploadAndDownloadData(amazonS3);

        var deleteRequest = new DeleteObjectRequest
        {
            BucketName = "testBucket",
            Key = "testObject",
        };
        await amazonS3.DeleteObjectAsync(deleteRequest);
        
        // list bucket objects to verify that it doesnt exist
        var listRequest = new ListObjectsV2Request
        {
            BucketName = "testBucket",
        };
        var list = await amazonS3.ListObjectsV2Async(listRequest);
        
        Assert.Empty(list.S3Objects);
    }
}