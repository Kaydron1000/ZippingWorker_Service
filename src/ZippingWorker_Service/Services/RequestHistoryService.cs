using System.Text.Json;
using ZippingWorker_Service.Configuration;
using ZippingWorker_Service.Model;

namespace ZippingWorker_Service.Services
{
    /// <summary>
    /// Interface for managing persistent request history stored in JSON file
    /// </summary>
    public interface IRequestHistoryService
    {
        /// <summary>
        /// Add a new request to the history file
        /// </summary>
        Task AddRequestAsync(RequestHistoryItem item);

        /// <summary>
        /// Update an existing request in the history file
        /// </summary>
        Task UpdateRequestAsync(RequestHistoryItem item);

        /// <summary>
        /// Get a specific request by ID from the history file
        /// </summary>
        Task<RequestHistoryItem?> GetRequestAsync(string id);

        /// <summary>
        /// Get all requests from the history file
        /// </summary>
        Task<List<RequestHistoryItem>> GetAllRequestsAsync();

        /// <summary>
        /// Get requests filtered by status
        /// </summary>
        Task<List<RequestHistoryItem>> GetRequestsByStatusAsync(RequestStatus status);

        /// <summary>
        /// Get count of requests by status
        /// </summary>
        Task<int> GetCountByStatusAsync(RequestStatus status);

        /// <summary>
        /// Remove old completed/failed requests older than the specified time
        /// </summary>
        Task<int> CleanupOldRequestsAsync(TimeSpan olderThan);
    }

    /// <summary>
    /// Service for managing persistent request history in JSON file with thread-safe operations
    /// </summary>
    public class RequestHistoryService : IRequestHistoryService
    {
        private readonly string _historyFilePath;
        private readonly ILogger<RequestHistoryService> _logger;
        private readonly SemaphoreSlim _fileLock = new SemaphoreSlim(1, 1);
        private readonly JsonSerializerOptions _jsonOptions;

