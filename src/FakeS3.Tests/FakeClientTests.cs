using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Transfer;
using Xunit;

namespace FakeS3.Tests
{
    public class FakeClientTests
    {
        public static IEnumerable<object[]> Clients()
        {
            var testDir = Path.Join(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(testDir);
            yield return new object[] {AWS.FakeS3.CreateFileClient(testDir, false)};
            yield return new object[] {AWS.FakeS3.CreateMemoryClient()};
        }

        [Theory]
        [MemberData(nameof(Clients))]
        public async Task ClientCanUploadAndDownloadData(IAmazonS3 amazonS3)
        {
            const string input = "Test Input";
            
            var transferUtility = new TransferUtility(amazonS3);
            await using var inputStream = new MemoryStream(Encoding.UTF8.GetBytes(input));
                
            var uploadRequest = new TransferUtilityUploadRequest
            {
                BucketName = "testBucket",
                Key = "testObject",
                InputStream = inputStream,
                ContentType = "text/plain"
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
    }
}