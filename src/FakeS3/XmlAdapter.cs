using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Amazon.S3.Model;

namespace FakeS3
{
    internal static class XmlAdapter
    {
        public const string AmazonDateTimeFormatString = "yyyy-MM-ddTHH:mm:ss.fffZ"; 
        
        public static string Buckets(IEnumerable<IBucket> buckets)
        {
            // TODO: convert to XDocument
            var xmlDoc = new XmlDocument();
            xmlDoc.AppendChild(xmlDoc.CreateXmlDeclaration("1.0", "UTF-8", null));
            var resultNode = xmlDoc.CreateElement("ListAllMyBucketsResult", "http://s3.amazonaws.com/doc/2006-03-01/");
            
            var owner = xmlDoc.CreateElement("Owner");
            var ownerId = xmlDoc.CreateElement("ID");
            var ownerName = xmlDoc.CreateElement("DisplayName");
            ownerId.Value = "123";
            ownerName.Value = "FakeS3";
            owner.AppendChild(ownerId);
            owner.AppendChild(ownerName);

            var xmlBuckets = xmlDoc.CreateElement("Buckets");
            foreach (var b in buckets)
            {
                var bucket = xmlDoc.CreateElement("Bucket");
                var bucketName = xmlDoc.CreateElement("Name");
                var bucketCreationDate = xmlDoc.CreateElement("CreationDate");
                bucketName.Value = b.Name;
                bucketCreationDate.Value = b.Created.ToString(AmazonDateTimeFormatString);
                bucket.AppendChild(bucketName);
                bucket.AppendChild(bucketCreationDate);
                xmlBuckets.AppendChild(bucket);
            }
            
            resultNode.AppendChild(owner);
            xmlDoc.AppendChild(resultNode);
            return xmlDoc.OuterXml;
        }

        public static string Bucket(Bucket bucket)
        {
            // TODO: convert to XDocument
            var xmlDoc = new XmlDocument();
            xmlDoc.AppendChild(xmlDoc.CreateXmlDeclaration("1.0", "UTF-8", null));
            var resultNode =
                xmlDoc.AppendChild(xmlDoc.CreateElement("ListBucketResult",
                    "http://s3.amazonaws.com/doc/2006-03-01/"))!;

            resultNode.AppendChild(xmlDoc.CreateElement("Name"))!.Value = bucket.Name;
            resultNode.AppendChild(xmlDoc.CreateElement("Prefix"));
            resultNode.AppendChild(xmlDoc.CreateElement("Marker"));
            resultNode.AppendChild(xmlDoc.CreateElement("MaxKeys"))!.Value = "1000";
            resultNode.AppendChild(xmlDoc.CreateElement("IsTruncated"))!.Value = "false";
            return xmlDoc.OuterXml;
        }

        public static string CopyObjectResult(IObject copiedObject)
        {
            var xDoc = new XDocument(new XDeclaration("1.0", "UTF-8", null));
            xDoc.Add(new XElement("CopyObjectResult",
                new XElement("LastModified", copiedObject.Modified.ToString(AmazonDateTimeFormatString)),
                new XElement("ETag", $"\"{copiedObject.Md5}\"")
                )
            );
            return xDoc.ToString();
        }

        public static string Error(
            string code,
            string message,
            string? resource,
            string? key,
            int requestId,
            int? hostId)
        {
            // TODO: convert to XDocument
            var xmlDoc = new XmlDocument();
            xmlDoc.AppendChild(xmlDoc.CreateXmlDeclaration("1.0", "UTF-8", null));
            var errorNode = xmlDoc.AppendChild(xmlDoc.CreateElement("Error"))!;
            errorNode.AppendChild(xmlDoc.CreateElement("Code"))!.Value = code;
            errorNode.AppendChild(xmlDoc.CreateElement("Message"))!.Value = message;

            if (resource != null)
                errorNode.AppendChild(xmlDoc.CreateElement("Resource"))!.Value = resource;

            if (key != null)
                errorNode.AppendChild(xmlDoc.CreateElement("Key"))!.Value = key;
            
            errorNode.AppendChild(xmlDoc.CreateElement("RequestId"))!.Value = requestId.ToString();

            if (hostId != null)
                errorNode.AppendChild(xmlDoc.CreateElement("HostId"))!.Value = hostId.ToString();

            return xmlDoc.OuterXml;
        }

        public static string ErrorNoSuchBucket(string name)
            => Error("NoSuchBucket", "The resource you requested does not exist", name, null, 1, null);

        public static string ErrorBucketNotEmpty(string name)
            => Error("BucketNotEmpty", "The bucket you tried to delete is not empty", name, null, 1, null);

        public static string ErrorNoSuchKey(string name)
            => Error("NoSuchKey", "The specified key does not exist", null, name, 1, 2);

        public static IEnumerable<string> KeysFromDeleteObjects(string xml)
            => XDocument.Parse(xml)
                .Descendants()
                .Where(d => d.Name == "Object")
                .Select(obj => obj.Attributes("Key").First().Value);

        public static string Acl()
            => new XDocument(
                new XDeclaration("1.0", "UTF-8", null),
                new XElement(XName.Get("AccessControlPolicy", "http://s3.amazonaws.com/doc/2006-03-01/"),
                    new XElement("Owner",
                        new XElement("ID", "abc"),
                        new XElement("DisplayName", "You")
                    ),
                    new XElement("AccessControlList",
                        new XElement("Grant",
                            new XElement("Grantee",
                                new XAttribute("xmlns:xsi", "http://www.w3.org/2001/XMLSchema-instance"),
                                new XAttribute("xsi:type", "CanonicalUser"),
                                new XElement("ID", "abc"),
                                new XElement("ID", "You")
                            ),
                            new XElement("FULL_CONTROL")
                        )
                    )
                )
            ).ToString();

        public static string BucketQuery(BucketQuery bucketQuery)
            => new XDocument(
                new XDeclaration("1.0", "UTF-8", null),
                new XElement(XName.Get("ListBucketResult", "http://s3.amazonaws.com/doc/2006-03-01/"),
                    new XElement("Name", bucketQuery.Bucket.Name),
                    new XElement("Prefix", bucketQuery.Options.Prefix),
                    new XElement("Marker", bucketQuery.Options.Marker),
                    new XElement("MaxKeys", bucketQuery.Options.MaxKeys),
                    new XElement("IsTruncated", bucketQuery.MatchSet.IsTruncated),
                    bucketQuery.MatchSet.Matches.Select(match =>
                        new XElement("Contents",
                            new XElement("Key", match.Name),
                            new XElement("LastModified", match.Modified.ToString(AmazonDateTimeFormatString)),
                            new XElement("ETag", $"\"{match.Md5}\""),
                            new XElement("Size", match.Size),
                            new XElement("StorageClass", "STANDARD"),
                            new XElement("Owner",
                                new XElement("ID", "abc"),
                                new XElement("DisplayName", "You")
                            )
                        )
                    ),
                    bucketQuery.MatchSet.CommonPrefixes.Select(prefix =>
                        new XElement("CommonPrefixes",
                            new XElement("Prefix", prefix)
                        )
                    )
                )
            ).ToString();
    }
}