# Test script for ZipInfo API - XML endpoint
# This demonstrates how to send XML directly

$baseUrl = "http://localhost:5000"

# Create XML for ZipInfoType with new attribute-based structure
$xml = @"
<zippingfiles xmlns="http://tempuri.org/ZipInfoSchema.xsd">
  <zipfilename>test-from-xml.zip</zipfilename>
  <zipfilelocation>C:\temp\output</zipfilelocation>
  <zipcompressionlevel>ultra</zipcompressionlevel>
  <validatezipping>true</validatezipping>
  <driveletters driveletter="C:" drivepath="E:\TestData" />
  <zipfiles>
    <fileinfo filelocation="C:\Windows\System32\drivers\etc\hosts" filehash="" internalziplocation="config/hosts.txt" />
    <fileinfo filelocation="C:\Windows\System32\drivers\etc\networks" filehash="" internalziplocation="config/networks.txt" />
  </zipfiles>
</zippingfiles>
"@

Write-Host "Sending XML zip request..." -ForegroundColor Cyan

try {
    $response = Invoke-RestMethod `
        -Uri "$baseUrl/api/zipinfo/xml" `
        -Method Post `
        -Body $xml `
        -ContentType "application/xml"

    Write-Host "Success!" -ForegroundColor Green
    Write-Host "Response: $($response | ConvertTo-Json)" -ForegroundColor Yellow
}
catch {
    Write-Host "Error: $_" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
}
