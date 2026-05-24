# Status API Documentation

The Status API provides endpoints to track and query the lifecycle of zip requests.

## Status Object

Each zip request has the following status properties:

- **RequestId**: Unique 6-character identifier for the request
- **RequestTime**: UTC timestamp when request was received
- **StartedTime**: UTC timestamp when processing started (null if not started)
- **FinishedTime**: UTC timestamp when processing finished (null if not finished)
- **CompressionLevel**: Archive compression level used
- **ArchiveFileLocation**: Path to the created archive file
- **DeletedInput**: Whether input files were deleted after archiving
- **ZippingValidated**: Whether the archive was validated after creation
- **Status**: Current status (Queued, InProgress, Completed, Failed)
- **ErrorMessage**: Error message if the request failed
- **FileCount**: Number of files in the archive
- **ArchiveSizeBytes**: Size of the final archive in bytes

## Endpoints

### Get Status by Request ID

**GET** `/api/status/{requestId}`

Returns the status of a specific request.

**Response:** 200 OK with `ZipRequestStatus` object, or 404 if not found.

**Example:**
```bash
curl http://localhost:8080/api/status/AbC123
```

---

### Get Queued Requests

**GET** `/api/status/queued`

Returns all requests currently in the queue (not started yet).

**Response:** 200 OK
```json
{
  "count": 3,
  "requests": [...]
}
```

---

### Get In-Progress Request

**GET** `/api/status/inprogress`

Returns the currently processing request (if any).

**Response:** 200 OK with request object, or 204 No Content if nothing is processing.

---

### Get Completed Requests

**GET** `/api/status/completed`

Returns all completed requests (successful and failed).

**Response:** 200 OK
```json
{
  "count": 10,
  "requests": [...]
}
```

---

### Get All Requests

**GET** `/api/status/all`

Returns all requests across all states.

**Response:** 200 OK
```json
{
  "total": 15,
  "queued": 3,
  "inProgress": 1,
  "completed": 10,
  "failed": 1,
  "requests": [...]
}
```

---

### Get Summary

**GET** `/api/status/summary`

Returns a summary of system status with counts and current request information.

**Response:** 200 OK
```json
{
  "summary": {
	"queued": 3,
	"inProgress": 1,
	"completed": 10,
	"failed": 1,
	"total": 15
  },
  "currentRequest": {
	"requestId": "AbC123",
	"startedTime": "2024-01-15T10:30:00Z",
	"archiveFileLocation": "/output/archive.7z",
	"fileCount": 150,
	"elapsedSeconds": 45.2
  }
}
```

---

### Cleanup Old Requests

**POST** `/api/status/cleanup?olderThanHours=24`

Removes completed/failed requests older than the specified number of hours.

**Query Parameters:**
- `olderThanHours` (default: 24): Remove requests completed more than this many hours ago

**Response:** 200 OK
```json
{
  "message": "Removed 5 old requests",
  "removed": 5,
  "remaining": 10
}
```

---

## Workflow Integration

When submitting a zip request via `/api/zipinfo/binary` or `/api/zipinfo/xml`, the response now includes a `RequestId`:

```json
{
  "message": "Zip request queued successfully",
  "requestId": "AbC123",
  "outputPath": "/output/archive.7z",
  "fileCount": 150,
  "compressionLevel": "Ultra",
  "validateZipping": "Extract"
}
```

Use this `RequestId` to track the request status through the Status API endpoints.

## Example Workflow

1. **Submit a zip request:**
   ```bash
   curl -X POST http://localhost:8080/api/zipinfo/xml \
	 -H "Content-Type: application/xml" \
	 -d @request.xml
   ```

   Response includes `"requestId": "AbC123"`

2. **Check request status:**
   ```bash
   curl http://localhost:8080/api/status/AbC123
   ```

3. **Monitor summary:**
   ```bash
   curl http://localhost:8080/api/status/summary
   ```

4. **Cleanup old completed requests:**
   ```bash
   curl -X POST "http://localhost:8080/api/status/cleanup?olderThanHours=48"
   ```
