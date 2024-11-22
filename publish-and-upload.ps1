# Variables
$APP_NAME = "Simple"
$PROJECT_PATH = "C:\Users\Aries\Documents\GitHub\C-Simple\src\CSimple\CSimple.csproj"  # Path to your .NET MAUI solution file
$OUTPUT_DIR = "C:\Users\Aries\Documents\GitHub\C-Simple\published"  # Publish output folder
$TARGET_FRAMEWORK = "net8.0-windows10.0.19041.0"  # Target framework
$BUILD_CONFIG = "Release"  # Adjust as necessary
$MSI_OUTPUT_DIR = "D:\My Drive\Simple"  # MSI output folder
$INSTALLER_PROJECT = "C:\Users\Aries\Documents\GitHub\C-Simple\src\InstallerProject\InstallerProject.vdproj"  # Path to the Installer Project
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
dotnet publish $PROJECT_PATH -f $TARGET_FRAMEWORK -c $BUILD_CONFIG -o $OUTPUT_DIR
if ($LASTEXITCODE -ne 0) { Write-Host "Publish failed." ; exit 1 }

# Build the MSI Installer
Write-Host "Building MSI installer..."
& "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" $PROJECT_PATH /t:Build /p:Configuration=$BUILD_CONFIG /p:Platform="x64"
if ($LASTEXITCODE -ne 0) { Write-Host "MSI creation failed." ; exit 1 }

Write-Host "MSI created successfully at $MSI_OUTPUT_DIR\$APP_NAME.msi"

# Create a new self-signed certificate with a unique name
Write-Host "Creating new self-signed certificate..."
$newCert = New-SelfSignedCertificate -DnsName "CSimpleInstallerNewCert" `
    -CertStoreLocation "Cert:\CurrentUser\My" `
    -KeyExportPolicy Exportable -KeySpec Signature `
    -Subject "CN=CSimpleInstallerNewCert" -NotAfter (Get-Date).AddYears(5)

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

# Sign the MSI installer using the new .pfx file
$msiFilePath = "$MSI_OUTPUT_DIR\$APP_NAME.msi"
Write-Host "Signing the MSI installer with the new certificate..."
& "C:\Program Files (x86)\Windows Kits\10\bin\10.0.19041.0\x64\signtool.exe" sign /f $newCertPath /p $newCertPassword /fd SHA256 /t http://timestamp.digicert.com $msiFilePath

# Verify if signing was successful
if ($LASTEXITCODE -ne 0) {
    Write-Host "MSI signing with the new certificate failed. Please check errors above."
    exit 1
}

Write-Host "MSI signed successfully with the new certificate."

# Cleanup published output
Remove-Item -Recurse -Force $OUTPUT_DIR
Write-Host "Process complete!"