        public RequestHistoryService(
            ZippingWorker_ServiceConfigurationType config,
            ILogger<RequestHistoryService> logger)
        {
            _logger = logger;

            // Resolve history file path
            var historyPath = config.requesthistory;
            if (string.IsNullOrWhiteSpace(historyPath))
            {
                historyPath = "requesthistoryqueue.json";
            }

            // Replace %APPPATH% placeholder
            historyPath = historyPath.Replace("%APPPATH%", AppContext.BaseDirectory, StringComparison.OrdinalIgnoreCase);

            // If relative path, make it relative to application directory
            if (!Path.IsPathRooted(historyPath))
            {
                historyPath = Path.Combine(AppContext.BaseDirectory, historyPath);
            }

            _historyFilePath = Path.GetFullPath(historyPath);

            // Ensure directory exists
            var directory = Path.GetDirectoryName(_historyFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            _logger.LogInformation("Request history file path: {FilePath}", _historyFilePath);
        }

        public async Task AddRequestAsync(RequestHistoryItem item)
        {
            await _fileLock.WaitAsync();
            try
            {
                var schema = await LoadHistoryAsync();

                // Check if already exists
                if (schema.Requests.Any(r => r.Id == item.Id))
                {
                    _logger.LogWarning("Request {RequestId} already exists in history, skipping add", item.Id);
                    return;
                }

                schema.Requests.Add(item);
                await SaveHistoryAsync(schema);

                _logger.LogDebug("Added request {RequestId} to history file", item.Id);
            }
            finally
            {
                _fileLock.Release();
            }
        }

        public async Task UpdateRequestAsync(RequestHistoryItem item)
        {
            await _fileLock.WaitAsync();
            try
            {
                var schema = await LoadHistoryAsync();

                var existingIndex = schema.Requests.FindIndex(r => r.Id == item.Id);
                if (existingIndex >= 0)
                {
                    schema.Requests[existingIndex] = item;
                    await SaveHistoryAsync(schema);
                    _logger.LogDebug("Updated request {RequestId} in history file", item.Id);
                }
                else
                {
                    _logger.LogWarning("Request {RequestId} not found for update, adding as new", item.Id);
                    schema.Requests.Add(item);
                    await SaveHistoryAsync(schema);
                }
            }
            finally
            {
                _fileLock.Release();
            }
        }

        public async Task<RequestHistoryItem?> GetRequestAsync(string id)
        {
            await _fileLock.WaitAsync();
            try
            {
                var schema = await LoadHistoryAsync();
                return schema.Requests.FirstOrDefault(r => r.Id == id);
            }
            finally
            {
                _fileLock.Release();
            }
        }

        public async Task<List<RequestHistoryItem>> GetAllRequestsAsync()
        {
            await _fileLock.WaitAsync();
            try
            {
                var schema = await LoadHistoryAsync();
                return schema.Requests.ToList();
            }
            finally
            {
                _fileLock.Release();
            }
        }

        public async Task<List<RequestHistoryItem>> GetRequestsByStatusAsync(RequestStatus status)
        {
            await _fileLock.WaitAsync();
            try
            {
                var schema = await LoadHistoryAsync();
                return schema.Requests.Where(r => r.Status == status).ToList();
            }
            finally
            {
                _fileLock.Release();
            }
        }

        public async Task<int> GetCountByStatusAsync(RequestStatus status)
        {
            await _fileLock.WaitAsync();
            try
            {
                var schema = await LoadHistoryAsync();
                return schema.Requests.Count(r => r.Status == status);
            }
            finally
            {
                _fileLock.Release();
            }
        }

        public async Task<int> CleanupOldRequestsAsync(TimeSpan olderThan)
        {
            await _fileLock.WaitAsync();
            try
            {
                var schema = await LoadHistoryAsync();
                var cutoffTime = DateTime.UtcNow - olderThan;

                var itemsToRemove = schema.Requests
                    .Where(r => (r.Status == RequestStatus.Completed || r.Status == RequestStatus.Failed)
                                && r.Finish.HasValue
                                && r.Finish.Value < cutoffTime)
                    .ToList();

                if (itemsToRemove.Count > 0)
                {
                    foreach (var item in itemsToRemove)
                    {
                        schema.Requests.Remove(item);
                    }

                    await SaveHistoryAsync(schema);
                    _logger.LogInformation("Cleaned up {Count} old requests from history file", itemsToRemove.Count);
                }

                return itemsToRemove.Count;
            }
            finally
            {
                _fileLock.Release();
            }
        }

        /// <summary>
        /// Load history from file (or create new if doesn't exist)
        /// </summary>
        private async Task<RequestHistorySchema> LoadHistoryAsync()
        {
            if (!File.Exists(_historyFilePath))
            {
                _logger.LogInformation("History file does not exist, creating new schema");
                return new RequestHistorySchema
                {
                    SchemaVersion = "1.0",
                    Requests = new List<RequestHistoryItem>()
                };
            }

            try
            {
                var json = await File.ReadAllTextAsync(_historyFilePath);
                var schema = JsonSerializer.Deserialize<RequestHistorySchema>(json, _jsonOptions);

                if (schema == null)
                {
                    _logger.LogWarning("Failed to deserialize history file, creating new schema");
                    return new RequestHistorySchema
                    {
                        SchemaVersion = "1.0",
                        Requests = new List<RequestHistoryItem>()
                    };
                }

                return schema;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading history file, creating new schema");
                return new RequestHistorySchema
                {
                    SchemaVersion = "1.0",
                    Requests = new List<RequestHistoryItem>()
                };
            }
        }

        /// <summary>
        /// Save history to file using atomic write pattern (write to temp, then rename)
        /// </summary>
        private async Task SaveHistoryAsync(RequestHistorySchema schema)
        {
            var tempPath = _historyFilePath + ".tmp";

            try
            {
                // Write to temp file
                var json = JsonSerializer.Serialize(schema, _jsonOptions);
                await File.WriteAllTextAsync(tempPath, json);

                // Atomic rename (replaces existing file)
                File.Move(tempPath, _historyFilePath, overwrite: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving history file");

                // Clean up temp file if it exists
                if (File.Exists(tempPath))
                {
                    try { File.Delete(tempPath); } catch { }
                }

                throw;
            }
        }
    }
}
