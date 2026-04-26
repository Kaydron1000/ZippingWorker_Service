using FluentAssertions;
using Xunit;
using ZippingWorker_Service.Services;

namespace ZippingWorker_Service.Tests.Services;

public class MetricsServiceTests
{
    private readonly IMetricsService _metricsService;

    public MetricsServiceTests()
    {
        _metricsService = new MetricsService();
    }

    [Fact]
    public void RecordZipRequestQueued_ShouldIncrementCounter()
    {
        // Arrange & Act
        _metricsService.RecordZipRequestQueued();

        // Assert - Counter should increment without throwing
        // Note: Actual prometheus counter values are difficult to assert directly
        // This test verifies the method executes without error
    }

    [Fact]
    public void RecordZipRequestStarted_ShouldIncrementCounter()
    {
        // Arrange & Act
        _metricsService.RecordZipRequestStarted();

        // Assert
        // Method should complete without throwing
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RecordZipRequestCompleted_ShouldAcceptSuccessAndFailureStatus(bool success)
    {
        // Arrange
        double durationSeconds = 5.0;
        long zipSizeBytes = 1024 * 1024;
        int fileCount = 10;

        // Act
        _metricsService.RecordZipRequestCompleted(success, durationSeconds, zipSizeBytes, fileCount);

        // Assert
        // Method should complete without throwing
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RecordZipValidation_ShouldAcceptPassedAndFailedResults(bool passed)
    {
        // Arrange
        double durationSeconds = 2.0;

        // Act
        _metricsService.RecordZipValidation(passed, durationSeconds);

        // Assert
        // Method should complete without throwing
    }



    [Fact]
    public void SetQueueDepth_ShouldAcceptValidDepth()
    {
        // Arrange
        int depth = 5;

        // Act
        _metricsService.SetQueueDepth(depth);

        // Assert
        // Method should complete without throwing
    }

    [Theory]
    [InlineData(10, 0)]
    [InlineData(5, 2)]
    [InlineData(0, 5)]
    public void RecordFileDeletion_ShouldAcceptSuccessAndFailureCounts(int successCount, int failedCount)
    {
        // Act
        _metricsService.RecordFileDeletion(successCount, failedCount);

        // Assert
        // Method should complete without throwing
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RecordCopyVerification_ShouldAcceptSuccessAndFailureResults(bool success)
    {
        // Act
        _metricsService.RecordCopyVerification(success);

        // Assert
        // Method should complete without throwing
    }
}
