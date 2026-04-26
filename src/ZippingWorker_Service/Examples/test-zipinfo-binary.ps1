# Test script for ZipInfo API - Binary endpoint
# This demonstrates how to send a serialized ZipInfoType object

$baseUrl = "http://localhost:5000"

# Create a ZipInfoType object
Add-Type -Path ".\bin\Debug\net10.0\ZippingWorker_Service.dll"

$zipInfo = New-Object ZippingWorker_Service.ZipInfoType
$zipInfo.zipfilename = "test-archive.zip"
$zipInfo.zipfilelocation = "C:\temp\output"
$zipInfo.zipcompressionlevel = [ZippingWorker_Service.CompressionLevelEnumType]::ultra
$zipInfo.validatezipping = $true

# Add drive letter mapping (optional)
$driveLetter = New-Object ZippingWorker_Service.DriveLetterType
$driveLetter.DriveLetter = "C:"
$driveLetter.DrivePath = "E:\TestData"
$zipInfo.driveletters = $driveLetter

# Create file entries
$file1 = New-Object ZippingWorker_Service.FileInfoType
$file1.filelocation = "C:\Windows\System32\drivers\etc\hosts"
$file1.internalziplocation = "config/hosts.txt"
$file1.filehash = ""

$file2 = New-Object ZippingWorker_Service.FileInfoType
$file2.filelocation = "C:\Windows\System32\drivers\etc\networks"
$file2.internalziplocation = "config/networks.txt"
$file2.filehash = ""

$zipInfo.zipfiles = @($file1, $file2)

# Serialize to XML byte array
$serializer = New-Object System.Xml.Serialization.XmlSerializer([ZippingWorker_Service.ZipInfoType])
$ms = New-Object System.IO.MemoryStream
$serializer.Serialize($ms, $zipInfo)
$bytes = $ms.ToArray()
$ms.Close()

Write-Host "Serialized object size: $($bytes.Length) bytes" -ForegroundColor Cyan

# Send to API
try {
    $response = Invoke-RestMethod `
        -Uri "$baseUrl/api/zipinfo/binary" `
        -Method Post `
        -Body $bytes `
        -ContentType "application/octet-stream"

    Write-Host "Success!" -ForegroundColor Green
    Write-Host "Response: $($response | ConvertTo-Json)" -ForegroundColor Yellow
}
catch {
    Write-Host "Error: $_" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
}
