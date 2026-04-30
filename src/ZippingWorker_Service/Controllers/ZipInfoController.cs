using Microsoft.AspNetCore.Mvc;
using System.Xml.Serialization;
using ZippingWorker_Service.Configuration;
using ZippingWorker_Service.Services;
using ZippingWorker_Service.Zipping;
using ZippingWorker_Service.Model;
using System.Xml;
using System.Xml.Schema;

namespace ZippingWorker_Service.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ZipInfoController : ControllerBase
    {
        private readonly IZipRequestQueue _zipQueue;
        private readonly IDriveLetterResolver _driveResolver;
        private readonly IMetricsService _metrics;
        private readonly ILogger<ZipInfoController> _logger;

        private XmlSchemaSet _SchemaSet;
        private XmlOnDeserializedSerializer _Serializer;

        public ZipInfoController(
            IZipRequestQueue zipQueue, 
            IDriveLetterResolver driveResolver,
            IMetricsService metrics,
            ILogger<ZipInfoController> logger)
        {
            _zipQueue = zipQueue;
            _driveResolver = driveResolver;
            _metrics = metrics;
            _logger = logger;

            _SchemaSet = XmlExtensions.LoadSchemaSet("ZippingWorker_Service", "ZipInfoSchema.xsd");
            _Serializer = new XmlOnDeserializedSerializer(typeof(ZipInfoType));
        }

        /// <summary>
        /// Accepts a byte array of serialized ZipInfoType object and processes the zip request
        /// </summary>
        /// <param name="serializedData">Binary serialized ZipInfoType object</param>
        /// <returns>Accepted response with job details</returns>
        [HttpPost("binary")]
        [Consumes("application/octet-stream")]
        public async Task<IActionResult> SubmitZipRequestBinary()
        {
            try
            {
                _metrics.RecordZipRequested();

                byte[] buffer;
                using (var ms = new MemoryStream())
                {
                    await Request.Body.CopyToAsync(ms);
                    buffer = ms.ToArray();
                }

                if (buffer == null || buffer.Length == 0)
                {
                    return BadRequest("No data received");
                }

                _logger.LogInformation("Received {ByteCount} bytes for deserialization", buffer.Length);

                // Deserialize using XML serializer (safer than BinaryFormatter)
                ZipInfoType zipInfo = null;
                using (MemoryStream ms = new MemoryStream(buffer))
                {
                    using(XmlReader reader = XmlReader.Create(ms))
                    {
                        XmlReader xmlContentNormalized = _Serializer.ValidateAgainstSchemaIgnoreCaseAndRootLoc(reader, _SchemaSet, out List<ValidationEventArgs> localErrorList, out List<ValidationEventArgs> localWarningList);
                        if (localErrorList.Count == 0 && xmlContentNormalized != null)
                        {
                            zipInfo = (ZipInfoType)_Serializer.Deserialize(xmlContentNormalized);
                            if (localWarningList.Count != 0)
                            {
                                _logger.LogWarning("XML validation completed with {WarningCount} warnings and no errors", localWarningList.Count);
                                foreach (var warning in localWarningList)
                                {
                                    _logger.LogWarning("Validation Warning: {Message} at Line {LineNumber}, Position {LinePosition}", warning.Message, warning.Exception.LineNumber, warning.Exception.LinePosition);
                                }

                            }
                            else
                            {
                                _logger.LogInformation("XML validation successful with no errors or warnings");
                            }
                        }
                        else
                        {
                            _logger.LogError("XML validation failed with {ErrorCount} errors and {WarningCount} warnings", localErrorList.Count, localWarningList.Count);
                            foreach (var error in localErrorList)
                            {
                                _logger.LogError("Validation Error: {Message} at Line {LineNumber}, Position {LinePosition}", error.Message, error.Exception.LineNumber, error.Exception.LinePosition);
                            }
                            foreach (var warning in localWarningList)
                            {
                                _logger.LogWarning("Validation Warning: {Message} at Line {LineNumber}, Position {LinePosition}", warning.Message, warning.Exception.LineNumber, warning.Exception.LinePosition);
                            }
                            return StatusCode(400, new { Error = "XML validation failed", Errors = localErrorList.Select(e => e.Message), Warnings = localWarningList.Select(w => w.Message) });
                        }
                    }
                }

                if (zipInfo == null)
                {
                    return BadRequest("Failed to deserialize ZipInfoType");
                }

                return await ProcessZipInfoAsync(zipInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing binary zip request");
                return StatusCode(500, new { Error = "Failed to process zip request", Message = ex.Message });
            }
        }

        /// <summary>
        /// Accepts XML representation of ZipInfoType
        /// </summary>
        [HttpPost("xml")]
        [Consumes("application/xml", "text/xml")]
        public async Task<IActionResult> SubmitZipRequestXml()
        {
            return await SubmitZipRequestBinary();
        }

        private async Task<IActionResult> ProcessZipInfoAsync(ZipInfoType zipInfo)
        {
            // Validate input
            if (string.IsNullOrWhiteSpace(zipInfo.zipfilename))
            {
                return BadRequest("zipfilename is required");
            }

            if (zipInfo.zipfiles == null || zipInfo.zipfiles.Length == 0)
            {
                return BadRequest("No files specified in zipfiles");
            }

            // Build manual drive letter mapping dictionary (if provided)
            Dictionary<string, string>? manualMappings = null;
            if (zipInfo.driveletters != null && zipInfo.driveletters.Length > 0)
            {
                manualMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                foreach (var driveMapping in zipInfo.driveletters)
                {
                    if (!string.IsNullOrWhiteSpace(driveMapping.driveletter) && 
                        !string.IsNullOrWhiteSpace(driveMapping.drivepath))
                    {
                        string normalizedDriveLetter = driveMapping.driveletter.TrimEnd(':') + ":";
                        manualMappings[normalizedDriveLetter] = driveMapping.drivepath;

                        _logger.LogInformation("Manual drive letter mapping configured: {DriveLetter} -> {DrivePath}", 
                            normalizedDriveLetter, driveMapping.drivepath);
                    }
                }

                if (manualMappings.Count == 0)
                {
                    manualMappings = null;
                }
            }

            // Build output path with automatic drive letter resolution
            string outputPath;
            if (string.IsNullOrWhiteSpace(zipInfo.zipfiledirectory))
            {
                outputPath = Path.Combine(Directory.GetCurrentDirectory(), zipInfo.zipfilename);
            }
            else
            {
                var location = _driveResolver.ResolvePath(zipInfo.zipfiledirectory, manualMappings);
                outputPath = Path.Combine(location, zipInfo.zipfilename);
            }

            // Ensure directory exists
            var outputDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            // Convert to ZipRequest with automatic drive letter resolution
            var files = zipInfo.zipfiles
                .Select(f => new ZipFileEntry
                {
                    SourcePath = _driveResolver.ResolvePath(f.filelocation, manualMappings),
                    ArchivePath = f.internalziplocation,
                    Hash = string.IsNullOrWhiteSpace(f.filehash) ? null : f.filehash
                })
                .ToList();

            var zipRequest = new ZipRequest
            {
                Files = files,
                OutputArchivePath = outputPath,
                CompressionLevel = MapCompressionLevel(zipInfo.zipcompressionlevel),
                ValidateZipping = zipInfo.validatezipping,
                DeleteInputFiles = zipInfo.deleteinputfiles
            };

            await _zipQueue.EnqueueAsync(zipRequest);

            _metrics.RecordZipRequestQueued();

            _logger.LogInformation(
                "Zip request queued: {FileCount} files to {OutputPath} with {CompressionLevel} compression",
                files.Count,
                outputPath,
                zipInfo.zipcompressionlevel);

            return Accepted(new
            {
                Message = "Zip request queued successfully",
                OutputPath = outputPath,
                FileCount = files.Count,
                CompressionLevel = zipInfo.zipcompressionlevel.ToString(),
                ValidateZipping = zipInfo.validatezipping
            });
        }

        private static ArchiveCompressionLevel MapCompressionLevel(ZippingWorker_Service.Model.CompressionLevelEnumType level)
        {
            return level switch
            {
                ZippingWorker_Service.Model.CompressionLevelEnumType.nocompression => ArchiveCompressionLevel.nocompression,
                ZippingWorker_Service.Model.CompressionLevelEnumType.fastest => ArchiveCompressionLevel.fastest,
                ZippingWorker_Service.Model.CompressionLevelEnumType.fast => ArchiveCompressionLevel.fast,
                ZippingWorker_Service.Model.CompressionLevelEnumType.normal => ArchiveCompressionLevel.normal,
                ZippingWorker_Service.Model.CompressionLevelEnumType.maximum => ArchiveCompressionLevel.maximum,
                ZippingWorker_Service.Model.CompressionLevelEnumType.ultra => ArchiveCompressionLevel.ultra,
                _ => ArchiveCompressionLevel.ultra
            };
        }
    }
}
