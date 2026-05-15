using System.Xml.Serialization;
using ZippingWorker_Service.Model;

namespace ZippingWorker_Service.Examples
{
    /// <summary>
    /// Example showing how to create and serialize ZipInfoType objects
    /// </summary>
    public class ZipInfoSerializationExample
    {
        /// <summary>
        /// Creates a sample ZipInfoType object
        /// </summary>
        public static ZipInfoType CreateSampleZipInfo()
        {
            var zipInfo = new ZipInfoType
            {
                zipfilename = "example-archive.zip",
                zipfiledirectory = @"C:\temp\output",
                zipcompressionlevel = CompressionLevelEnumType.ultra,
                validatezipping = ValidateEnumType.extract,
                driveletters = new[]
                {
                    new DriveLetterType
                    {
                        driveletter = "C:",
                        drivepath = @"E:\TestData"
                    },
                    new DriveLetterType
                    {
                        driveletter = "D:",
                        drivepath = @"E:\OtherData"
                    }
                },
                zipfiles = new[]
                {
                    new FileInfoType
                    {
                        filelocation = @"C:\source\file1.txt",
                        internalziplocation = "documents/file1.txt",
                        filehash = ""
                    },
                    new FileInfoType
                    {
                        filelocation = @"C:\source\file2.pdf",
                        internalziplocation = "documents/file2.pdf",
                        filehash = ""
                    }
                }
            };

            return zipInfo;
        }

        /// <summary>
        /// Serializes ZipInfoType to XML byte array
        /// </summary>
        public static byte[] SerializeToXmlBytes(ZipInfoType zipInfo)
        {
            var serializer = new XmlSerializer(typeof(ZipInfoType));
            using var ms = new MemoryStream();
            serializer.Serialize(ms, zipInfo);
            return ms.ToArray();
        }

        /// <summary>
        /// Deserializes ZipInfoType from XML byte array
        /// </summary>
        public static ZipInfoType DeserializeFromXmlBytes(byte[] data)
        {
            var serializer = new XmlSerializer(typeof(ZipInfoType));
            using var ms = new MemoryStream(data);
            return (ZipInfoType)serializer.Deserialize(ms)!;
        }

        /// <summary>
        /// Serializes ZipInfoType to XML string
        /// </summary>
        public static string SerializeToXmlString(ZipInfoType zipInfo)
        {
            var serializer = new XmlSerializer(typeof(ZipInfoType));
            using var sw = new StringWriter();
            serializer.Serialize(sw, zipInfo);
            return sw.ToString();
        }

        /// <summary>
        /// Sends a ZipInfoType to the API endpoint using HttpClient
        /// </summary>
        public static async Task<HttpResponseMessage> SendToApiAsync(ZipInfoType zipInfo, string apiBaseUrl = "http://localhost:5000")
        {
            using var httpClient = new HttpClient();
            var bytes = SerializeToXmlBytes(zipInfo);
            var content = new ByteArrayContent(bytes);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

            return await httpClient.PostAsync($"{apiBaseUrl}/api/zipinfo/binary", content);
        }

        /// <summary>
        /// Example usage
        /// </summary>
        public static async Task ExampleUsageAsync()
        {
            // Create zip info
            var zipInfo = CreateSampleZipInfo();

            // Serialize to bytes
            var bytes = SerializeToXmlBytes(zipInfo);
            Console.WriteLine($"Serialized size: {bytes.Length} bytes");

            // Send to API
            var response = await SendToApiAsync(zipInfo);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Success: {result}");
            }
            else
            {
                Console.WriteLine($"Error: {response.StatusCode}");
            }
        }
    }
}
