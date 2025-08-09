# CSimple Publish and Upload Script
# 
# This script builds, packages, signs, and distributes CSimple for Windows
# 
# PROVEN WORKING METHOD (Cemented 2025-08-09):
# 1. Updates project version files (Package.appxmanifest and .csproj) with build-info.json version
# 2. Uses dotnet publish to build and generate MSIX package automatically 
# 3. Locates the generated test MSIX package from AppPackages directory
# 4. Signs the MSIX package using the certificate (CSimple_NewCert.pfx)
# 5. Copies signed package to distribution directory with proper documentation
# 
# This method ensures:
# - Version consistency between build-info.json and actual package
# - Proper package identity (CSimple-App) and publisher (Simple Inc)
# - Correct digital signing for Windows trust
# - Start menu accessibility after installation
# 
# Version: 2.0 (Updated to cement working methodology)

# Variables
$APP_NAME = "CSimple"
$ScriptBaseDir = $PSScriptRoot # Get the directory where the script is located
$PROJECT_PATH = Join-Path $ScriptBaseDir "..\CSimple.csproj"  # Path relative to script location
$OUTPUT_DIR = (Resolve-Path (Join-Path $ScriptBaseDir "..\..\..\published")).Path  # Publish output folder in base directory
$TARGET_FRAMEWORK = "net8.0-windows10.0.19041.0"  # Target framework
$BUILD_CONFIG = "Release"  # Adjust as necessary
$MSI_OUTPUT_DIR = Join-Path $ScriptBaseDir "..\..\..\msi_output" # MSI output folder in base directory
$newCertPassword = "CSimpleNew"  # Password for the new .pfx file
$CertDir = Join-Path $ScriptBaseDir "..\..\..\certs" # Certificate directory in base directory
$env:PATH += ";C:\Program Files\dotnet"
$subject = "CN=Simple Inc, O=Simple Inc, C=US"
$mappingFilePath = Join-Path $ScriptBaseDir "..\..\..\mapping.txt" # Mapping file in base directory

# Build info file (consolidates version and revision tracking)
$buildInfoPath = Join-Path $ScriptBaseDir "..\..\..\build-info.json" # Build info file in base directory

# Root destination directory 
$rootDestDir = "D:\My Drive\Simple\beta_versions"
# Version-specific paths will be set after determining the version

# Maximum number of versions to keep in the main directory before archiving
$maxVersionsToKeep = 3

# Release metadata file
$releaseMetadataPath = Join-Path $rootDestDir "releases.json"

# Import Certificate Service
. (Join-Path $PSScriptRoot "CertificateService.ps1")

# Import Documentation Service
. (Join-Path $PSScriptRoot "DocumentationService.ps1")

# Function to check if tool exists
function Test-ToolExists {
    param (
        [string]$toolName,
        [string]$toolPath = ""
    )
    
    if ($toolPath -and (Test-Path $toolPath)) {
        return $true
    }
    
    return $null -ne (Get-Command $toolName -ErrorAction SilentlyContinue)
}

