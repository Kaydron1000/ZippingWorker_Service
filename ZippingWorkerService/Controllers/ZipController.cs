using Microsoft.AspNetCore.Mvc;
using ZippingWorkerService.Services;

namespace ZippingWorkerService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ZipController : ControllerBase
    {
        private readonly IZipRequestQueue _zipQueue;
        private readonly ILogger<ZipController> _logger;

        public ZipController(IZipRequestQueue zipQueue, ILogger<ZipController> logger)
        {
            _zipQueue = zipQueue;
            _logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> CreateZip([FromBody] ZipRequestDto request)
        {
            if (request.Files == null || request.Files.Count == 0)
            {
                return BadRequest("No files specified");
            }

            if (string.IsNullOrWhiteSpace(request.OutputArchivePath))
            {
                return BadRequest("Output archive path is required");
            }

            var zipRequest = new ZipRequest
            {
                Files = request.Files.Select(f => new ZipFileEntry
                {
                    SourcePath = f.SourcePath,
                    ArchivePath = f.ArchivePath,
                    Hash = null
                }).ToList(),
                OutputArchivePath = request.OutputArchivePath
            };

            await _zipQueue.EnqueueAsync(zipRequest);

            _logger.LogInformation("Zip request queued for: {OutputPath}", request.OutputArchivePath);

            return Accepted(new { Message = "Zip request queued successfully", OutputPath = request.OutputArchivePath });
        }
    }

    public class ZipRequestDto
    {
        public List<FileEntry> Files { get; set; } = [];
        public string OutputArchivePath { get; set; } = string.Empty;
    }

    public class FileEntry
    {
        public string SourcePath { get; set; } = string.Empty;
        public string ArchivePath { get; set; } = string.Empty;
    }
}
