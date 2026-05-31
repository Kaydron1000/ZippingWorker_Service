using FluentAssertions;
using Xunit;
using ZippingWorker_Service.Model;

namespace ZippingWorker_Service.Tests.Model
{
    public class RequestHistorySchemaTests
    {
        [Fact]
        public void RequestHistorySchema_ShouldHaveDefaultVersion()
        {
            // Arrange & Act
            var collection = new RequestHistorySchema();

            // Assert
            collection.SchemaVersion.Should().Be("1.0");
            collection.Requests.Should().NotBeNull();
            collection.Requests.Should().BeEmpty();
        }

        [Fact]
        public void RequestHistorySchema_ShouldAllowVersionOverride()
        {
            // Arrange & Act
            var collection = new RequestHistorySchema
            {
                SchemaVersion = "2.0"
            };

            // Assert
            collection.SchemaVersion.Should().Be("2.0");
        }

        [Fact]
        public async Task RequestHistoryValidator_ShouldValidateEmptyCollection()
        {
            // Arrange
            var validator = await RequestHistoryValidator.CreateAsync("Model/RequestHistorySchema.json");
            var collection = new RequestHistorySchema();

            // Act
            var isValid = validator.ValidateObject(collection, out var errors);

            // Assert
            isValid.Should().BeTrue();
            errors.Should().BeEmpty();
        }

        [Fact]
        public async Task RequestHistoryValidator_ShouldValidateVersionedSchema()
        {
            // Arrange
            var validator = await RequestHistoryValidator.CreateAsync("Model/RequestHistorySchema.json");
            var collection = new RequestHistorySchema
            {
                Requests = new List<RequestHistoryItem>
                {
                    new RequestHistoryItem
                    {
                        Id = Guid.NewGuid().ToString(),
                        Status = RequestStatus.Completed,
                        Requested = DateTime.UtcNow,
                        Started = DateTime.UtcNow.AddMinutes(1),
                        Finish = DateTime.UtcNow.AddMinutes(5)
                    }
                }
            };

            // Act
            var isValid = validator.ValidateObject(collection, out var errors);

            // Assert
            isValid.Should().BeTrue();
            errors.Should().BeEmpty();
            collection.SchemaVersion.Should().Be("1.0");
        }

        [Fact]
        public async Task RequestHistoryValidator_ShouldSerializeWithLowercaseStatus()
        {
            // Arrange
            var validator = await RequestHistoryValidator.CreateAsync("Model/RequestHistorySchema.json");
            var collection = new RequestHistorySchema
            {
                Requests = new List<RequestHistoryItem>
                {
                    new RequestHistoryItem
                    {
                        Id = Guid.NewGuid().ToString(),
                        Status = RequestStatus.Completed,
                        Requested = DateTime.UtcNow
                    }
                }
            };

            // Act
            var json = validator.Serialize(collection);

            // Assert
            json.Should().Contain("\"status\": \"completed\"");
            json.Should().Contain("\"schemaVersion\": \"1.0\"");
            json.Should().Contain("\"requests\":");
        }

        [Fact]
        public async Task RequestHistoryValidator_ShouldDeserializeValidJson()
        {
            // Arrange
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
    }
  ]
}";

            // Act
            var collection = validator.Deserialize(json, out var errors);

            // Assert
            collection.Should().NotBeNull();
            errors.Should().BeEmpty();
            collection!.SchemaVersion.Should().Be("1.0");
            collection.Requests.Should().HaveCount(1);
            collection.Requests[0].Id.Should().Be("550e8400-e29b-41d4-a716-446655440000");
            collection.Requests[0].Status.Should().Be(RequestStatus.Completed);
        }

        [Fact]
        public async Task RequestHistoryValidator_ShouldDetectVersion()
        {
            // Arrange
            var validator = await RequestHistoryValidator.CreateAsync("Model/RequestHistorySchema.json");
            var json = @"{
  ""schemaVersion"": ""1.0"",
  ""requests"": []
}";

            // Act
            var collection = validator.Deserialize(json, out var errors, out var detectedVersion);

            // Assert
            collection.Should().NotBeNull();
            errors.Should().BeEmpty();
            detectedVersion.Should().Be("1.0");
        }

        [Fact]
        public void IsVersionSupported_ShouldReturnTrueForCurrentVersion()
        {
            // Act
            var isSupported = RequestHistoryValidator.IsVersionSupported("1.0");

            // Assert
            isSupported.Should().BeTrue();
        }

        [Fact]
        public void IsVersionSupported_ShouldReturnFalseForUnknownVersion()
        {
            // Act
            var isSupported = RequestHistoryValidator.IsVersionSupported("2.0");

            // Assert
            isSupported.Should().BeFalse();
        }

        [Theory]
        [InlineData("0.9")]
        [InlineData("1.1")]
        [InlineData("invalid")]
        public void IsVersionSupported_ShouldReturnFalseForNonCurrentVersions(string version)
        {
            // Act
            var isSupported = RequestHistoryValidator.IsVersionSupported(version);

            // Assert
            isSupported.Should().BeFalse();
        }

        [Fact]
        public async Task RequestHistoryValidator_ShouldRejectInvalidStatus()
        {
            // Arrange
            var validator = await RequestHistoryValidator.CreateAsync("Model/RequestHistorySchema.json");
            var json = @"{
  ""schemaVersion"": ""1.0"",
  ""requests"": [
    {
      ""id"": ""550e8400-e29b-41d4-a716-446655440000"",
      ""status"": ""invalid-status"",
      ""requested"": ""2024-01-15T10:00:00Z""
    }
  ]
}";

            // Act
            var collection = validator.Deserialize(json, out var errors);

            // Assert
            collection.Should().BeNull();
            errors.Should().NotBeEmpty();
        }

        [Fact]
        public async Task RequestHistoryValidator_ShouldRejectMissingRequiredFields()
        {
            // Arrange
            var validator = await RequestHistoryValidator.CreateAsync("Model/RequestHistorySchema.json");
            var json = @"{
  ""schemaVersion"": ""1.0"",
  ""requests"": [
    {
      ""status"": ""completed"",
      ""requested"": ""2024-01-15T10:00:00Z""
    }
  ]
}";

            // Act
            var collection = validator.Deserialize(json, out var errors);

            // Assert
            collection.Should().BeNull();
            errors.Should().NotBeEmpty();
        }
    }
}