# Function to update project files with current version
function Update-ProjectVersion {
    param (
        [string]$projectPath,
        [string]$manifestPath,
        [string]$version
    )
    
    Write-Host "Updating project version to $version..." -ForegroundColor Yellow
    
    # Parse version parts
    $versionParts = $version -split '\.'
    $majorMinor = "$($versionParts[0]).$($versionParts[1])"
    $buildNumber = $versionParts[2]
    
    # Update Package.appxmanifest - be more specific with the replacement
    if (Test-Path $manifestPath) {
        $manifestContent = Get-Content $manifestPath -Raw
        # Only replace the Version attribute in the Identity element
        $manifestContent = $manifestContent -replace '<Identity([^>]*)\s+Version="[^"]*"', "<Identity`$1 Version=`"$version`""
        Set-Content -Path $manifestPath -Value $manifestContent -Encoding UTF8
        Write-Host "Updated Package.appxmanifest version to $version" -ForegroundColor Green
    }
    
    # Update .csproj file
    if (Test-Path $projectPath) {
        $projectContent = Get-Content $projectPath -Raw
        $projectContent = $projectContent -replace '<ApplicationDisplayVersion>[^<]*</ApplicationDisplayVersion>', "<ApplicationDisplayVersion>$majorMinor.$buildNumber</ApplicationDisplayVersion>"
        $projectContent = $projectContent -replace '<ApplicationVersion>[^<]*</ApplicationVersion>', "<ApplicationVersion>$buildNumber</ApplicationVersion>"
        Set-Content -Path $projectPath -Value $projectContent -Encoding UTF8
        Write-Host "Updated .csproj ApplicationDisplayVersion to $majorMinor.$buildNumber and ApplicationVersion to $buildNumber" -ForegroundColor Green
    }
}

# Function to get and increment build info
function Get-IncrementedBuildInfo {
    # Load existing build info or create default
    if (Test-Path $buildInfoPath) {
        try {
            $buildInfo = Get-Content $buildInfoPath -Raw | ConvertFrom-Json
            $currentRevision = $buildInfo.revision
            $currentVersion = $buildInfo.version
        }
        catch {
            Write-Host "Invalid build info format. Resetting to defaults." -ForegroundColor Yellow
            $currentRevision = 0
            $currentVersion = "1.0.0.0"
        }
    }
    else {
        Write-Host "Build info file not found. Starting fresh." -ForegroundColor Yellow
        $currentRevision = 0
        $currentVersion = "1.0.0.0"
    }
    
    # Increment revision
    $newRevision = $currentRevision + 1
    
    # Generate new version (using revision as build number)
    $versionParts = $currentVersion -split '\.'
    $newVersion = "$($versionParts[0]).$($versionParts[1]).$newRevision.0"
    
    # Create updated build info
    $newBuildInfo = @{
        "version"    = $newVersion
        "revision"   = $newRevision
        "lastBuild"  = (Get-Date).ToString("o")
        "buildCount" = $newRevision
    }
    
    # Save updated build info
    $newBuildInfo | ConvertTo-Json -Depth 5 | Set-Content -Path $buildInfoPath -Encoding UTF8
    
    return $newBuildInfo
}

# Function to get release date in standardized format
function Get-FormattedReleaseDate {
    return Get-Date -Format "yyyy.MM.dd"
}

# Function to get all version directories
function Get-VersionDirectories {
    param (
        [string]$rootPath
    )
    
    if (Test-Path $rootPath) {
        return Get-ChildItem -Path $rootPath -Directory | Where-Object { $_.Name -match "^v\d+\.\d+\.\d+\.\d+.*$" } | Sort-Object CreationTime -Descending
    }
    return @()
}

# Function to clean up old versions
function Invoke-VersionArchiving {
    param (
        [string]$rootPath,
        [int]$keepCount
    )
    
    $versionDirs = Get-VersionDirectories -rootPath $rootPath
    
    # Skip cleanup if we don't have more than the keep count
    if ($versionDirs.Count -le $keepCount) {
        return
    }
    
    # Delete older versions (skip the first $keepCount which are the newest)
    $toDelete = $versionDirs | Select-Object -Skip $keepCount
    
    foreach ($dir in $toDelete) {
        Write-Host "Deleting older version: $($dir.Name)" -ForegroundColor Yellow
        
        try {
            # Remove the directory and all its contents
            Remove-Item -Path $dir.FullName -Recurse -Force
            Write-Host "Successfully deleted: $($dir.Name)" -ForegroundColor Green
        }
        catch {
            Write-Host "Failed to delete $($dir.Name): $_" -ForegroundColor Red
        }
    }
}

# Function to update release metadata
function Update-ReleaseMetadata {
    param (
        [string]$metadataPath,
        [string]$version,
        [int]$revision,
        [string]$releaseDate,
        [string]$releaseNotes = ""
    )
    
    # Initialize metadata object
    $metadata = @{
        "releases" = @()
    }
    
    # Load existing metadata if present
    if (Test-Path $metadataPath) {
        try {
            $jsonContent = Get-Content $metadataPath -Raw | ConvertFrom-Json
            # Convert PSCustomObject to hashtable manually for compatibility
            $metadata = @{
                "releases" = @()
            }
            if ($jsonContent.releases) {
                $metadata.releases = $jsonContent.releases
            }
        }
        catch {
            Write-Host "Error reading metadata file, creating new one: $_" -ForegroundColor Yellow
        }
    }
    
    # Add new release info
    $releaseInfo = @{
        "version"     = $version
        "revision"    = $revision
        "releaseDate" = $releaseDate
        "timestamp"   = (Get-Date).ToString("o")
        "notes"       = $releaseNotes
    }
    
    # Ensure releases array exists
    if (-not $metadata.releases) {
        $metadata.releases = @()
    }
    
    # Add new release at the beginning (most recent first)
    $metadata.releases = @($releaseInfo) + $metadata.releases
    
    # Save metadata
    $metadata | ConvertTo-Json -Depth 10 | Set-Content -Path $metadataPath -Encoding UTF8
}

# Check for dotnet
Write-Host "Checking for dotnet executable..."
if (-not (Test-ToolExists -toolName "dotnet")) {
    Write-Host "dotnet not found in the PATH."
    exit 1
}
Write-Host "dotnet found."

# Check for signtool and makeappx
$signtoolPath = "C:\Program Files (x86)\Windows Kits\10\bin\10.0.19041.0\x64\signtool.exe"
$makeAppxPath = "C:\Program Files (x86)\Windows Kits\10\bin\10.0.19041.0\x64\makeappx.exe"

if (-not (Test-ToolExists -toolName "signtool" -toolPath $signtoolPath)) {
    Write-Host "signtool not found at $signtoolPath" -ForegroundColor Red
    exit 1
}

if (-not (Test-ToolExists -toolName "makeappx" -toolPath $makeAppxPath)) {
    Write-Host "makeappx not found at $makeAppxPath" -ForegroundColor Red
    exit 1
}

# Create output directories
if (-not (Test-Path $OUTPUT_DIR)) { New-Item -ItemType Directory -Path $OUTPUT_DIR }
if (-not (Test-Path $MSI_OUTPUT_DIR)) { New-Item -ItemType Directory -Path $MSI_OUTPUT_DIR }

# Create root destination directory if it doesn't exist
if (-not (Test-Path $rootDestDir)) {
    New-Item -ItemType Directory -Path $rootDestDir -Force | Out-Null
}

# Get build info and increment it
$buildInfo = Get-IncrementedBuildInfo
$currentRevision = $buildInfo.revision
$appVersion = $buildInfo.version

Write-Host "Building revision #$currentRevision (v$appVersion)" -ForegroundColor Green

# Update project files with the current version
$manifestPath = Join-Path $ScriptBaseDir "..\Platforms\Windows\Package.appxmanifest"
Update-ProjectVersion -projectPath $PROJECT_PATH -manifestPath $manifestPath -version $appVersion

# Get release date for folder naming
$releaseDate = Get-FormattedReleaseDate

# Setup version-specific paths and names
$versionDirName = "v$appVersion-$releaseDate"
$versionDir = Join-Path $rootDestDir $versionDirName

$msixFileName = "$APP_NAME-v$appVersion.msix"
$certFileName = "SimpleCert.cer"  # Use consistent certificate name
$MSIX_MANIFEST_PATH = Join-Path $ScriptBaseDir "..\..\..\AppxManifest.xml"
$MSIX_OUTPUT_PATH = Join-Path $ScriptBaseDir "..\..\..\$msixFileName"

# Create version directory
if (Test-Path $versionDir) {
    # Clear existing files if directory exists
    Remove-Item -Path "$versionDir\*" -Force -Recurse
}
else {
    New-Item -ItemType Directory -Path $versionDir -Force | Out-Null
}

# Build .NET MAUI project
Write-Host "Building .NET MAUI app..."
# Check if project file exists before building
if (-not (Test-Path $PROJECT_PATH)) {
    Write-Host "Project file not found at $PROJECT_PATH" -ForegroundColor Red
    exit 1
}
dotnet build $PROJECT_PATH --framework $TARGET_FRAMEWORK -c $BUILD_CONFIG
if ($LASTEXITCODE -ne 0) { Write-Host "Build failed." ; exit 1 }

# Publish .NET MAUI App and create MSIX package
Write-Host "Publishing app and creating MSIX package..."
# The project is configured to generate MSIX packages automatically during publish
dotnet publish $PROJECT_PATH -f $TARGET_FRAMEWORK -c $BUILD_CONFIG -o $OUTPUT_DIR --self-contained /p:AppxPackageSigningEnabled=false
if ($LASTEXITCODE -ne 0) { Write-Host "Publish failed." ; exit 1 }

# Find the generated MSIX package
$msixPackageDir = Get-ChildItem -Path (Split-Path $PROJECT_PATH) -Recurse -Directory | Where-Object { $_.Name -match "AppPackages" } | Select-Object -First 1
if (-not $msixPackageDir) {
    Write-Host "Could not find AppPackages directory" -ForegroundColor Red
    exit 1
}

# Find the test package directory that matches our current version
$testPackageDir = Get-ChildItem -Path $msixPackageDir.FullName -Directory | Where-Object { $_.Name -match "_Test$" -and $_.Name -like "*$appVersion*" } | Sort-Object CreationTime -Descending | Select-Object -First 1
if (-not $testPackageDir) {
    Write-Host "Could not find test package directory for version $appVersion" -ForegroundColor Red
    Write-Host "Available directories:" -ForegroundColor Yellow
    Get-ChildItem -Path $msixPackageDir.FullName -Directory | ForEach-Object { Write-Host "  $($_.Name)" -ForegroundColor Yellow }
    exit 1
}

$generatedMsix = Get-ChildItem -Path $testPackageDir.FullName -Filter "*.msix" | Select-Object -First 1
if (-not $generatedMsix) {
    Write-Host "Could not find generated MSIX package in $($testPackageDir.FullName)" -ForegroundColor Red
    exit 1
}

Write-Host "Found generated MSIX: $($generatedMsix.FullName)" -ForegroundColor Green

# Initialize and handle certificate management
$certResult = Initialize-AppCertificate -certDir $CertDir -subject $subject -password $newCertPassword

# Sign the generated MSIX package
Write-Host "Signing MSIX package..."
& $signtoolPath sign /fd SHA256 /a /f $certResult.PfxPath /p $newCertPassword $generatedMsix.FullName
if ($LASTEXITCODE -ne 0) {
    Write-Host "MSIX signing failed." -ForegroundColor Red
    exit 1
}

Write-Host "MSIX package signed successfully" -ForegroundColor Green

# Set the final MSIX path for copying to distribution directory
$MSIX_OUTPUT_PATH = $generatedMsix.FullName

# Copy files to the version-specific directory
Write-Host "Copying files to version directory: $versionDir"
try {
    # Copy MSIX and certificate to version directory
    Copy-Item -Path $MSIX_OUTPUT_PATH -Destination (Join-Path $versionDir $msixFileName) -Force
    Copy-Item -Path $certResult.CerPath -Destination (Join-Path $versionDir $certFileName) -Force
    
    # Create all installation documentation using the documentation service
    New-InstallationDocumentation -versionDir $versionDir -appVersion $appVersion -releaseDate $releaseDate -certFileName $certFileName -msixFileName $msixFileName -buildInfoPath $buildInfoPath
    
    # Update release metadata
    Update-ReleaseMetadata -metadataPath $releaseMetadataPath -version $appVersion -revision $currentRevision -releaseDate $releaseDate
    
    # Archive older versions
    Invoke-VersionArchiving -rootPath $rootDestDir -keepCount $maxVersionsToKeep

    Write-Host "Files successfully copied to version directory: $versionDir" -ForegroundColor Green
} 
catch {
    Write-Host "Failed to copy files to version directory. Error: $_" -ForegroundColor Red
}

# Clean up temporary files
Write-Host "Cleaning up temporary files..."
if (Test-Path $mappingFilePath) { Remove-Item $mappingFilePath -Force }
if (Test-Path $MSIX_MANIFEST_PATH) { Remove-Item $MSIX_MANIFEST_PATH -Force }
# Note: We keep the generated MSIX package for distribution

Write-Host "Process complete! Files have been published to:" -ForegroundColor Green
Write-Host "- Version directory: $versionDir" -ForegroundColor Green
Write-Host ""
Write-Host "MSIX package: $(Join-Path $versionDir $msixFileName)" -ForegroundColor Green
Write-Host "Certificate: $(Join-Path $versionDir $certFileName)" -ForegroundColor Green