using System.Xml;
using System.Xml.Serialization;
using ZippingWorker_Service.Configuration;
using ZippingWorker_Service.Model;

namespace ZippingWorker_Service.Services
{
    /// <summary>
    /// Service for persisting zip request data to XML files
    /// </summary>
    public interface IRequestPersistenceService
    {
        /// <summary>
        /// Saves a ZipInfoType request with a generated ID to an XML file
        /// </summary>
        /// <param name="zipInfo">The zip request data</param>
        /// <param name="requestId">The unique request ID</param>
        /// <returns>The full path to the saved XML file</returns>
        Task<string> SaveRequestAsync(ZipInfoType zipInfo, string requestId);

        /// <summary>
        /// Loads a ZipInfoRequestType from an XML file by ID
        /// </summary>
        /// <param name="requestId">The unique request ID</param>
        /// <returns>The loaded request, or null if not found</returns>
        Task<ZipInfoRequestType?> LoadRequestAsync(string requestId);

        /// <summary>
        /// Deletes a request XML file by ID
        /// </summary>
        /// <param name="requestId">The unique request ID</param>
        /// <returns>True if deleted, false if not found</returns>
        Task<bool> DeleteRequestAsync(string requestId);

        /// <summary>
        /// Gets the file path for a given request ID
        /// </summary>
        /// <param name="requestId">The unique request ID</param>
        /// <returns>The full file path</returns>
        string GetRequestFilePath(string requestId);
    }

    /// <summary>
    /// Implementation of request persistence service
    /// </summary>
    public class RequestPersistenceService : IRequestPersistenceService
    {
        private readonly string _storeRequestsFolder;
        private readonly ILogger<RequestPersistenceService> _logger;
        private static readonly XmlSerializer _serializer = new XmlSerializer(typeof(ZipInfoRequestType));

        public RequestPersistenceService(
            ZippingWorker_ServiceConfigurationType config,
            ILogger<RequestPersistenceService> logger)
        {
            _logger = logger;

            // Resolve the store requests folder path
            _storeRequestsFolder = ResolvePath(config.storerequestsfolder);

            // Ensure directory exists
            if (!Directory.Exists(_storeRequestsFolder))
            {
                Directory.CreateDirectory(_storeRequestsFolder);
                _logger.LogInformation("Created requests storage directory: {Path}", _storeRequestsFolder);
            }
        }

        public async Task<string> SaveRequestAsync(ZipInfoType zipInfo, string requestId)
        {
            if (zipInfo == null)
                throw new ArgumentNullException(nameof(zipInfo));
            if (string.IsNullOrWhiteSpace(requestId))
                throw new ArgumentException("Request ID cannot be null or empty", nameof(requestId));

            // Create the request wrapper with ID
            var request = new ZipInfoRequestType
            {
                id = requestId,
                zippingfiles = zipInfo
            };

            string filePath = GetRequestFilePath(requestId);

            try
            {
                // Serialize to XML file
                using (var writer = new StreamWriter(filePath, false, System.Text.Encoding.UTF8))
                {
                    _serializer.Serialize(writer, request);
                }

                _logger.LogInformation("Saved request {RequestId} to {FilePath}", requestId, filePath);
                return filePath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save request {RequestId} to {FilePath}", requestId, filePath);
                throw;
            }
        }

        public async Task<ZipInfoRequestType?> LoadRequestAsync(string requestId)
        {
            if (string.IsNullOrWhiteSpace(requestId))
                throw new ArgumentException("Request ID cannot be null or empty", nameof(requestId));

            string filePath = GetRequestFilePath(requestId);

            if (!File.Exists(filePath))
            {
                _logger.LogWarning("Request file not found: {FilePath}", filePath);
                return null;
            }

            try
            {
                using (var reader = new StreamReader(filePath, System.Text.Encoding.UTF8))
                {
                    var request = (ZipInfoRequestType?)_serializer.Deserialize(reader);
                    _logger.LogInformation("Loaded request {RequestId} from {FilePath}", requestId, filePath);
                    return request;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load request {RequestId} from {FilePath}", requestId, filePath);
                throw;
            }
        }

        public async Task<bool> DeleteRequestAsync(string requestId)
        {
            if (string.IsNullOrWhiteSpace(requestId))
                throw new ArgumentException("Request ID cannot be null or empty", nameof(requestId));

            string filePath = GetRequestFilePath(requestId);

            if (!File.Exists(filePath))
            {
                _logger.LogWarning("Request file not found for deletion: {FilePath}", filePath);
                return false;
            }

            try
            {
                File.Delete(filePath);
                _logger.LogInformation("Deleted request file {FilePath}", filePath);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete request file {FilePath}", filePath);
                throw;
            }
        }

        public string GetRequestFilePath(string requestId)
        {
            // Sanitize the request ID to ensure it's a valid filename
            var sanitizedId = SanitizeFileName(requestId);
            return Path.Combine(_storeRequestsFolder, $"{sanitizedId}.xml");
        }

        private string ResolvePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return Path.Combine(AppContext.BaseDirectory, "requests");

            // Replace %APPPATH% placeholder with application directory
            path = path.Replace("%APPPATH%", AppContext.BaseDirectory, StringComparison.OrdinalIgnoreCase);

            // If relative path, make it relative to application directory
            if (!Path.IsPathRooted(path))
            {
                path = Path.Combine(AppContext.BaseDirectory, path);
            }

            return Path.GetFullPath(path);
        }

        private string SanitizeFileName(string fileName)
        {
            // Remove invalid file name characters
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
            return string.IsNullOrWhiteSpace(sanitized) ? "unknown" : sanitized;
        }
    }
}
