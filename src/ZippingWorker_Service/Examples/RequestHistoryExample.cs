using ZippingWorker_Service.Model;

namespace ZippingWorker_Service.Examples
{
    /// <summary>
    /// Examples demonstrating the usage of RequestHistorySchema (collection)
    /// </summary>
    public static class RequestHistoryExample
    {
        /// <summary>
        /// Creates a sample collection with multiple request histories
        /// </summary>
        public static RequestHistorySchema CreateSampleCollection()
        {
            var collection = new RequestHistorySchema
            {
                SchemaVersion = "1.0",
                Requests = new List<RequestHistoryItem>
                {
                    new RequestHistoryItem
                    {
                        Id = Guid.NewGuid().ToString(),
                        Status = RequestStatus.Completed,
                        Requested = DateTime.UtcNow.AddHours(-2),
                        Started = DateTime.UtcNow.AddHours(-2).AddMinutes(5),
                        Finish = DateTime.UtcNow.AddHours(-1)
                    },
                    new RequestHistoryItem
                    {
                        Id = Guid.NewGuid().ToString(),
                        Status = RequestStatus.Processing,
                        Requested = DateTime.UtcNow.AddMinutes(-30),
                        Started = DateTime.UtcNow.AddMinutes(-25),
                        Finish = null
                    },
                    new RequestHistoryItem
                    {
                        Id = Guid.NewGuid().ToString(),
                        Status = RequestStatus.Queued,
                        Requested = DateTime.UtcNow.AddMinutes(-10),
                        Started = null,
                        Finish = null
                    }
                }
            };

            return collection;
        }

        /// <summary>
        /// Example: Serialize a collection to JSON
        /// </summary>
        public static async Task SerializationExample()
        {
            var validator = await RequestHistoryValidator.CreateAsync("Model/RequestHistorySchema.json");
            var collection = CreateSampleCollection();

            var json = validator.Serialize(collection);
            Console.WriteLine("Serialized Collection:");
            Console.WriteLine(json);
        }

        /// <summary>
        /// Example: Deserialize and validate JSON
        /// </summary>
        public static async Task DeserializationExample()
        {
            var validator = await RequestHistoryValidator.CreateAsync("Model/RequestHistorySchema.json");

            var json = @"{
  ""schemaVersion"": ""1.0"",
  ""requests"": [
    {
      ""id"": ""550e8400-e29b-41d4-a716-446655440000"",
      ""status"": ""completed"",
      ""requested"": ""2024-01-15T10:00:00Z"",
      ""started"": ""2024-01-15T10:01:00Z"",
      ""finish"": ""2024-01-15T10:05:00Z""
    },
    {
      ""id"": ""660e8400-e29b-41d4-a716-446655440001"",
      ""status"": ""processing"",
      ""requested"": ""2024-01-15T11:00:00Z"",
      ""started"": ""2024-01-15T11:02:00Z"",
      ""finish"": null
    }
  ]
}";

            var collection = validator.Deserialize(json, out var errors);

            if (collection != null)
            {
                Console.WriteLine($"Deserialized collection with {collection.Requests.Count} requests");
                Console.WriteLine($"Schema version: {collection.SchemaVersion}");
            }
            else
            {
                Console.WriteLine("Validation failed:");
                foreach (var error in errors)
                {
                    Console.WriteLine($"  - {error.Kind}: {error.Path}");
                }
            }
        }

        /// <summary>
        /// Example: Validate a collection
        /// </summary>
        public static async Task ValidationExample()
        {
            var validator = await RequestHistoryValidator.CreateAsync("Model/RequestHistorySchema.json");
            var collection = CreateSampleCollection();

            var isValid = validator.ValidateObject(collection, out var errors);

            if (isValid)
            {
                Console.WriteLine("Collection is valid!");
            }
            else
            {
                Console.WriteLine("Collection validation failed:");
                foreach (var error in errors)
                {
                    Console.WriteLine($"  - {error.Kind}: {error.Path}");
                }
            }
        }

