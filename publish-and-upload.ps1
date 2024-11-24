# Variables
$APP_NAME = "Simple"
$PROJECT_PATH = "C:\Users\Aries\Documents\GitHub\C-Simple\src\CSimple\CSimple.csproj"  # Path to your .NET MAUI project file
$OUTPUT_DIR = "C:\Users\Aries\Documents\GitHub\C-Simple\published"  # Publish output folder
$TARGET_FRAMEWORK = "net8.0-windows10.0.19041.0"  # Target framework
$BUILD_CONFIG = "Release"  # Adjust as necessary
$MSI_OUTPUT_DIR = "D:\My Drive\Simple"  # MSI output folder
$newCertPath = "C:\Users\Aries\Documents\CSimple\Certificates\CSimple_NewCert.pfx"  # Path to save the new certificate
$newCertPassword = "CSimpleNew"  # Password for the new .pfx file

# Ensure the .NET SDK is in the PATH
$env:PATH += ";C:\Program Files\dotnet"

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

# Create a new self-signed certificate with a unique name
Write-Host "Creating new self-signed certificate..."
$newCert = New-SelfSignedCertificate -Type Custom -Subject "CN=Contoso Software, O=Contoso Corporation, C=US" `
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
$newCertDir = [System.IO.Path]::GetDirectoryName($newCertPath)
if (-not (Test-Path $newCertDir)) { New-Item -ItemType Directory -Path $newCertDir }

# Export new certificate to .pfx file
Write-Host "Exporting new certificate to $newCertPath..."
Export-PfxCertificate -Cert $newCert -FilePath $newCertPath -Password (ConvertTo-SecureString -String $newCertPassword -Force -AsPlainText)

# Verify certificate export
if (-not (Test-Path $newCertPath)) {
    Write-Host "New certificate export failed."
    exit 1
}

# Sign the published output using the new .pfx file
Write-Host "Signing the published output with the new certificate..."
$filesToSign = Get-ChildItem -Path $OUTPUT_DIR -Recurse | Where-Object { $_.Extension -eq ".exe" -or $_.Extension -eq ".dll" }
foreach ($file in $filesToSign) {
    & "C:\Program Files (x86)\Windows Kits\10\bin\10.0.19041.0\x64\signtool.exe" sign /f $newCertPath /p $newCertPassword /fd SHA256 /t http://timestamp.digicert.com "$($file.FullName)"
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Signing $($file.FullName) failed. Please check errors above."
        exit 1
    }
}

Write-Host "All files signed successfully with the new certificate."

# Bundle the published output into a zip file for easy upload/download
$zipFilePath = "C:\Users\Aries\Documents\GitHub\C-Simple\published.zip"
Write-Host "Creating a zip file of the published output..."
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($OUTPUT_DIR, $zipFilePath)

Write-Host "Process complete! The bundled project is saved at $zipFilePath."