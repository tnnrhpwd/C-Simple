# Variables
$APP_NAME = "Simple"
$PROJECT_PATH = "C:\Users\Aries\Documents\GitHub\C-Simple\src\CSimple\CSimple.csproj"  # Path to your .NET MAUI solution file
$OUTPUT_DIR = "C:\Users\Aries\Documents\GitHub\C-Simple\published"  # Publish output folder
$TARGET_FRAMEWORK = "net8.0-windows10.0.19041.0"  # Target framework
$BUILD_CONFIG = "Release"  # Adjust as necessary
$MSI_OUTPUT_DIR = "D:\My Drive\Simple"  # MSI output folder
$INSTALLER_PROJECT = "C:\Users\Aries\Documents\GitHub\C-Simple\src\InstallerProject\InstallerProject.vdproj"  # Path to the Installer Project
$certPath = "D:\My Drive\Simple\CSimple.pfx"  # Path to save the certificate
$certPassword = "CSimple"  # Password for the .pfx file

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

# Create a self-signed certificate
Write-Host "Creating self-signed certificate..."
$cert = New-SelfSignedCertificate -DnsName "CSimpleInstaller" `
    -CertStoreLocation "Cert:\CurrentUser\My" `
    -KeyExportPolicy Exportable -KeySpec Signature `
    -Subject "CN=CSimpleInstaller" -NotAfter (Get-Date).AddYears(5)

# Export certificate to .pfx file
Write-Host "Exporting certificate to $certPath..."
Export-PfxCertificate -Cert $cert -FilePath $certPath -Password (ConvertTo-SecureString -String $certPassword -Force -AsPlainText)

# Sign the MSI installer using the generated certificate
$msiFilePath = "$MSI_OUTPUT_DIR\$APP_NAME.msi"
Write-Host "Signing the MSI installer..."
& "C:\Program Files (x86)\Windows Kits\10\bin\10.0.19041.0\x64\signtool.exe" sign /f $certPath /p $certPassword $msiFilePath

# Verify if signing was successful
if ($LASTEXITCODE -ne 0) {
    Write-Host "MSI signing failed. Please check errors above."
    exit 1
}

Write-Host "MSI signed successfully."

# Cleanup published output
Remove-Item -Recurse -Force $OUTPUT_DIR
Write-Host "Process complete!"
