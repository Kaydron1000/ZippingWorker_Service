using Prometheus;

namespace ZippingWorker_Service.Services
{
    public interface IMetricsService
    {
        void RecordZipRequestQueued();
        void RecordZipRequestStarted();
        void RecordZipRequestCompleted(bool success, double durationSeconds, long zipSizeBytes, int fileCount);
        void RecordZipValidation(bool passed, double durationSeconds);
        void RecordFileDeletion(int successCount, int failedCount);
        void RecordCopyVerification(bool success);
        int GetQueueDepth();
        void SetQueueDepth(int depth);
    }

    public class MetricsService : IMetricsService
    {
        // Counters
        private static readonly Counter ZipRequestsQueued = Metrics.CreateCounter(
            "zipping_requests_queued_total",
            "Total number of zip requests queued");

        private static readonly Counter ZipRequestsStarted = Metrics.CreateCounter(
            "zipping_requests_started_total",
            "Total number of zip requests started processing");

        private static readonly Counter ZipRequestsCompleted = Metrics.CreateCounter(
            "zipping_requests_completed_total",
            "Total number of zip requests completed",
            new CounterConfiguration { LabelNames = new[] { "status" } });

        private static readonly Counter ZipValidationResults = Metrics.CreateCounter(
            "zipping_validation_results_total",
            "Total number of zip validation results",
            new CounterConfiguration { LabelNames = new[] { "result" } });

        private static readonly Counter FilesDeleted = Metrics.CreateCounter(
            "zipping_files_deleted_total",
            "Total number of input files deleted",
            new CounterConfiguration { LabelNames = new[] { "status" } });

        private static readonly Counter CopyVerifications = Metrics.CreateCounter(
            "zipping_copy_verifications_total",
            "Total number of copy verifications",
            new CounterConfiguration { LabelNames = new[] { "result" } });

        // Gauges
        private static readonly Gauge QueueDepth = Metrics.CreateGauge(
            "zipping_queue_depth",
            "Current number of zip requests in queue");

        private static readonly Gauge LastZipSizeBytes = Metrics.CreateGauge(
            "zipping_last_zip_size_bytes",
            "Size of the last created zip file in bytes");

        private static readonly Gauge LastZipFileCount = Metrics.CreateGauge(
            "zipping_last_zip_file_count",
            "Number of files in the last created zip");

        // Histograms
        private static readonly Histogram ZipCreationDuration = Metrics.CreateHistogram(
            "zipping_creation_duration_seconds",
            "Duration of zip creation operations",
            new HistogramConfiguration
            {
                Buckets = Histogram.ExponentialBuckets(1, 2, 12) // 1s to ~68 minutes
            });

        private static readonly Histogram ZipValidationDuration = Metrics.CreateHistogram(
            "zipping_validation_duration_seconds",
            "Duration of zip validation operations",
            new HistogramConfiguration
            {
                Buckets = Histogram.ExponentialBuckets(0.1, 2, 12) // 0.1s to ~6.8 minutes
            });

        private static readonly Histogram ZipSizeBytes = Metrics.CreateHistogram(
            "zipping_zip_size_bytes",
            "Size distribution of created zip files",
            new HistogramConfiguration
            {
                Buckets = Histogram.ExponentialBuckets(1024, 4, 12) // 1KB to ~16GB
            });

        private static readonly Histogram ZipFileCount = Metrics.CreateHistogram(
            "zipping_zip_file_count",
            "Number of files per zip archive",
            new HistogramConfiguration
            {
                Buckets = Histogram.ExponentialBuckets(1, 4, 10) // 1 to ~1M files
            });

        static MetricsService()
        {
            // Static constructor to ensure metrics are registered with Prometheus
            // This forces initialization of all static readonly fields when the type is first accessed
        }

        public void RecordZipRequestQueued()
        {
            ZipRequestsQueued.Inc();
        }

        public void RecordZipRequestStarted()
        {
            ZipRequestsStarted.Inc();
        }

        public void RecordZipRequestCompleted(bool success, double durationSeconds, long zipSizeBytes, int fileCount)
        {
            ZipRequestsCompleted.WithLabels(success ? "success" : "failure").Inc();

            if (success)
            {
                ZipCreationDuration.Observe(durationSeconds);
                ZipSizeBytes.Observe(zipSizeBytes);
                ZipFileCount.Observe(fileCount);
                LastZipSizeBytes.Set(zipSizeBytes);
                LastZipFileCount.Set(fileCount);
            }
        }

        public void RecordZipValidation(bool passed, double durationSeconds)
        {
            ZipValidationResults.WithLabels(passed ? "passed" : "failed").Inc();
            ZipValidationDuration.Observe(durationSeconds);
        }

        public void RecordFileDeletion(int successCount, int failedCount)
        {
            FilesDeleted.WithLabels("success").Inc(successCount);
            FilesDeleted.WithLabels("failed").Inc(failedCount);
        }

        public void RecordCopyVerification(bool success)
        {
            CopyVerifications.WithLabels(success ? "success" : "failed").Inc();
        }

        public int GetQueueDepth()
        {
            return (int)QueueDepth.Value;
        }

        public void SetQueueDepth(int depth)
        {
            QueueDepth.Set(depth);
        }
    }
}
