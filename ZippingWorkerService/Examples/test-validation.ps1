# Advanced test script showing validation with pre-calculated hashes
# This demonstrates the validation feature

$baseUrl = "http://localhost:5000"

# Helper function to calculate SHA256 hash
function Get-FileHashSHA256 {
    param([string]$FilePath)

    if (Test-Path $FilePath) {
        $hash = Get-FileHash -Path $FilePath -Algorithm SHA256
        return $hash.Hash.ToLower()
    }
    return $null
}

# Example 1: Validation WITHOUT pre-calculated hashes (service will calculate them)
Write-Host "`n=== Example 1: Validation with auto-calculated hashes ===" -ForegroundColor Cyan

$xml1 = @"
<?xml version="1.0" encoding="utf-8"?>
<zippingfiles xmlns="http://tempuri.org/ZipInfoSchema.xsd">
  <zipfilename>validated-auto.zip</zipfilename>
  <zipfilelocation>C:\temp\output</zipfilelocation>
  <zipcompressionlevel>ultra</zipcompressionlevel>
  <validatezipping>true</validatezipping>
  <zipfiles>
    <zipinfo>
      <filelocation>C:\Windows\System32\drivers\etc\hosts</filelocation>
      <filehash></filehash>
      <internalziplocation>hosts.txt</internalziplocation>
    </zipinfo>
  </zipfiles>
</zippingfiles>
"@

try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/zipinfo/xml" -Method Post -Body $xml1 -ContentType "application/xml"
    Write-Host "SUCCESS: Request queued - Service will calculate hashes and validate" -ForegroundColor Green
    Write-Host ($response | ConvertTo-Json) -ForegroundColor Yellow
}
catch {
    Write-Host "ERROR: $_" -ForegroundColor Red
}

# Example 2: Validation WITH pre-calculated hashes
Write-Host "`n=== Example 2: Validation with pre-calculated hashes ===" -ForegroundColor Cyan

$file1 = "C:\Windows\System32\drivers\etc\hosts"
$file2 = "C:\Windows\System32\drivers\etc\networks"

if (Test-Path $file1) {
    $hash1 = Get-FileHashSHA256 $file1
    Write-Host "Pre-calculated hash for hosts: $hash1" -ForegroundColor Gray
} else {
    $hash1 = ""
}

if (Test-Path $file2) {
    $hash2 = Get-FileHashSHA256 $file2
    Write-Host "Pre-calculated hash for networks: $hash2" -ForegroundColor Gray
} else {
    $hash2 = ""
}

$xml2 = @"
<?xml version="1.0" encoding="utf-8"?>
<zippingfiles xmlns="http://tempuri.org/ZipInfoSchema.xsd">
  <zipfilename>validated-precalc.zip</zipfilename>
  <zipfilelocation>C:\temp\output</zipfilelocation>
  <zipcompressionlevel>ultra</zipcompressionlevel>
  <validatezipping>true</validatezipping>
  <zipfiles>
    <zipinfo>
      <filelocation>$file1</filelocation>
      <filehash>$hash1</filehash>
      <internalziplocation>config/hosts.txt</internalziplocation>
    </zipinfo>
    <zipinfo>
      <filelocation>$file2</filelocation>
      <filehash>$hash2</filehash>
      <internalziplocation>config/networks.txt</internalziplocation>
    </zipinfo>
  </zipfiles>
</zippingfiles>
"@

try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/zipinfo/xml" -Method Post -Body $xml2 -ContentType "application/xml"
    Write-Host "SUCCESS: Request queued with pre-calculated hashes" -ForegroundColor Green
    Write-Host ($response | ConvertTo-Json) -ForegroundColor Yellow
}
catch {
    Write-Host "ERROR: $_" -ForegroundColor Red
}

# Example 3: No validation
Write-Host "`n=== Example 3: No validation (faster) ===" -ForegroundColor Cyan

$xml3 = @"
<?xml version="1.0" encoding="utf-8"?>
<zippingfiles xmlns="http://tempuri.org/ZipInfoSchema.xsd">
  <zipfilename>no-validation.zip</zipfilename>
  <zipfilelocation>C:\temp\output</zipfilelocation>
  <zipcompressionlevel>fastest</zipcompressionlevel>
  <validatezipping>false</validatezipping>
  <zipfiles>
    <zipinfo>
      <filelocation>C:\Windows\System32\drivers\etc\hosts</filelocation>
      <filehash></filehash>
      <internalziplocation>hosts.txt</internalziplocation>
    </zipinfo>
  </zipfiles>
</zippingfiles>
"@

try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/zipinfo/xml" -Method Post -Body $xml3 -ContentType "application/xml"
    Write-Host "SUCCESS: Request queued without validation" -ForegroundColor Green
    Write-Host ($response | ConvertTo-Json) -ForegroundColor Yellow
}
catch {
    Write-Host "ERROR: $_" -ForegroundColor Red
}

Write-Host "`n=== Check the service logs to see validation results ===" -ForegroundColor Magenta
