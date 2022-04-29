using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Transfer;
using Xunit;

namespace FakeS3.Tests;

public static class CommonTests
{
    public static async Task CanUploadAndDownloadData(IAmazonS3 amazonS3)
    {
        const string input = "Test Input";

        var transferUtility = new TransferUtility(amazonS3);
        await using var inputStream = new MemoryStream(Encoding.UTF8.GetBytes(input));

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

        Assert.Equal(input, output);
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
}