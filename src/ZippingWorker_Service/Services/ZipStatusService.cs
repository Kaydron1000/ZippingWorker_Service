using System.Collections.Concurrent;
using ZippingWorker_Service.Zipping;

namespace ZippingWorker_Service.Services
{
    /// <summary>
    /// Represents the status of a zip request throughout its lifecycle
    /// </summary>
    public class ZipRequestStatus
    {
        /// <summary>
        /// Unique identifier for this request
        /// </summary>
        public string RequestId { get; set; } = string.Empty;

        /// <summary>
        /// Time when the request was received
        /// </summary>
        public DateTime RequestTime { get; set; }

        /// <summary>
        /// Time when processing started (null if not started yet)
        /// </summary>
        public DateTime? StartedTime { get; set; }

        /// <summary>
        /// Time when processing finished (null if not finished yet)
        /// </summary>
        public DateTime? FinishedTime { get; set; }

        /// <summary>
        /// Compression level used for the archive
        /// </summary>
        public ArchiveCompressionLevel CompressionLevel { get; set; }

        /// <summary>
        /// Final location of the created archive file
        /// </summary>
        public string ArchiveFileLocation { get; set; } = string.Empty;

        /// <summary>
        /// Whether input files were deleted after archiving
        /// </summary>
        public bool DeletedInput { get; set; }

        /// <summary>
        /// Whether the archive was validated after creation
        /// </summary>
        public bool ZippingValidated { get; set; }

        /// <summary>
        /// Current status of the request
        /// </summary>
        public ZipRequestStatusEnum Status { get; set; } = ZipRequestStatusEnum.Queued;

        /// <summary>
        /// Error message if the request failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Number of files in the archive
        /// </summary>
        public int FileCount { get; set; }

        /// <summary>
        /// Size of the final archive in bytes (0 if not yet created)
        /// </summary>
        public long ArchiveSizeBytes { get; set; }
    }

    /// <summary>
    /// Status enum for tracking request state
    /// </summary>
    public enum ZipRequestStatusEnum
    {
        Queued,
        InProgress,
        Completed,
        Failed
    }

    public interface IZipStatusService
    {
        /// <summary>
        /// Register a new request that has been queued
        /// </summary>
        string RegisterRequest(ZipRequest request);

        /// <summary>
        /// Mark a request as started
        /// </summary>
        void MarkStarted(string requestId);

        /// <summary>
        /// Mark a request as completed successfully
        /// </summary>
        void MarkCompleted(string requestId, string archiveLocation, long archiveSizeBytes, bool deletedInput, bool validated);

        /// <summary>
        /// Mark a request as failed
        /// </summary>
        void MarkFailed(string requestId, string errorMessage);

        /// <summary>
        /// Get status of a specific request
        /// </summary>
        ZipRequestStatus? GetStatus(string requestId);

        /// <summary>
        /// Get all requests currently in queue (not started)
        /// </summary>
        IEnumerable<ZipRequestStatus> GetQueuedRequests();

        /// <summary>
        /// Get the currently in-progress request (if any)
        /// </summary>
        ZipRequestStatus? GetInProgressRequest();

        /// <summary>
        /// Get all completed requests
        /// </summary>
        IEnumerable<ZipRequestStatus> GetCompletedRequests();

        /// <summary>
        /// Get all requests (queued, in-progress, completed, failed)
        /// </summary>
        IEnumerable<ZipRequestStatus> GetAllRequests();

        /// <summary>
        /// Remove old completed/failed requests older than the specified time
        /// </summary>
        void CleanupOldRequests(TimeSpan olderThan);
    }

    public class ZipStatusService : IZipStatusService
    {
        private readonly ConcurrentDictionary<string, ZipRequestStatus> _statuses = new();
        private readonly ILogger<ZipStatusService> _logger;

        public ZipStatusService(ILogger<ZipStatusService> logger)
        {
            _logger = logger;
        }