        /// <summary>
        /// Example: Add a new request to an existing collection
        /// </summary>
        public static void AddRequestExample()
        {
            var collection = CreateSampleCollection();

            // Add a new request
            var newRequest = new RequestHistoryItem
            {
                Id = Guid.NewGuid().ToString(),
                Status = RequestStatus.Pending,
                Requested = DateTime.UtcNow
            };

            collection.Requests.Add(newRequest);

            Console.WriteLine($"Collection now has {collection.Requests.Count} requests");
        }

        /// <summary>
        /// Example: Track the complete lifecycle of a request
        /// </summary>
        public static async Task TrackRequestLifecycle()
        {
            var validator = await RequestHistoryValidator.CreateAsync("Model/RequestHistorySchema.json");
            var collection = new RequestHistorySchema();

            // Request received
            var requestId = Guid.NewGuid().ToString();
            var newRequest = new RequestHistoryItem
            {
                Id = requestId,
                Status = RequestStatus.Pending,
                Requested = DateTime.UtcNow
            };
            collection.Requests.Add(newRequest);
            Console.WriteLine($"Request {requestId} added with status: {newRequest.Status}");

            // Request queued
            newRequest.Status = RequestStatus.Queued;
            Console.WriteLine($"Request {requestId} updated to status: {newRequest.Status}");

            // Processing started
            await Task.Delay(100); // Simulate some delay
            newRequest.Status = RequestStatus.Processing;
            newRequest.Started = DateTime.UtcNow;
            Console.WriteLine($"Request {requestId} started processing at: {newRequest.Started}");

            // Processing completed
            await Task.Delay(100); // Simulate work
            newRequest.Status = RequestStatus.Completed;
            newRequest.Finish = DateTime.UtcNow;
            Console.WriteLine($"Request {requestId} completed at: {newRequest.Finish}");

            // Validate and save
            var isValid = validator.ValidateObject(collection, out var errors);
            if (isValid)
            {
                var json = validator.Serialize(collection);
                Console.WriteLine("\nFinal history:");
                Console.WriteLine(json);
            }
        }

        /// <summary>
        /// Example: Query requests by status
        /// </summary>
        public static void QueryExample()
        {
            var collection = CreateSampleCollection();

            var completedRequests = collection.Requests
                .Where(r => r.Status == RequestStatus.Completed)
                .ToList();

            var activeRequests = collection.Requests
                .Where(r => r.Status == RequestStatus.Processing || r.Status == RequestStatus.Queued)
                .ToList();

            Console.WriteLine($"Completed: {completedRequests.Count}");
            Console.WriteLine($"Active: {activeRequests.Count}");
        }

        /// <summary>
        /// Example: Load collection from file, add request, and save back
        /// </summary>
        public static async Task LoadAddSaveExample(string filePath)
        {
            var validator = await RequestHistoryValidator.CreateAsync("Model/RequestHistorySchema.json");

            // Load existing collection or create new one
            RequestHistorySchema collection;
            if (File.Exists(filePath))
            {
                var json = await File.ReadAllTextAsync(filePath);
                collection = validator.Deserialize(json, out var errors) ?? new RequestHistorySchema();

                if (errors.Any())
                {
                    Console.WriteLine("Warning: Loaded collection had validation errors");
                }
            }
            else
            {
                collection = new RequestHistorySchema();
            }

            // Add new request
            collection.Requests.Add(new RequestHistoryItem
            {
                Id = Guid.NewGuid().ToString(),
                Status = RequestStatus.Queued,
                Requested = DateTime.UtcNow
            });

            // Save back
            var outputJson = validator.Serialize(collection);
            await File.WriteAllTextAsync(filePath, outputJson);

            Console.WriteLine($"Saved collection with {collection.Requests.Count} requests to {filePath}");
        }
    }
}
