using System.Collections.Concurrent;
using ZippingWorker_Service.Zipping;
using ZippingWorker_Service.Model;

namespace ZippingWorker_Service.Services
{
    /// <summary>
    /// Represents the status of a zip request throughout its lifecycle
    /// </summary>
    public class ZipRequestStatus
    {
        /// <summary>
        /// Core request history item containing id, status, timestamps, compression level, archive location, file count, and priority
        /// </summary>
        public required RequestHistoryItem Request { get; set; }

        /// <summary>
        /// Compression level enum (for internal use)
        /// </summary>
        public ArchiveCompressionLevel CompressionLevel { get; set; }

        /// <summary>
        /// Whether input files were deleted after archiving
        /// </summary>
        public bool DeletedInput { get; set; }

        /// <summary>
        /// Whether the archive was validated after creation
        /// </summary>
        public bool ZippingValidated { get; set; }

        /// <summary>
        /// Error message if the request failed
        /// </summary>
        public string? ErrorMessage { get; set; }

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
        /// <param name="request">The zip request to register</param>
        /// <param name="requestId">Optional request ID. If not provided, one will be generated.</param>
        /// <returns>The request ID (either the provided one or generated)</returns>
        string RegisterRequest(ZipRequest request, string? requestId = null);

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
        private readonly IRequestHistoryService _historyService;
        private readonly ILogger<ZipStatusService> _logger;

        public ZipStatusService(
            IRequestHistoryService historyService,
            ILogger<ZipStatusService> logger)
        {
            _historyService = historyService;
            _logger = logger;
        }

        private static RequestStatus MapToRequestStatus(ZipRequestStatusEnum status)
        {
            return status switch
            {
                ZipRequestStatusEnum.Queued => RequestStatus.Queued,
                ZipRequestStatusEnum.InProgress => RequestStatus.Processing,
                ZipRequestStatusEnum.Completed => RequestStatus.Completed,
                ZipRequestStatusEnum.Failed => RequestStatus.Failed,
                _ => RequestStatus.Pending
            };
        }

        public string RegisterRequest(ZipRequest request, string? requestId = null)
        {
            // Use provided ID or generate a new one
            if (string.IsNullOrWhiteSpace(requestId))
            {
                requestId = Extensions.GenerateShortId();
            }

            var requestHistoryItem = new RequestHistoryItem
            {
                Id = requestId,
                Status = RequestStatus.Queued,
                Requested = DateTime.UtcNow,
                CompressionLevel = request.CompressionLevel.ToString(),
                ArchiveLocation = request.OutputArchivePath,
                FileCount = request.Files.Count,
                Priority = 0 // Default priority, can be set later
            };

            var status = new ZipRequestStatus
            {
                Request = requestHistoryItem,
                CompressionLevel = request.CompressionLevel
            };

            _statuses[requestId] = status;

            // Persist to history file immediately
            Task.Run(async () =>
            {
                try
                {
                    await _historyService.AddRequestAsync(requestHistoryItem);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to add request {RequestId} to history file", requestId);
                }
            });

            _logger.LogInformation("Registered request {RequestId} with {FileCount} files", requestId, request.Files.Count);
            return requestId;
        }

        public void MarkStarted(string requestId)
        {
            if (_statuses.TryGetValue(requestId, out var status))
            {
                status.Request.Started = DateTime.UtcNow;
                status.Request.Status = RequestStatus.Processing;

                // Update in history file
                Task.Run(async () =>
                {
                    try
                    {
                        await _historyService.UpdateRequestAsync(status.Request);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to update started status for request {RequestId} in history file", requestId);
                    }
                });

                _logger.LogInformation("Request {RequestId} started processing", requestId);
            }
        }

