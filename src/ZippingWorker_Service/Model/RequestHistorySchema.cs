using System.Text.Json.Serialization;

namespace ZippingWorker_Service.Model
{
    /// <summary>
    /// Represents a collection of request histories
    /// </summary>
    public class RequestHistorySchema
    {
        /// <summary>
        /// Schema version for migration purposes
        /// </summary>
        [JsonPropertyName("schemaVersion")]
        public string SchemaVersion { get; set; } = "1.0";

        /// <summary>
        /// Array of request history records
        /// </summary>
        [JsonPropertyName("requests")]
        public List<RequestHistoryItem> Requests { get; set; } = new();
    }

    /// <summary>
    /// Represents a single request history item
    /// </summary>
    public class RequestHistoryItem
    {
        /// <summary>
        /// Unique identifier for the request
        /// </summary>
        [JsonPropertyName("id")]
        public required string Id { get; set; }

        /// <summary>
        /// Current status of the request
        /// </summary>
        [JsonPropertyName("status")]
        [JsonConverter(typeof(LowercaseJsonStringEnumConverter<RequestStatus>))]
        public required RequestStatus Status { get; set; }

        /// <summary>
        /// Timestamp when the request was received
        /// </summary>
        [JsonPropertyName("requested")]
        public required DateTime Requested { get; set; }

        /// <summary>
        /// Timestamp when processing started (nullable)
        /// </summary>
        [JsonPropertyName("started")]
        public DateTime? Started { get; set; }

        /// <summary>
        /// Timestamp when processing finished (nullable)
        /// </summary>
        [JsonPropertyName("finish")]
        public DateTime? Finish { get; set; }

        /// <summary>
        /// Compression level used for the archive
        /// </summary>
        [JsonPropertyName("compressionLevel")]
        public string? CompressionLevel { get; set; }

        /// <summary>
        /// Final location of the created archive file
        /// </summary>
        [JsonPropertyName("archiveLocation")]
        public string? ArchiveLocation { get; set; }

        /// <summary>
        /// Number of files in the archive
        /// </summary>
        [JsonPropertyName("fileCount")]
        public int FileCount { get; set; }

        /// <summary>
        /// Priority level for processing this request (lower number = higher priority)
        /// </summary>
        [JsonPropertyName("priority")]
        public int Priority { get; set; }
    }

    /// <summary>
    /// Status of a zip request
    /// </summary>
    public enum RequestStatus
    {
        [JsonPropertyName("pending")]
        Pending,

        [JsonPropertyName("queued")]
        Queued,

        [JsonPropertyName("processing")]
        Processing,

        [JsonPropertyName("completed")]
        Completed,

        [JsonPropertyName("failed")]
        Failed,

        [JsonPropertyName("cancelled")]
        Cancelled
    }
}
