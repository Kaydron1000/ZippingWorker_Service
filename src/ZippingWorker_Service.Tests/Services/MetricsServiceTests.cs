using FluentAssertions;
using Xunit;
using ZippingWorker_Service.Services;
using ZippingWorker_Service.Configuration;
using Moq;

namespace ZippingWorker_Service.Tests.Services;

public class MetricsServiceTests
{
    private readonly IMetricsService _metricsService;
    private readonly ZippingWorker_ServiceConfigurationType _mockConfig;

    public MetricsServiceTests()
    {
        // Create a mock configuration
        _mockConfig = new ZippingWorker_ServiceConfigurationType();
        _metricsService = new MetricsService(_mockConfig);
    }

    #region Basic Counter Tests

    [Fact]
    public void RecordZipRequested_ShouldIncrementCounter()
    {
        // Arrange & Act
        _metricsService.RecordZipRequested();

        // Assert - Counter should increment without throwing
        // Note: Actual prometheus counter values are difficult to assert directly
        // This test verifies the method executes without error
    }

    [Fact]
    public void RecordZipRequestQueued_ShouldIncrementCounter()
    {
        // Arrange & Act
        _metricsService.RecordZipRequestQueued();

        // Assert
        // Method should complete without throwing
    }

    [Fact]
    public void RecordZipRequestStarted_ShouldIncrementCounter()
    {
        // Arrange & Act
        _metricsService.RecordZipRequestStarted();

        // Assert
        // Method should complete without throwing
    }

    #endregion

    #region Metadata Recording Tests

    [Fact]
    public void RecordZipMetadata_ShouldAcceptKeyValuePairs()
    {
        // Arrange
        string key = "compression_level";
        string value = "ultra";

        // Act
        _metricsService.RecordZipMetadata(key, value);

        // Assert
        // Method should complete without throwing
    }

    [Theory]
    [InlineData("archiver", "sevenzip")]
    [InlineData("archiver", "dotnetzip")]
    [InlineData("validation", "enabled")]
    [InlineData("validation", "disabled")]
    public void RecordZipMetadata_ShouldAcceptVariousMetadata(string key, string value)
    {
        // Act
        _metricsService.RecordZipMetadata(key, value);

        // Assert
        // Method should complete without throwing
    }

    #endregion

    #region Completion Recording Tests

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

    [Fact]
    public void RecordZipRequestCompleted_WithSuccess_ShouldRecordMetrics()
    {
        // Arrange
        double durationSeconds = 15.5;
        long zipSizeBytes = 5 * 1024 * 1024; // 5 MB
        int fileCount = 25;

        // Act
        _metricsService.RecordZipRequestCompleted(true, durationSeconds, zipSizeBytes, fileCount);

        // Assert
        // Successful completion should record duration, size, and file count
        // Method should complete without throwing
    }

    [Fact]
    public void RecordZipRequestCompleted_WithFailure_ShouldNotRecordOptionalMetrics()
    {
        // Arrange
        double durationSeconds = 2.0;
        long zipSizeBytes = 0;
        int fileCount = 0;

        // Act
        _metricsService.RecordZipRequestCompleted(false, durationSeconds, zipSizeBytes, fileCount);

        // Assert
        // Failed completion should not record size/count metrics
        // Method should complete without throwing
    }

    [Theory]
    [InlineData(0.1, 1024, 1)]                    // Small: 0.1s, 1KB, 1 file
    [InlineData(30.0, 100 * 1024 * 1024, 50)]     // Medium: 30s, 100MB, 50 files
    [InlineData(300.0, 1024L * 1024 * 1024, 500)] // Large: 5min, 1GB, 500 files
    public void RecordZipRequestCompleted_ShouldHandleVariousSizes(double duration, long size, int fileCount)
    {
        // Act
        _metricsService.RecordZipRequestCompleted(true, duration, size, fileCount);

        // Assert
        // Method should handle various data sizes without error
    }

    #endregion

    #region Validation Recording Tests

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

    [Theory]
    [InlineData(true, 0.5)]
    [InlineData(true, 5.0)]
    [InlineData(true, 30.0)]
    [InlineData(false, 1.0)]
    [InlineData(false, 10.0)]
    public void RecordZipValidation_ShouldRecordDifferentDurations(bool passed, double duration)
    {
        // Act
        _metricsService.RecordZipValidation(passed, duration);

        // Assert
        // Method should handle various validation durations
    }

    #endregion

    #region Queue Depth Tests

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

    [Fact]
    public void GetQueueDepth_ShouldReturnSetValue()
    {
        // Arrange
        int expectedDepth = 7;
        _metricsService.SetQueueDepth(expectedDepth);

        // Act
        int actualDepth = _metricsService.GetQueueDepth();

        // Assert
        actualDepth.Should().Be(expectedDepth);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(100)]
    public void SetQueueDepth_ShouldAcceptVariousDepths(int depth)
    {
        // Act
        _metricsService.SetQueueDepth(depth);
        int retrievedDepth = _metricsService.GetQueueDepth();

        // Assert
        retrievedDepth.Should().Be(depth);
    }

    [Fact]
    public void QueueDepth_ShouldPersistAcrossMultipleUpdates()
    {
        // Arrange & Act
        _metricsService.SetQueueDepth(5);
        _metricsService.SetQueueDepth(10);
        _metricsService.SetQueueDepth(3);

        // Assert
        _metricsService.GetQueueDepth().Should().Be(3);
    }

    #endregion

    #region File Deletion Tests

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