        public void MarkCompleted(string requestId, string archiveLocation, long archiveSizeBytes, bool deletedInput, bool validated)
        {
            if (_statuses.TryGetValue(requestId, out var status))
            {
                status.Request.Finish = DateTime.UtcNow;
                status.Request.Status = RequestStatus.Completed;
                status.Request.ArchiveLocation = archiveLocation;
                status.ArchiveSizeBytes = archiveSizeBytes;
                status.DeletedInput = deletedInput;
                status.ZippingValidated = validated;

                var duration = status.Request.Finish.Value - status.Request.Started.GetValueOrDefault(status.Request.Requested);
                _logger.LogInformation("Request {RequestId} completed in {Duration:F2}s", requestId, duration.TotalSeconds);

                // Update in history file and remove from memory
                Task.Run(async () =>
                {
                    try
                    {
                        await _historyService.UpdateRequestAsync(status.Request);

                        // Remove from in-memory dictionary to free memory
                        _statuses.TryRemove(requestId, out _);
                        _logger.LogDebug("Removed completed request {RequestId} from memory", requestId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to update completed status for request {RequestId} in history file", requestId);
                    }
                });
            }
        }

        public void MarkFailed(string requestId, string errorMessage)
        {
            if (_statuses.TryGetValue(requestId, out var status))
            {
                status.Request.Finish = DateTime.UtcNow;
                status.Request.Status = RequestStatus.Failed;
                status.ErrorMessage = errorMessage;

                _logger.LogWarning("Request {RequestId} failed: {Error}", requestId, errorMessage);

                // Update in history file and remove from memory
                Task.Run(async () =>
                {
                    try
                    {
                        await _historyService.UpdateRequestAsync(status.Request);

                        // Remove from in-memory dictionary to free memory
                        _statuses.TryRemove(requestId, out _);
                        _logger.LogDebug("Removed failed request {RequestId} from memory", requestId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to update failed status for request {RequestId} in history file", requestId);
                    }
                });
            }
        }

        public ZipRequestStatus? GetStatus(string requestId)
        {
            // First check in-memory
            if (_statuses.TryGetValue(requestId, out var status))
            {
                return status;
            }

            // If not in memory, check history file (for completed/failed requests)
            try
            {
                var historyItem = _historyService.GetRequestAsync(requestId).GetAwaiter().GetResult();
                if (historyItem != null)
                {
                    // Reconstruct ZipRequestStatus from history item
                    return new ZipRequestStatus
                    {
                        Request = historyItem,
                        CompressionLevel = ArchiveCompressionLevel.ultra, // Default, actual value not stored
                        ErrorMessage = null, // Could add to RequestHistoryItem if needed
                        ArchiveSizeBytes = 0, // Could add to RequestHistoryItem if needed
                        DeletedInput = false,
                        ZippingValidated = false
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve request {RequestId} from history file", requestId);
            }

            return null;
        }

        public IEnumerable<ZipRequestStatus> GetQueuedRequests()
        {
            return _statuses.Values
                .Where(s => s.Request.Status == RequestStatus.Queued)
                .OrderBy(s => s.Request.Requested);
        }

        public ZipRequestStatus? GetInProgressRequest()
        {
            return _statuses.Values
                .FirstOrDefault(s => s.Request.Status == RequestStatus.Processing);
        }

        public IEnumerable<ZipRequestStatus> GetCompletedRequests()
        {
            // In-memory completed/failed should be minimal since they're removed on completion
            // Read from history file for completed requests
            try
            {
                var historyItems = _historyService.GetAllRequestsAsync().GetAwaiter().GetResult();
                var completed = historyItems
                    .Where(h => h.Status == RequestStatus.Completed || h.Status == RequestStatus.Failed)
                    .Select(h => new ZipRequestStatus
                    {
                        Request = h,
                        CompressionLevel = ArchiveCompressionLevel.ultra,
                        ErrorMessage = null,
                        ArchiveSizeBytes = 0,
                        DeletedInput = false,
                        ZippingValidated = false
                    })
                    .OrderByDescending(s => s.Request.Finish);

                return completed;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve completed requests from history file");
                return Enumerable.Empty<ZipRequestStatus>();
            }
        }

        public IEnumerable<ZipRequestStatus> GetAllRequests()
        {
            // Merge in-memory (active requests) with file-based history
            try
            {
                var historyItems = _historyService.GetAllRequestsAsync().GetAwaiter().GetResult();
                var inMemoryIds = _statuses.Keys.ToHashSet();

                // Get file-based items that aren't in memory
                var fileBasedStatuses = historyItems
                    .Where(h => !inMemoryIds.Contains(h.Id))
                    .Select(h => new ZipRequestStatus
                    {
                        Request = h,
                        CompressionLevel = ArchiveCompressionLevel.ultra,
                        ErrorMessage = null,
                        ArchiveSizeBytes = 0,
                        DeletedInput = false,
                        ZippingValidated = false
                    });

                // Combine with in-memory
                return _statuses.Values
                    .Concat(fileBasedStatuses)
                    .OrderByDescending(s => s.Request.Requested);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to merge in-memory and file-based requests");
                // Fallback to in-memory only
                return _statuses.Values.OrderByDescending(s => s.Request.Requested);
            }
        }

        public void CleanupOldRequests(TimeSpan olderThan)
        {
            // Clean up any old requests still in memory (shouldn't be many)
            var cutoffTime = DateTime.UtcNow - olderThan;
            var oldRequestsInMemory = _statuses.Values
                .Where(s => (s.Request.Status == RequestStatus.Completed || s.Request.Status == RequestStatus.Failed)
                            && s.Request.Finish.HasValue
                            && s.Request.Finish.Value < cutoffTime)
                .Select(s => s.Request.Id)
                .ToList();

            foreach (var requestId in oldRequestsInMemory)
            {
                if (_statuses.TryRemove(requestId, out _))
                {
                    _logger.LogDebug("Cleaned up old request {RequestId} from memory", requestId);
                }
            }

            // Clean up old requests from history file
            Task.Run(async () =>
            {
                try
                {
                    var cleanedCount = await _historyService.CleanupOldRequestsAsync(olderThan);
                    if (cleanedCount > 0)
                    {
                        _logger.LogInformation("Cleaned up {Count} old requests from history file", cleanedCount);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to cleanup old requests from history file");
                }
            });

            if (oldRequestsInMemory.Count > 0)
            {
                _logger.LogInformation("Cleaned up {Count} old requests from memory", oldRequestsInMemory.Count);
            }
        }
    }
}
