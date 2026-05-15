# Prometheus Metrics

The ZippingWorkerService exposes Prometheus metrics at the `/metrics` endpoint.

## Endpoint

```
GET http://localhost:{configured_port}/metrics
```

**Note:** The port is configured via the `serviceport` attribute in `config.xml` (default: 5000).

## Available Metrics

### Counters

- **`zipping_requests_queued_total`**  
  Total number of zip requests queued

- **`zipping_requests_started_total`**  
  Total number of zip requests that started processing

- **`zipping_requests_completed_total{status="success|failure"}`**  
  Total number of completed zip requests (labeled by status)

- **`zipping_validation_results_total{result="passed|failed"}`**  
  Total number of zip validation results

- **`zipping_files_deleted_total{status="success|failed"}`**  
  Total number of input files deleted after successful archiving

- **`zipping_directories_deleted_total{status="success|failed"}`**  
  Total number of directories deleted after successful archiving

- **`zipping_copy_verifications_total{result="success|failed"}`**  
  Total number of staging-to-final copy verifications

### Gauges

- **`zipping_queue_depth`**  
  Current number of zip requests waiting in queue

- **`zipping_current_progress_percent`**  
  Current progress percentage of the active zip operation (0-100). Shows real-time archiving progress. Resets to 0 when no operation is in progress.

- **`zipping_last_zip_size_bytes`**  
  Size of the most recently created zip file in bytes

- **`zipping_last_zip_file_count`**  
  Number of files in the most recently created zip archive

### Histograms

- **`zipping_creation_duration_seconds`**  
  Duration of zip creation operations (buckets: 1s to ~68 minutes)

- **`zipping_validation_duration_seconds`**  
  Duration of zip validation operations (buckets: 0.1s to ~6.8 minutes)

- **`zipping_zip_size_bytes`**  
  Size distribution of created zip files (buckets: 1KB to ~16GB)

- **`zipping_zip_file_count`**  
  Number of files per zip archive (buckets: 1 to ~1M files)

## Prometheus Configuration

Add this job to your `prometheus.yml`:

```yaml
scrape_configs:
  - job_name: 'zipping-worker-service'
    scrape_interval: 15s
    static_configs:
      - targets: ['localhost:5000']  # Update this to match your configured serviceport
```

## Example Queries

### Request Success Rate
```promql
rate(zipping_requests_completed_total{status="success"}[5m]) / 
rate(zipping_requests_completed_total[5m]) * 100
```

### Average Zip Creation Time (last 5 minutes)
```promql
rate(zipping_creation_duration_seconds_sum[5m]) / 
rate(zipping_creation_duration_seconds_count[5m])
```

### Total Data Archived (last hour)
```promql
sum(increase(zipping_zip_size_bytes_sum[1h]))
```

### Validation Failure Rate
```promql
rate(zipping_validation_results_total{result="failed"}[5m]) / 
rate(zipping_validation_results_total[5m]) * 100
```

### Files Deleted per Second
```promql
rate(zipping_files_deleted_total{status="success"}[5m])
```

### Directories Deleted per Second
```promql
rate(zipping_directories_deleted_total{status="success"}[5m])
```

## Grafana Dashboard

Example dashboard panels:

1. **Success Rate** - Single Stat with success percentage
2. **Queue Depth** - Graph showing `zipping_queue_depth` over time
3. **Throughput** - Graph showing `rate(zipping_requests_completed_total[5m])`
4. **Zip Size Distribution** - Heatmap of `zipping_zip_size_bytes`
5. **Processing Time** - Graph of P50, P95, P99 latencies from `zipping_creation_duration_seconds`
6. **Validation Results** - Pie chart of passed vs failed validations

## Testing Metrics

```bash
# Queue a zip request (update port to match your configured serviceport)
curl -X POST http://localhost:5000/api/zipinfo/xml \
  -H "Content-Type: application/xml" \
  -d @sample-zipinfo.xml

# View metrics (update port to match your configured serviceport)
curl http://localhost:5000/metrics
```
