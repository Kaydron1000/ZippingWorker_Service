using ZippingWorker_Service.Model;
using System.Text.Json;

namespace ZippingWorker_Service.Examples
{
    /// <summary>
    /// Examples demonstrating schema version migration for request history collections
    /// </summary>
    public static class RequestHistoryMigrationExample
    {
        /// <summary>
        /// Example: Check if a collection version is supported
        /// </summary>
        public static async Task CheckVersionExample(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Console.WriteLine("File not found");
                return;
            }

            var json = await File.ReadAllTextAsync(filePath);

            // Extract version
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("schemaVersion", out var versionElement))
            {
                var version = versionElement.GetString();
                var isSupported = RequestHistoryValidator.IsVersionSupported(version!);

                Console.WriteLine($"Collection schema version: {version}");
                Console.WriteLine($"Is supported: {isSupported}");

                if (!isSupported)
                {
                    Console.WriteLine("Migration may be required");
                }
            }
            else
            {
                Console.WriteLine("No schema version found - may be legacy format");
            }
        }

        /// <summary>
        /// Example: Migrate a hypothetical v0.9 collection to v1.0
        /// </summary>
        public static async Task<string> MigrateFromV0_9ToV1_0(string oldVersionJson)
        {
            // In this example, assume v0.9 didn't have schemaVersion at collection level
            // and used different status names
            using var doc = JsonDocument.Parse(oldVersionJson);

            var migratedCollection = new RequestHistorySchema
            {
                SchemaVersion = "1.0",
                Requests = new List<RequestHistoryItem>()
            };

            if (doc.RootElement.TryGetProperty("requests", out var requestsArray))
            {
                foreach (var item in requestsArray.EnumerateArray())
                {
                    var migratedItem = new RequestHistoryItem
                    {
                        Id = item.GetProperty("id").GetString()!,
                        Status = MapOldStatusToNew(item.GetProperty("status").GetString()!),
                        Requested = item.GetProperty("requested").GetDateTime()
                    };

                    if (item.TryGetProperty("started", out var startedProp) && startedProp.ValueKind != JsonValueKind.Null)
                    {
                        migratedItem.Started = startedProp.GetDateTime();
                    }

                    if (item.TryGetProperty("finish", out var finishProp) && finishProp.ValueKind != JsonValueKind.Null)
                    {
                        migratedItem.Finish = finishProp.GetDateTime();
                    }

                    migratedCollection.Requests.Add(migratedItem);
                }
            }

            var validator = await RequestHistoryValidator.CreateAsync("Model/RequestHistorySchema.json");
            return validator.Serialize(migratedCollection);
        }

        /// <summary>
        /// Maps old status values to new ones (hypothetical migration example)
        /// </summary>
        private static RequestStatus MapOldStatusToNew(string oldStatus)
        {
            // Example: maybe v0.9 used "done" instead of "completed"
            return oldStatus.ToLower() switch
            {
                "done" => RequestStatus.Completed,
                "pending" => RequestStatus.Pending,
                "queued" => RequestStatus.Queued,
                "processing" => RequestStatus.Processing,
                "failed" => RequestStatus.Failed,
                "cancelled" => RequestStatus.Cancelled,
                _ => RequestStatus.Pending
            };
        }

        /// <summary>
        /// Example: Load collection with automatic migration
        /// </summary>
        public static async Task<RequestHistorySchema?> LoadWithAutoMigration(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return new RequestHistorySchema();
            }

            var json = await File.ReadAllTextAsync(filePath);
            var validator = await RequestHistoryValidator.CreateAsync("Model/RequestHistorySchema.json");

            // Try to deserialize and detect version
            var collection = validator.Deserialize(json, out var errors, out var detectedVersion);

            if (collection != null && errors.Count == 0)
            {
                // Already valid v1.0
                Console.WriteLine($"Loaded valid collection (version {detectedVersion})");
                return collection;
            }

            // Check if migration is needed
            if (detectedVersion == "0.9")
            {
                Console.WriteLine("Detected v0.9 collection, migrating...");
                var migratedJson = await MigrateFromV0_9ToV1_0(json);
                collection = validator.Deserialize(migratedJson, out errors);

                if (collection != null && errors.Count == 0)
                {
                    Console.WriteLine("Migration successful");
                    // Optionally save migrated version
                    await File.WriteAllTextAsync(filePath + ".migrated", migratedJson);
                    return collection;
                }
            }

            Console.WriteLine("Could not load or migrate collection:");
            foreach (var error in errors)
            {
                Console.WriteLine($"  - {error.Kind}: {error.Path}");
            }

            return null;
        }
    }
}