        public string RegisterRequest(ZipRequest request)
        {
            var requestId = Extensions.GenerateShortId();
            var status = new ZipRequestStatus
            {
                RequestId = requestId,
                RequestTime = DateTime.UtcNow,
                CompressionLevel = request.CompressionLevel,
                ArchiveFileLocation = request.OutputArchivePath,
                Status = ZipRequestStatusEnum.Queued,
                FileCount = request.Files.Count
            };

            _statuses[requestId] = status;
            _logger.LogInformation("Registered request {RequestId} with {FileCount} files", requestId, request.Files.Count);
            return requestId;
        }

        public void MarkStarted(string requestId)
        {
            if (_statuses.TryGetValue(requestId, out var status))
            {
                status.StartedTime = DateTime.UtcNow;
                status.Status = ZipRequestStatusEnum.InProgress;
                _logger.LogInformation("Request {RequestId} started processing", requestId);
            }
        }

        public void MarkCompleted(string requestId, string archiveLocation, long archiveSizeBytes, bool deletedInput, bool validated)
        {
            if (_statuses.TryGetValue(requestId, out var status))
            {
                status.FinishedTime = DateTime.UtcNow;
                status.Status = ZipRequestStatusEnum.Completed;
                status.ArchiveFileLocation = archiveLocation;
                status.ArchiveSizeBytes = archiveSizeBytes;
                status.DeletedInput = deletedInput;
                status.ZippingValidated = validated;

                var duration = status.FinishedTime.Value - status.StartedTime.GetValueOrDefault(status.RequestTime);
                _logger.LogInformation("Request {RequestId} completed in {Duration:F2}s", requestId, duration.TotalSeconds);
            }
        }

        public void MarkFailed(string requestId, string errorMessage)
        {
            if (_statuses.TryGetValue(requestId, out var status))
            {
                status.FinishedTime = DateTime.UtcNow;
                status.Status = ZipRequestStatusEnum.Failed;
                status.ErrorMessage = errorMessage;
                _logger.LogWarning("Request {RequestId} failed: {Error}", requestId, errorMessage);
            }
        }

        public ZipRequestStatus? GetStatus(string requestId)
        {
            _statuses.TryGetValue(requestId, out var status);
            return status;
        }

        public IEnumerable<ZipRequestStatus> GetQueuedRequests()
        {
            return _statuses.Values
                .Where(s => s.Status == ZipRequestStatusEnum.Queued)
                .OrderBy(s => s.RequestTime);
        }

        public ZipRequestStatus? GetInProgressRequest()
        {
            return _statuses.Values
                .FirstOrDefault(s => s.Status == ZipRequestStatusEnum.InProgress);
        }

        public IEnumerable<ZipRequestStatus> GetCompletedRequests()
        {
            return _statuses.Values
                .Where(s => s.Status == ZipRequestStatusEnum.Completed || s.Status == ZipRequestStatusEnum.Failed)
                .OrderByDescending(s => s.FinishedTime);
        }

        public IEnumerable<ZipRequestStatus> GetAllRequests()
        {
            return _statuses.Values.OrderByDescending(s => s.RequestTime);
        }

        public void CleanupOldRequests(TimeSpan olderThan)
        {
            var cutoffTime = DateTime.UtcNow - olderThan;
            var oldRequests = _statuses.Values
                .Where(s => (s.Status == ZipRequestStatusEnum.Completed || s.Status == ZipRequestStatusEnum.Failed)
                            && s.FinishedTime.HasValue
                            && s.FinishedTime.Value < cutoffTime)
                .Select(s => s.RequestId)
                .ToList();

            foreach (var requestId in oldRequests)
            {
                if (_statuses.TryRemove(requestId, out _))
                {
                    _logger.LogDebug("Cleaned up old request {RequestId}", requestId);
                }
            }

            if (oldRequests.Count > 0)
            {
                _logger.LogInformation("Cleaned up {Count} old requests", oldRequests.Count);
            }
        }
    }
}
