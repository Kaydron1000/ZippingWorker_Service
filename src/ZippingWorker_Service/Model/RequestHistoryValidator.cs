using NJsonSchema;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ZippingWorker_Service.Model
{
    /// <summary>
    /// Validates and serializes/deserializes request history collections against the JSON schema
    /// </summary>
    public class RequestHistoryValidator
    {
        public const string CurrentSchemaVersion = "1.0";

        private readonly JsonSchema _schema;
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };

        public RequestHistoryValidator(JsonSchema schema)
        {
            _schema = schema;
        }

        /// <summary>
        /// Load schema from embedded resource or file
        /// </summary>
        public static async Task<RequestHistoryValidator> CreateAsync(string schemaPath)
        {
            var schemaJson = await File.ReadAllTextAsync(schemaPath);
            var schema = await JsonSchema.FromJsonAsync(schemaJson);
            return new RequestHistoryValidator(schema);
        }

        /// <summary>
        /// Validate JSON string against schema
        /// </summary>
        public ICollection<NJsonSchema.Validation.ValidationError> Validate(string json)
        {
            return _schema.Validate(json);
        }

        /// <summary>
        /// Deserialize and validate JSON string with version checking
        /// </summary>
        public RequestHistorySchema? Deserialize(string json, out ICollection<NJsonSchema.Validation.ValidationError> errors, out string? detectedVersion)
        {
            errors = Validate(json);
            detectedVersion = null;

            if (errors.Count > 0)
            {
                return null;
            }

            var history = JsonSerializer.Deserialize<RequestHistorySchema>(json, _jsonOptions);

            if (history != null)
            {
                detectedVersion = history.SchemaVersion;

                // Check if version is supported
                if (history.SchemaVersion != CurrentSchemaVersion)
                {
                    // Future: Handle migration logic here
                    // For now, we just track the version
                }
            }

            return history;
        }

        /// <summary>
        /// Deserialize and validate JSON string (simple version without version output)
        /// </summary>
        public RequestHistorySchema? Deserialize(string json, out ICollection<NJsonSchema.Validation.ValidationError> errors)
        {
            return Deserialize(json, out errors, out _);
        }

        /// <summary>
        /// Serialize RequestHistory object to JSON
        /// </summary>
        public string Serialize(RequestHistorySchema history)
        {
            return JsonSerializer.Serialize(history, _jsonOptions);
        }

        /// <summary>
        /// Validate a RequestHistory object
        /// </summary>
        public bool ValidateObject(RequestHistorySchema history, out ICollection<NJsonSchema.Validation.ValidationError> errors)
        {
            var json = Serialize(history);
            errors = Validate(json);
            return errors.Count == 0;
        }

        /// <summary>
        /// Check if a version string is compatible with the current version
        /// </summary>
        public static bool IsVersionSupported(string version)
        {
            // For now, only exact match is supported
            // Future: Add logic for backwards compatibility
            return version == CurrentSchemaVersion;
        }
    }
}
