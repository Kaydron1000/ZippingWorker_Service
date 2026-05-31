using Microsoft.AspNetCore.Mvc;
using ZippingWorker_Service.Services;

namespace ZippingWorker_Service.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StatusController : ControllerBase
    {
        private readonly IZipStatusService _statusService;
        private readonly ILogger<StatusController> _logger;

        public StatusController(IZipStatusService statusService, ILogger<StatusController> logger)
        {
            _statusService = statusService;
            _logger = logger;
        }

        /// <summary>
        /// Get status of a specific request by ID
        /// </summary>
        /// <param name="requestId">The request ID returned when the zip was queued</param>
        /// <returns>Status information for the request</returns>
        [HttpGet("{requestId}")]
        [ProducesResponseType(typeof(ZipRequestStatus), 200)]
        [ProducesResponseType(404)]
        public IActionResult GetStatus(string requestId)
        {
            var status = _statusService.GetStatus(requestId);
            if (status == null)
            {
                _logger.LogWarning("Status requested for unknown request ID: {RequestId}", requestId);
                return NotFound(new { message = $"Request ID '{requestId}' not found" });
            }

            return Ok(status);
        }

        /// <summary>
        /// Get all requests currently in the queue (not started yet)
        /// </summary>
        [HttpGet("queued")]
        [ProducesResponseType(typeof(IEnumerable<ZipRequestStatus>), 200)]
        public IActionResult GetQueuedRequests()
        {
            var queued = _statusService.GetQueuedRequests();
            return Ok(new
            {
                count = queued.Count(),
                requests = queued
            });
        }

        /// <summary>
        /// Get the currently in-progress request (if any)
        /// </summary>
        [HttpGet("inprogress")]
        [ProducesResponseType(typeof(ZipRequestStatus), 200)]
        [ProducesResponseType(204)]
        public IActionResult GetInProgressRequest()
        {
            var inProgress = _statusService.GetInProgressRequest();
            if (inProgress == null)
            {
                return NoContent();
            }

            return Ok(inProgress);
        }

        /// <summary>
        /// Get all completed requests (successful and failed)
        /// </summary>
        [HttpGet("completed")]
        [ProducesResponseType(typeof(IEnumerable<ZipRequestStatus>), 200)]
        public IActionResult GetCompletedRequests()
        {
            var completed = _statusService.GetCompletedRequests();
            return Ok(new
            {
                count = completed.Count(),
                requests = completed
            });
        }

        /// <summary>
        /// Get all requests (queued, in-progress, completed, failed)
        /// </summary>
        [HttpGet("allrequeststatus")]
        [ProducesResponseType(typeof(IEnumerable<ZipRequestStatus>), 200)]
        public IActionResult GetAllRequests()
        {
            var all = _statusService.GetAllRequests();
            var grouped = all.GroupBy(r => r.Request.Status);

            return Ok(new
            {
                total = all.Count(),
                queued = grouped.FirstOrDefault(g => g.Key == Model.RequestStatus.Queued)?.Count() ?? 0,
                inProgress = grouped.FirstOrDefault(g => g.Key == Model.RequestStatus.Processing)?.Count() ?? 0,
                completed = grouped.FirstOrDefault(g => g.Key == Model.RequestStatus.Completed)?.Count() ?? 0,
                failed = grouped.FirstOrDefault(g => g.Key == Model.RequestStatus.Failed)?.Count() ?? 0,
                requests = all
            });
        }

        /// <summary>
        /// Get summary statistics about current system status
        /// </summary>
        [HttpGet("summary")]
        [ProducesResponseType(200)]
        public IActionResult GetSummary()
        {
            var all = _statusService.GetAllRequests().ToList();
            var queued = all.Count(r => r.Request.Status == Model.RequestStatus.Queued);
            var inProgress = all.Count(r => r.Request.Status == Model.RequestStatus.Processing);
            var completed = all.Count(r => r.Request.Status == Model.RequestStatus.Completed);
            var failed = all.Count(r => r.Request.Status == Model.RequestStatus.Failed);

            var currentRequest = _statusService.GetInProgressRequest();

            return Ok(new
            {
                summary = new
                {
                    queued,
                    inProgress,
                    completed,
                    failed,
                    total = all.Count
                },
                currentRequest = currentRequest != null ? new
                {
                    RequestId = currentRequest.Request.Id,
                    StartedTime = currentRequest.Request.Started,
                    ArchiveFileLocation = currentRequest.Request.ArchiveLocation,
                    FileCount = currentRequest.Request.FileCount,
                    elapsedSeconds = currentRequest.Request.Started.HasValue 
                        ? (DateTime.UtcNow - currentRequest.Request.Started.Value).TotalSeconds 
                        : 0
                } : null
            });
        }

        /// <summary>
        /// Clean up old completed/failed requests
        /// </summary>
        /// <param name="olderThanHours">Remove requests completed more than this many hours ago (default: 24)</param>
        [HttpPost("cleanup")]
        [ProducesResponseType(200)]
        public IActionResult CleanupOldRequests([FromQuery] int olderThanHours = 24)
        {
            if (olderThanHours < 1)
            {
                return BadRequest(new { message = "olderThanHours must be at least 1" });
            }

            var beforeCount = _statusService.GetAllRequests().Count();
            _statusService.CleanupOldRequests(TimeSpan.FromHours(olderThanHours));
            var afterCount = _statusService.GetAllRequests().Count();
            var removed = beforeCount - afterCount;

            _logger.LogInformation("Cleanup removed {Count} old requests", removed);

            return Ok(new
            {
                message = $"Removed {removed} old requests",
                removed,
                remaining = afterCount
            });
        }
    }
}
