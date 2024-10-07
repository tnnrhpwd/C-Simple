# Variables
$APP_NAME = "Simple"
$OUTPUT_DIR = "C:\Users\Aries\Documents\GitHub\C-Simple\published"  # Customize this for your output folder
$TARGET_FRAMEWORK = "net8.0-windows10.0.19041.0"  # Change this to your target framework (ios, android, windows, etc.)
$BUILD_CONFIG = "Release"  # Adjust if necessary
$GDRIVE_FOLDER_ID = "1lKQeLUHYwlrqO8P7LkMztHxSd_CpTvrx"  # Folder ID in Google Drive

# Ensure the .NET SDK is in the PATH
$env:PATH += ";C:\Program Files\dotnet"

# Check if dotnet exists
Write-Host "Checking for dotnet executable..."
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host "dotnet could not be found in the PATH."

    # Attempt to directly access dotnet
    if (-not (Test-Path "C:\Program Files\dotnet\dotnet.exe")) {
        Write-Host "dotnet could not be found. Please check the installation."
        exit 1
    } else {
        Write-Host "dotnet found at C:\Program Files\dotnet\dotnet.exe"
    }
} else {
    Write-Host "dotnet found in PATH."
}

# Check for required programs
if (-not (Get-Command gdrive -ErrorAction SilentlyContinue) -and -not (Get-Command rclone -ErrorAction SilentlyContinue)) {
    Write-Host "Neither gdrive nor rclone could be found. Please install one of them."
    exit 1
}

# Set the Windows SDK path
$WINDOWS_SDK_PATH = "C:\Program Files (x86)\Windows Kits\10\Include"

# Create output directory if it does not exist
if (-not (Test-Path $OUTPUT_DIR)) {
    New-Item -ItemType Directory -Path $OUTPUT_DIR
}

# Publish the .NET MAUI App
Write-Host "Publishing .NET MAUI app..."
dotnet publish -f $TARGET_FRAMEWORK -c $BUILD_CONFIG -o $OUTPUT_DIR /p:WindowsSdkDir="$WINDOWS_SDK_PATH"

# Verify if the publish was successful
if ($LASTEXITCODE -ne 0) {
    Write-Host "Publish failed. Check errors above."
    exit 1
}

# Get the path to the published output
$PUBLISH_FOLDER = Get-ChildItem -Path $OUTPUT_DIR -Directory | Where-Object { $_.Name -eq $TARGET_FRAMEWORK } | Select-Object -First 1

if (-not $PUBLISH_FOLDER) {
    Write-Host "Publish folder not found!"
    exit 1
}

Write-Host "Published app located at: $($PUBLISH_FOLDER.FullName)"

# Compress the published folder into a zip
$ZIP_FILE = "${APP_NAME}_$(Get-Date -Format 'yyyyMMdd_HHmmss').zip"
Write-Host "Compressing published output into $ZIP_FILE..."
Compress-Archive -Path "$($PUBLISH_FOLDER.FullName)\*" -DestinationPath $ZIP_FILE

# Verify if the zip was successful
if ($LASTEXITCODE -ne 0) {
    Write-Host "Compression failed."
    exit 1
}

Write-Host "Upload to Google Drive starting..."

# Upload to Google Drive using gdrive or rclone
if (Get-Command gdrive -ErrorAction SilentlyContinue) {
    & gdrive upload --parent $GDRIVE_FOLDER_ID $ZIP_FILE
} elseif (Get-Command rclone -ErrorAction SilentlyContinue) {
    & rclone copy $ZIP_FILE remote:path/to/GoogleDriveFolder
} else {
    Write-Host "No valid Google Drive uploader found."
    exit 1
}

# Verify if the upload was successful
if ($LASTEXITCODE -eq 0) {
    Write-Host "Upload successful!"
} else {
    Write-Host "Upload failed."
    exit 1
}

# Cleanup
Remove-Item -Recurse -Force $OUTPUT_DIR
Remove-Item -Force $ZIP_FILE

Write-Host "Process complete!"
