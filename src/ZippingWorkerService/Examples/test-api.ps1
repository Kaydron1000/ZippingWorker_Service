# Test script for ZippingWorkerService API
# This demonstrates how to submit files for zipping

$baseUrl = "http://localhost:5000"

# Test 1: Simple zip request
$testRequest = @{
    files = @(
        @{
            sourcePath = "C:\Windows\System32\drivers\etc\hosts"
            archivePath = "hosts.txt"
        }
    )
    outputArchivePath = "C:\temp\test-archive.zip"
} | ConvertTo-Json

Write-Host "Sending zip request..." -ForegroundColor Cyan
try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/zip" -Method Post -Body $testRequest -ContentType "application/json"
    Write-Host "Success!" -ForegroundColor Green
    Write-Host "Response: $($response | ConvertTo-Json)" -ForegroundColor Yellow
}
catch {
    Write-Host "Error: $_" -ForegroundColor Red
}

# Test 2: Multiple files
$multiFileRequest = @{
    files = @(
        @{
            sourcePath = "C:\Windows\System32\drivers\etc\hosts"
            archivePath = "config/hosts.txt"
        },
        @{
            sourcePath = "C:\Windows\System32\drivers\etc\networks"
            archivePath = "config/networks.txt"
        }
    )
    outputArchivePath = "C:\temp\multi-file-archive.zip"
} | ConvertTo-Json

Write-Host "`nSending multi-file zip request..." -ForegroundColor Cyan
try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/zip" -Method Post -Body $multiFileRequest -ContentType "application/json"
    Write-Host "Success!" -ForegroundColor Green
    Write-Host "Response: $($response | ConvertTo-Json)" -ForegroundColor Yellow
}
catch {
    Write-Host "Error: $_" -ForegroundColor Red
}