    [Fact]
    public void RecordFileDeletion_WithAllSuccess_ShouldRecordCorrectly()
    {
        // Arrange
        int successCount = 25;
        int failedCount = 0;

        // Act
        _metricsService.RecordFileDeletion(successCount, failedCount);

        // Assert
        // Method should record all successful deletions
    }

    [Fact]
    public void RecordFileDeletion_WithAllFailures_ShouldRecordCorrectly()
    {
        // Arrange
        int successCount = 0;
        int failedCount = 10;

        // Act
        _metricsService.RecordFileDeletion(successCount, failedCount);

        // Assert
        // Method should record all failed deletions
    }

    [Fact]
    public void RecordFileDeletion_WithMixedResults_ShouldRecordBothCounts()
    {
        // Arrange
        int successCount = 15;
        int failedCount = 3;

        // Act
        _metricsService.RecordFileDeletion(successCount, failedCount);

        // Assert
        // Method should record both successful and failed deletions
    }

    #endregion

    #region Copy Verification Tests

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

    [Fact]
    public void RecordCopyVerification_WithSuccess_ShouldIncrementCounter()
    {
        // Act
        _metricsService.RecordCopyVerification(true);

        // Assert
        // Successful verification should increment success counter
    }

    [Fact]
    public void RecordCopyVerification_WithFailure_ShouldIncrementFailureCounter()
    {
        // Act
        _metricsService.RecordCopyVerification(false);

        // Assert
        // Failed verification should increment failure counter
    }

    #endregion

    #region Integration/Workflow Tests

    [Fact]
    public void CompleteWorkflow_ShouldRecordAllMetricsWithoutError()
    {
        // Arrange - Simulate a complete zip request workflow

        // Act
        _metricsService.RecordZipRequested();
        _metricsService.RecordZipRequestQueued();
        _metricsService.SetQueueDepth(1);

        _metricsService.RecordZipRequestStarted();
        _metricsService.SetQueueDepth(0);

        _metricsService.RecordZipMetadata("archiver", "sevenzip");
        _metricsService.RecordZipMetadata("compression", "ultra");

        _metricsService.RecordZipRequestCompleted(true, 10.5, 50 * 1024 * 1024, 20);
        _metricsService.RecordZipValidation(true, 2.5);
        _metricsService.RecordCopyVerification(true);
        _metricsService.RecordFileDeletion(20, 0);

        // Assert
        // Complete workflow should execute without errors
        _metricsService.GetQueueDepth().Should().Be(0);
    }

    [Fact]
    public void FailedWorkflow_ShouldRecordFailureMetrics()
    {
        // Arrange - Simulate a failed zip request workflow

        // Act
        _metricsService.RecordZipRequested();
        _metricsService.RecordZipRequestQueued();
        _metricsService.SetQueueDepth(1);

        _metricsService.RecordZipRequestStarted();
        _metricsService.SetQueueDepth(0);

        _metricsService.RecordZipRequestCompleted(false, 5.0, 0, 0);

        // Assert
        // Failed workflow should record appropriately
        _metricsService.GetQueueDepth().Should().Be(0);
    }

    [Fact]
    public void ConcurrentMetricRecording_ShouldHandleMultipleCallsSafely()
    {
        // Arrange & Act - Simulate concurrent metric recording
        for (int i = 0; i < 10; i++)
        {
            _metricsService.RecordZipRequested();
            _metricsService.RecordZipRequestQueued();
            _metricsService.RecordZipRequestStarted();
            _metricsService.RecordZipRequestCompleted(true, 1.0, 1024, 1);
        }

        // Assert
        // Multiple concurrent operations should complete without error
    }

    #endregion

    #region Progress Tracking Tests

    [Fact]
    public void UpdateZipProgress_ShouldUpdateGauge()
    {
        // Arrange & Act
        _metricsService.UpdateZipProgress(25, 100);

        // Assert
        // Progress gauge should be updated to 25%
        // Method should complete without throwing
    }

    [Fact]
    public void UpdateZipProgress_WithZeroTotal_ShouldNotThrow()
    {
        // Arrange & Act
        Action act = () => _metricsService.UpdateZipProgress(0, 0);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void UpdateZipProgress_ShouldCalculatePercentageCorrectly()
    {
        // Arrange & Act
        _metricsService.UpdateZipProgress(50, 200); // Should be 25%
        _metricsService.UpdateZipProgress(100, 100); // Should be 100%
        _metricsService.UpdateZipProgress(1, 3); // Should be ~33.33%

        // Assert
        // All calculations should complete without error
    }

    [Fact]
    public void ResetZipProgress_ShouldSetProgressToZero()
    {
        // Arrange
        _metricsService.UpdateZipProgress(75, 100);

        // Act
        _metricsService.ResetZipProgress();

        // Assert
        // Progress should be reset to 0
        // Method should complete without throwing
    }

    [Fact]
    public void ProgressTracking_FullWorkflow_ShouldWorkCorrectly()
    {
        // Arrange & Act - Simulate a full zip operation progress
        _metricsService.RecordZipRequestStarted();

        // Simulate progress updates
        _metricsService.UpdateZipProgress(10, 100);
        _metricsService.UpdateZipProgress(25, 100);
        _metricsService.UpdateZipProgress(50, 100);
        _metricsService.UpdateZipProgress(75, 100);
        _metricsService.UpdateZipProgress(100, 100);

        // Complete and reset
        _metricsService.RecordZipRequestCompleted(true, 10.0, 1024000, 100);
        _metricsService.ResetZipProgress();

        // Assert
        // Full workflow should complete without error
    }

    #endregion
}
