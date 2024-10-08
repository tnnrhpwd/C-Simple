# Variables 
$APP_NAME = "Simple"
$PROJECT_PATH = "C:\Users\Aries\Documents\GitHub\C-Simple\src\CSimple\CSimple.csproj"  # Path to your .NET MAUI solution file
$OUTPUT_DIR = "C:\Users\Aries\Documents\GitHub\C-Simple\published"  # Publish output folder
$TARGET_FRAMEWORK = "net8.0-windows10.0.19041.0"  # Target framework
$BUILD_CONFIG = "Release"  # Adjust as necessary
$MSI_OUTPUT_DIR = "D:\My Drive\Simple"  # MSI output folder
$INSTALLER_PROJECT = "C:\Users\Aries\Documents\GitHub\C-Simple\src\InstallerProject\InstallerProject.vdproj"  # Path to the Installer Project

# Ensure the .NET SDK is in the PATH
$env:PATH += ";C:\Program Files\dotnet"

# Check if dotnet exists
Write-Host "Checking for dotnet executable..."
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host "dotnet could not be found in the PATH."
    exit 1
} else {
    Write-Host "dotnet found in PATH."
}

# Create output directories if they do not exist
if (-not (Test-Path $OUTPUT_DIR)) {
    New-Item -ItemType Directory -Path $OUTPUT_DIR
}
if (-not (Test-Path $MSI_OUTPUT_DIR)) {
    New-Item -ItemType Directory -Path $MSI_OUTPUT_DIR
}

# Build the project targeting Windows framework
Write-Host "Building .NET MAUI app for Windows..."
dotnet build $PROJECT_PATH --framework $TARGET_FRAMEWORK -c $BUILD_CONFIG

# Verify if the build was successful
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed. Check errors above."
    exit 1
}

# Publish the .NET MAUI App
Write-Host "Publishing .NET MAUI app..."
dotnet publish $PROJECT_PATH -f $TARGET_FRAMEWORK -c $BUILD_CONFIG -o $OUTPUT_DIR

# Verify if the publish was successful
if ($LASTEXITCODE -ne 0) {
    Write-Host "Publish failed. Check errors above."
    exit 1
}

# Build the MSI using MSBuild (Installer Project)
Write-Host "Building MSI installer with MSBuild..."
& "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" `
    $PROJECT_PATH /t:Build /p:Configuration=$BUILD_CONFIG /p:Platform="x64"

# Verify if MSI creation was successful
if ($LASTEXITCODE -ne 0) {
    Write-Host "MSI creation failed. Check errors above."
    exit 1
}

Write-Host "MSI installer created successfully: $MSI_OUTPUT_DIR\$APP_NAME.msi"

# Sign the MSI installer
$msiFilePath = "$MSI_OUTPUT_DIR\$APP_NAME.msi"  # Path to your generated MSI file
$certPath = "D:\My Drive\CSimple.pfx"  # Path to your exported .pfx file
$certPassword = "CSimple"  # Password for the certificate

Write-Host "Signing the MSI installer..."
& "C:\Program Files (x86)\Windows Kits\10\bin\10.0.19041.0\x64\signtool.exe" sign /f $certPath /p $certPassword $msiFilePath

# Verify if signing was successful
if ($LASTEXITCODE -ne 0) {
    Write-Host "MSI signing failed. Check errors above."
    exit 1
}

Write-Host "MSI installer signed successfully."

# Cleanup published output folder
Remove-Item -Recurse -Force $OUTPUT_DIR

Write-Host "Process complete!"
