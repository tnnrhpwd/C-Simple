# Variables
$APP_NAME = "Simple"
$PROJECT_PATH = "C:\Users\Aries\Documents\GitHub\C-Simple\src\CSimple\CSimple.csproj"  # Path to your .NET MAUI project file
$OUTPUT_DIR = "C:\Users\Aries\Documents\GitHub\C-Simple\published"  # Publish output folder
$TARGET_FRAMEWORK = "net8.0-windows10.0.19041.0"  # Target framework
$BUILD_CONFIG = "Release"  # Adjust as necessary
$MSI_OUTPUT_DIR = "D:\My Drive\Simple"  # MSI output folder
$newCertPassword = "CSimpleNew"  # Password for the new .pfx file
$MSIX_OUTPUT_PATH = "C:\Users\Aries\Documents\GitHub\C-Simple\Simple.msix"  # Path to save the MSIX package
$MSIX_MANIFEST_PATH = "C:\Users\Aries\Documents\GitHub\C-Simple\AppxManifest.xml"  # Path to the AppxManifest.xml file
$cerPath = "C:\Users\Aries\Documents\CSimple\Certificates\SimpleCert.cer"
$pfxPath = "C:\Users\Aries\Documents\CSimple\Certificates\CSimple_NewCert.pfx"
$env:PATH += ";C:\Program Files\dotnet"
$subject = "CN=CSimple, O=Simple Org, C=US"
$mappingFilePath = "C:\Users\Aries\Documents\GitHub\C-Simple\mapping.txt"

# Function to check if a certificate with the same CN and O exists
function Test-CertificateExists {
  param (
      [string]$certPath,
      [string]$subject,
      [string]$password
  )
  if (Test-Path $certPath) {
      $cert = Get-PfxCertificate -FilePath $certPath -Password (ConvertTo-SecureString -String $password -Force -AsPlainText)
      return $cert.Subject -eq $subject
  }
  return $false
}

# Check for dotnet
Write-Host "Checking for dotnet executable..."
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host "dotnet not found in the PATH."
    exit 1
}
Write-Host "dotnet found."

# Create output directories
if (-not (Test-Path $OUTPUT_DIR)) { New-Item -ItemType Directory -Path $OUTPUT_DIR }
if (-not (Test-Path $MSI_OUTPUT_DIR)) { New-Item -ItemType Directory -Path $MSI_OUTPUT_DIR }
if (-not (Test-Path "C:\Users\Aries\Documents\CSimple")) { New-Item -ItemType Directory -Path "C:\Users\Aries\Documents\CSimple" }

# Build .NET MAUI project
Write-Host "Building .NET MAUI app..."
dotnet build $PROJECT_PATH --framework $TARGET_FRAMEWORK -c $BUILD_CONFIG
if ($LASTEXITCODE -ne 0) { Write-Host "Build failed." ; exit 1 }

# Publish .NET MAUI App
Write-Host "Publishing app..."
dotnet publish $PROJECT_PATH -f $TARGET_FRAMEWORK -c $BUILD_CONFIG -o $OUTPUT_DIR --self-contained
if ($LASTEXITCODE -ne 0) { Write-Host "Publish failed." ; exit 1 }

# Check if the certificate already exists
Write-Host "Checking if certificate with the same CN and O already exists..."
if (Test-CertificateExists -certPath $cerPath -subject $subject -password $newCertPassword -or (Test-CertificateExists -certPath $pfxPath -subject $subject -password $newCertPassword)) {
  Write-Host "Certificate with the same CN and O already exists. Skipping creation and export."
  exit 0
}

# Create a new self-signed certificate with a unique name
Write-Host "Creating new self-signed certificate..."
$newCert = New-SelfSignedCertificate -Type Custom -Subject $subject `
    -KeyUsage DigitalSignature -FriendlyName "CSimpleInstallerNewCert" `
    -CertStoreLocation "Cert:\CurrentUser\My" `
    -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}") `
    -NotAfter (Get-Date).AddYears(5)

# Verify certificate creation
if (-not $newCert) {
    Write-Host "New certificate creation failed."
    exit 1
}

# Ensure the directory for the new certificate path exists
$newCertDir = [System.IO.Path]::GetDirectoryName($pfxPath)
if (-not (Test-Path $newCertDir)) { New-Item -ItemType Directory -Path $newCertDir }

# Export the certificate to .cer file
Write-Host "Exporting new certificate to $cerPath..."
Export-Certificate -Cert $newCert -FilePath $cerPath

# Export new certificate to .pfx file
Write-Host "Exporting new certificate to $pfxPath..."
Export-PfxCertificate -Cert $newCert -FilePath $pfxPath -Password (ConvertTo-SecureString -String $newCertPassword -Force -AsPlainText)

# Sign the published output using the new .pfx file
Write-Host "Signing the published output with the new certificate..."
$filesToSign = Get-ChildItem -Path $OUTPUT_DIR -Recurse | Where-Object { $_.Extension -eq ".exe" -or $_.Extension -eq ".dll" }
foreach ($file in $filesToSign) {
    & "C:\Program Files (x86)\Windows Kits\10\bin\10.0.19041.0\x64\signtool.exe" sign /f "$pfxPath" /p "$newCertPassword" /fd SHA256 /t http://timestamp.digicert.com "$($file.FullName)"
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Signing $($file.FullName) failed. Please check errors above."
        exit 1
    }
}

Write-Host "All files signed successfully with the new certificate."

Write-Host "Creating mapping file..."

$mappingContent = @"
[Files]
"AppxManifest.xml" "AppxManifest.xml"
"@

# Ensure $OUTPUT_DIR is set
if (-not $OUTPUT_DIR) {
    Write-Host "Error: OUTPUT_DIR is not defined." -ForegroundColor Red
    exit 1
}

# Process each file and directory in $OUTPUT_DIR
Get-ChildItem -Path $OUTPUT_DIR -Recurse | ForEach-Object {
    $relativePath = $_.FullName.Substring($OUTPUT_DIR.Length + 1).Replace("\", "/")
    $mappingContent += "`"$($_.FullName.Replace("\", "/"))`" `"$relativePath`"`r`n"
}

# Output the mapping file content to a file
$mappingFile = Join-Path -Path $OUTPUT_DIR -ChildPath "mapping.txt"
Set-Content -Path $mappingFile -Value $mappingContent -Encoding UTF8

Write-Host "Mapping file created successfully at $mappingFile" -ForegroundColor Green

# Create MSIX package
Write-Host "Creating MSIX package..."
$makeAppxPath = "C:\Program Files (x86)\Windows Kits\10\bin\10.0.19041.0\x64\makeappx.exe"
& $makeAppxPath pack /m $MSIX_MANIFEST_PATH /f $mappingFilePath /p $MSIX_OUTPUT_PATH
if ($LASTEXITCODE -ne 0) {
    Write-Host "MSIX package creation failed."
    exit 1
}

Write-Host "Process complete! The MSIX package is saved at $MSIX_OUTPUT_PATH."