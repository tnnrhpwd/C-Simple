# CSimple Cross-Platform Publish and Upload Script
# 
# This script builds, packages, signs, and distributes CSimple for multiple platforms
# 
# Supported Platforms:
# - Windows: MSIX packages with code signing
# - Linux: Self-contained executables with .tar.gz packaging
# - Android: APK packages with optional signing
# - Future: iOS IPA
# 
# Version: 3.0 (Cross-platform support)

param(
    [string[]]$Platforms = @("windows"),  # Default to Windows only
    [switch]$AllPlatforms,
    [switch]$WindowsOnly,
    [switch]$LinuxOnly,
    [switch]$AndroidOnly,
    [string]$Architecture = "x64",  # Supported: x64, arm64
    [string]$AndroidKeyStore = "",
    [string]$AndroidKeyAlias = "csimple",
    [SecureString]$AndroidStorePassword = $null,
    [SecureString]$AndroidKeyPassword = $null
)

# Process platform selection
if ($AllPlatforms) {
    $Platforms = @("windows", "linux", "android")
    Write-Host "Selected: All platforms" -ForegroundColor Yellow
}
elseif ($WindowsOnly) {
    $Platforms = @("windows")
    Write-Host "Selected: Windows only" -ForegroundColor Yellow
}
elseif ($LinuxOnly) {
    $Platforms = @("linux")
    Write-Host "Selected: Linux only" -ForegroundColor Yellow
}
elseif ($AndroidOnly) {
    $Platforms = @("android")
    Write-Host "Selected: Android only" -ForegroundColor Yellow
}

Write-Host "Building for platforms: $($Platforms -join ', ')" -ForegroundColor Cyan

# Base Variables
$APP_NAME = "CSimple"
$ScriptBaseDir = $PSScriptRoot
$PROJECT_PATH = Join-Path $ScriptBaseDir "..\CSimple.csproj"
$BUILD_CONFIG = "Release"
$newCertPassword = "CSimpleNew"
$CertDir = Join-Path $ScriptBaseDir "..\..\..\certs"
$env:PATH += ";C:\Program Files\dotnet"
$subject = "CN=Simple Inc, O=Simple Inc, C=US"

# Build info file
$buildInfoPath = Join-Path $ScriptBaseDir "..\..\..\build-info.json"

# Root destination directory 
$rootDestDir = "D:\My Drive\Simple\beta_versions"
$maxVersionsToKeep = 3
$releaseMetadataPath = Join-Path $rootDestDir "releases.json"

# Platform-specific configurations
$platformConfigs = @{
    "windows" = @{
        Framework                = "net8.0-windows10.0.19041.0"
        RuntimeId                = "win-$Architecture"
        Extension                = "msix"
        RequiresSigning          = $true
        RequiresSpecialPackaging = $true
        OutputDir                = Join-Path $ScriptBaseDir "..\..\..\published\windows"
    }
    "linux"   = @{
        Framework                = "net8.0"
        RuntimeId                = "linux-$Architecture"
        Extension                = "tar.gz"
        RequiresSigning          = $false
        RequiresSpecialPackaging = $true
        OutputDir                = Join-Path $ScriptBaseDir "..\..\..\published\linux"
    }
    "android" = @{
        Framework                = "net8.0-android"
        RuntimeId                = $null
        Extension                = "apk"
        RequiresSigning          = $true
        RequiresSpecialPackaging = $true
        OutputDir                = Join-Path $ScriptBaseDir "..\..\..\published\android"
    }
}

# Import services
. (Join-Path $PSScriptRoot "CertificateService.ps1")
. (Join-Path $PSScriptRoot "DocumentationService.ps1")
. (Join-Path $PSScriptRoot "CrossPlatformPackaging.ps1")

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
    
    # Update Package.appxmanifest for Windows - be more specific with the replacement
    if ((Test-Path $manifestPath) -and ($Platforms -contains "windows")) {
        $manifestContent = Get-Content $manifestPath -Raw
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
    
    $newRevision = $currentRevision + 1
    $versionParts = $currentVersion -split '\.'
    $newVersion = "$($versionParts[0]).$($versionParts[1]).$newRevision.0"
    
    $newBuildInfo = @{
        "version"    = $newVersion
        "revision"   = $newRevision
        "lastBuild"  = (Get-Date).ToString("o")
        "buildCount" = $newRevision
        "platforms"  = $Platforms
    }
    
    $newBuildInfo | ConvertTo-Json -Depth 5 | Set-Content -Path $buildInfoPath -Encoding UTF8
    return $newBuildInfo
}

# Function to build for specific platform
function Invoke-PlatformBuild {
    param (
        [string]$platform,
        [hashtable]$config,
        [string]$version
    )
    
    Write-Host "`n=== Building for $platform ===" -ForegroundColor Cyan
    
    $outputDir = $config.OutputDir
    if (-not (Test-Path $outputDir)) { 
        New-Item -ItemType Directory -Path $outputDir -Force | Out-Null 
    }
    
    # Clear previous build output
    if (Test-Path $outputDir) {
        Remove-Item -Path "$outputDir\*" -Recurse -Force -ErrorAction SilentlyContinue
    }
    
    try {
        Write-Host "Building $platform application..." -ForegroundColor Yellow
        
        # Build command varies by platform
        if ($platform -eq "windows") {
            # Windows: Build and let MAUI generate MSIX
            dotnet build $PROJECT_PATH --framework $config.Framework -c $BUILD_CONFIG
            if ($LASTEXITCODE -ne 0) { throw "Build failed for $platform" }
            
            dotnet publish $PROJECT_PATH -f $config.Framework -c $BUILD_CONFIG -o $outputDir --self-contained /p:AppxPackageSigningEnabled=false
            if ($LASTEXITCODE -ne 0) { throw "Publish failed for $platform" }
            
        }
        elseif ($platform -eq "linux") {
            # Linux: Create self-contained executable
            dotnet publish $PROJECT_PATH -f $config.Framework -c $BUILD_CONFIG -r $config.RuntimeId --self-contained true -o $outputDir /p:PublishSingleFile=true /p:PublishReadyToRun=true
            if ($LASTEXITCODE -ne 0) { throw "Publish failed for $platform" }
        }
        
        Write-Host "$platform build completed successfully!" -ForegroundColor Green
        return $true
        
    }
    catch {
        Write-Host "Build failed for $platform`: $_" -ForegroundColor Red
        return $false
    }
}

# Function to package for specific platform
function Invoke-PlatformPackaging {
    param (
        [string]$platform,
        [hashtable]$config,
        [string]$version,
        [string]$versionDir
    )
    
    Write-Host "`nPackaging for $platform..." -ForegroundColor Yellow
    
    try {
        if ($platform -eq "windows") {
            # Handle Windows MSIX packaging (existing logic)
            return Invoke-WindowsPackaging -config $config -version $version -versionDir $versionDir
            
        }
        elseif ($platform -eq "linux") {
            # Handle Linux .tar.gz packaging
            return Invoke-LinuxPackaging -config $config -version $version -versionDir $versionDir
        }
        elseif ($platform -eq "android") {
            # Handle Android APK packaging
            return Invoke-AndroidPackaging -config $config -version $version -versionDir $versionDir
        }
        
        return $false
        
    }
    catch {
        Write-Host "Packaging failed for $platform`: $_" -ForegroundColor Red
        return $false
    }
}

# Windows-specific packaging
function Invoke-WindowsPackaging {
    param (
        [hashtable]$config,
        [string]$version,
        [string]$versionDir
    )
    
    Write-Host "Processing Windows MSIX package..." -ForegroundColor Yellow
    
    # Find the generated MSIX package (existing logic)
    $msixPackageDir = Get-ChildItem -Path (Split-Path $PROJECT_PATH) -Recurse -Directory | Where-Object { $_.Name -match "AppPackages" } | Select-Object -First 1
    if (-not $msixPackageDir) {
        throw "Could not find AppPackages directory"
    }
    
    $testPackageDir = Get-ChildItem -Path $msixPackageDir.FullName -Directory | Where-Object { $_.Name -match "_Test$" -and $_.Name -like "*$version*" } | Sort-Object CreationTime -Descending | Select-Object -First 1
    if (-not $testPackageDir) {
        throw "Could not find test package directory for version $version"
    }
    
    $generatedMsix = Get-ChildItem -Path $testPackageDir.FullName -Filter "*.msix" | Select-Object -First 1
    if (-not $generatedMsix) {
        throw "Could not find generated MSIX package"
    }
    
    Write-Host "Found generated MSIX: $($generatedMsix.FullName)" -ForegroundColor Green
    
    # Initialize certificate management
    $certResult = Initialize-AppCertificate -certDir $CertDir -subject $subject -password $newCertPassword
    
    # Sign the MSIX package
    $signtoolPath = "C:\Program Files (x86)\Windows Kits\10\bin\10.0.19041.0\x64\signtool.exe"
    Write-Host "Signing MSIX package..." -ForegroundColor Yellow
    & $signtoolPath sign /fd SHA256 /a /f $certResult.PfxPath /p $newCertPassword $generatedMsix.FullName
    if ($LASTEXITCODE -ne 0) {
        throw "MSIX signing failed"
    }
    
    # Create Windows platform directory
    $windowsPlatformDir = Join-Path $versionDir "windows"
    New-Item -ItemType Directory -Path $windowsPlatformDir -Force | Out-Null
    
    # Copy files
    $msixFileName = "$APP_NAME-v$version-windows.msix"
    $certFileName = "SimpleCert.cer"
    
    Copy-Item -Path $generatedMsix.FullName -Destination (Join-Path $windowsPlatformDir $msixFileName) -Force
    Copy-Item -Path $certResult.CerPath -Destination (Join-Path $windowsPlatformDir $certFileName) -Force
    
    # Create Windows-specific documentation
    New-WindowsInstallationDocumentation -platformDir $windowsPlatformDir -appVersion $version -certFileName $certFileName -msixFileName $msixFileName
    
    Write-Host "Windows packaging completed!" -ForegroundColor Green
    return $true
}

# Linux-specific packaging
function Invoke-LinuxPackaging {
    param (
        [hashtable]$config,
        [string]$version,
        [string]$versionDir
    )
    
    Write-Host "Creating Linux package..." -ForegroundColor Yellow
    
    $linuxOutputDir = $config.OutputDir
    $linuxPlatformDir = Join-Path $versionDir "linux"
    New-Item -ItemType Directory -Path $linuxPlatformDir -Force | Out-Null
    
    # Create application directory structure
    $appDir = Join-Path $linuxPlatformDir "csimple"
    New-Item -ItemType Directory -Path $appDir -Force | Out-Null
    
    # Copy all published files
    Copy-Item -Path "$linuxOutputDir\*" -Destination $appDir -Recurse -Force
    
    # Make the main executable file executable (if on Linux/WSL)
    $mainExecutable = Join-Path $appDir "CSimple"
    if (Test-Path $mainExecutable) {
        try {
            # Try to set execute permissions (will work on WSL/Linux)
            chmod +x $mainExecutable 2>$null
        }
        catch {
            # Ignore errors on Windows
        }
    }
    
    # Create launch script
    $launchScript = @"
#!/bin/bash
# CSimple Linux Launch Script
SCRIPT_DIR="`$(cd "`$(dirname "`${BASH_SOURCE[0]}")" && pwd)"
cd "`$SCRIPT_DIR"
./CSimple "`$@"
"@
    
    $launchScriptPath = Join-Path $linuxPlatformDir "csimple.sh"
    Set-Content -Path $launchScriptPath -Value $launchScript -Encoding UTF8
    
    # Create .tar.gz archive
    $tarFileName = "$APP_NAME-v$version-linux-$Architecture.tar.gz"
    $tarFilePath = Join-Path $linuxPlatformDir $tarFileName
    
    try {
        # Use PowerShell's built-in compression if tar is not available
        if (Get-Command tar -ErrorAction SilentlyContinue) {
            # Use tar if available (WSL, Git Bash, or native Windows tar)
            Push-Location $linuxPlatformDir
            tar -czf $tarFileName -C . csimple csimple.sh
            Pop-Location
        }
        else {
            # Fallback: Use .NET compression
            Add-Type -AssemblyName System.IO.Compression.FileSystem
            [System.IO.Compression.ZipFile]::CreateFromDirectory($appDir, ($tarFilePath -replace '\.tar\.gz$', '.zip'))
            Write-Host "Created .zip file instead of .tar.gz (tar not available)" -ForegroundColor Yellow
            $tarFileName = $tarFileName -replace '\.tar\.gz$', '.zip'
            $tarFilePath = $tarFilePath -replace '\.tar\.gz$', '.zip'
        }
    }
    catch {
        Write-Host "Warning: Could not create compressed archive: $_" -ForegroundColor Yellow
    }
    
    # Create Linux-specific documentation
    New-LinuxInstallationDocumentation -platformDir $linuxPlatformDir -appVersion $version -archiveFileName $tarFileName
    
    Write-Host "Linux packaging completed!" -ForegroundColor Green
    Write-Host "Package: $tarFilePath" -ForegroundColor Green
    
    return $true
}

# Android-specific packaging
function Invoke-AndroidPackaging {
    param (
        [hashtable]$config,
        [string]$version,
        [string]$versionDir
    )
    
    Write-Host "Creating Android package..." -ForegroundColor Yellow
    
    # Validate Android keystore configuration
    if (-not $AndroidKeyStore -or -not (Test-Path $AndroidKeyStore)) {
        Write-Host "Error: Android keystore not found at: $AndroidKeyStore" -ForegroundColor Red
        return $false
    }
    
    if (-not $AndroidKeyAlias) {
        Write-Host "Error: Android key alias not specified" -ForegroundColor Red
        return $false
    }
    
    # Convert SecureString passwords to plain text for dotnet command
    $keystorePassword = $null
    $keyPassword = $null
    
    if ($AndroidKeystorePassword) {
        $keystorePassword = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto([System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($AndroidKeystorePassword))
    }
    
    if ($AndroidKeyPassword) {
        $keyPassword = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto([System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($AndroidKeyPassword))
    }
    
    $androidOutputDir = $config.OutputDir
    $androidPlatformDir = Join-Path $versionDir "android"
    New-Item -ItemType Directory -Path $androidPlatformDir -Force | Out-Null
    
    try {
        # Build Android APK with signing
        $buildArgs = @(
            "publish"
            "-f", $config.Framework
            "-c", "Release"
            "--no-restore"
            "-p:AndroidSigningKeyStore=$AndroidKeyStore"
            "-p:AndroidSigningKeyAlias=$AndroidKeyAlias"
        )
        
        if ($keystorePassword) {
            $buildArgs += "-p:AndroidSigningStorePass=$keystorePassword"
        }
        
        if ($keyPassword) {
            $buildArgs += "-p:AndroidSigningKeyPass=$keyPassword"
        }
        
        Write-Host "Building Android APK..." -ForegroundColor Yellow
        $buildResult = & dotnet @buildArgs 2>&1
        
        if ($LASTEXITCODE -ne 0) {
            Write-Host "Android build failed:" -ForegroundColor Red
            Write-Host $buildResult -ForegroundColor Red
            return $false
        }
        
        # Find the generated APK
        $apkFiles = Get-ChildItem -Path $androidOutputDir -Filter "*.apk" -Recurse
        if (-not $apkFiles) {
            Write-Host "Error: No APK file found in output directory" -ForegroundColor Red
            return $false
        }
        
        # Copy APK to versioned directory
        $sourceApk = $apkFiles[0].FullName
        $targetApk = Join-Path $androidPlatformDir "$APP_NAME-v$version-android.apk"
        Copy-Item -Path $sourceApk -Destination $targetApk -Force
        
        # Create Android-specific documentation
        New-AndroidInstallationDocumentation -platformDir $androidPlatformDir -appVersion $version -apkFileName (Split-Path $targetApk -Leaf)
        
        Write-Host "Android packaging completed!" -ForegroundColor Green
        Write-Host "APK: $targetApk" -ForegroundColor Green
        
        return $true
    }
    catch {
        Write-Host "Android packaging failed: $_" -ForegroundColor Red
        return $false
    }
    finally {
        # Clear sensitive data from memory
        if ($keystorePassword) {
            $keystorePassword = $null
        }
        if ($keyPassword) {
            $keyPassword = $null
        }
    }
}

# Function to get release date
function Get-FormattedReleaseDate {
    return Get-Date -Format "yyyy.MM.dd"
}

# Function to update release metadata
function Update-ReleaseMetadata {
    param (
        [string]$metadataPath,
        [string]$version,
        [int]$revision,
        [string]$releaseDate,
        [string[]]$platforms,
        [string]$releaseNotes = ""
    )
    
    $metadata = @{ "releases" = @() }
    
    if (Test-Path $metadataPath) {
        try {
            $jsonContent = Get-Content $metadataPath -Raw | ConvertFrom-Json
            $metadata = @{ "releases" = @() }
            if ($jsonContent.releases) {
                $metadata.releases = $jsonContent.releases
            }
        }
        catch {
            Write-Host "Error reading metadata file, creating new one: $_" -ForegroundColor Yellow
        }
    }
    
    $releaseInfo = @{
        "version"     = $version
        "revision"    = $revision
        "releaseDate" = $releaseDate
        "timestamp"   = (Get-Date).ToString("o")
        "platforms"   = $platforms
        "notes"       = $releaseNotes
    }
    
    if (-not $metadata.releases) {
        $metadata.releases = @()
    }
    
    $metadata.releases = @($releaseInfo) + $metadata.releases
    $metadata | ConvertTo-Json -Depth 10 | Set-Content -Path $metadataPath -Encoding UTF8
}

# Main execution starts here
Write-Host "CSimple Cross-Platform Publisher v3.0" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan

# Check for dotnet
Write-Host "Checking for dotnet executable..."
if (-not (Test-ToolExists -toolName "dotnet")) {
    Write-Host "dotnet not found in the PATH." -ForegroundColor Red
    exit 1
}
Write-Host "dotnet found." -ForegroundColor Green

# Check Windows-specific tools if building for Windows
if ($Platforms -contains "windows") {
    $signtoolPath = "C:\Program Files (x86)\Windows Kits\10\bin\10.0.19041.0\x64\signtool.exe"
    if (-not (Test-ToolExists -toolName "signtool" -toolPath $signtoolPath)) {
        Write-Host "signtool not found at $signtoolPath - Windows builds will fail" -ForegroundColor Red
        exit 1
    }
}

# Create root destination directory
if (-not (Test-Path $rootDestDir)) {
    New-Item -ItemType Directory -Path $rootDestDir -Force | Out-Null
}

# Get build info and increment it
$buildInfo = Get-IncrementedBuildInfo
$currentRevision = $buildInfo.revision
$appVersion = $buildInfo.version

Write-Host "`nBuilding revision #$currentRevision (v$appVersion) for platforms: $($Platforms -join ', ')" -ForegroundColor Green

# Update project files
$manifestPath = Join-Path $ScriptBaseDir "..\Platforms\Windows\Package.appxmanifest"
Update-ProjectVersion -projectPath $PROJECT_PATH -manifestPath $manifestPath -version $appVersion

# Setup version directory
$releaseDate = Get-FormattedReleaseDate
$versionDirName = "v$appVersion-$releaseDate"
$versionDir = Join-Path $rootDestDir $versionDirName

if (Test-Path $versionDir) {
    Remove-Item -Path "$versionDir\*" -Force -Recurse -ErrorAction SilentlyContinue
}
else {
    New-Item -ItemType Directory -Path $versionDir -Force | Out-Null
}

# Build and package each platform
$successfulBuilds = @()
$failedBuilds = @()

foreach ($platform in $Platforms) {
    $config = $platformConfigs[$platform]
    if (-not $config) {
        Write-Host "Unknown platform: $platform" -ForegroundColor Red
        $failedBuilds += $platform
        continue
    }
    
    Write-Host "`n" + ("=" * 50) -ForegroundColor DarkCyan
    Write-Host "Processing platform: $platform" -ForegroundColor Cyan
    Write-Host ("=" * 50) -ForegroundColor DarkCyan
    
    # Build
    if (Invoke-PlatformBuild -platform $platform -config $config -version $appVersion) {
        # Package
        if (Invoke-PlatformPackaging -platform $platform -config $config -version $appVersion -versionDir $versionDir) {
            $successfulBuilds += $platform
            Write-Host "$platform build and packaging completed successfully!" -ForegroundColor Green
        }
        else {
            $failedBuilds += $platform
        }
    }
    else {
        $failedBuilds += $platform
    }
}

# Create cross-platform documentation
New-CrossPlatformDocumentation -versionDir $versionDir -appVersion $appVersion -releaseDate $releaseDate -platforms $successfulBuilds

# Update release metadata
Update-ReleaseMetadata -metadataPath $releaseMetadataPath -version $appVersion -revision $currentRevision -releaseDate $releaseDate -platforms $successfulBuilds

# Clean up old versions (existing logic can be reused)

# Final summary
Write-Host "`n" + ("=" * 60) -ForegroundColor Green
Write-Host "BUILD SUMMARY" -ForegroundColor Green
Write-Host ("=" * 60) -ForegroundColor Green

if ($successfulBuilds.Count -gt 0) {
    Write-Host "✓ Successful builds: $($successfulBuilds -join ', ')" -ForegroundColor Green
    Write-Host "📁 Output directory: $versionDir" -ForegroundColor Green
}

if ($failedBuilds.Count -gt 0) {
    Write-Host "✗ Failed builds: $($failedBuilds -join ', ')" -ForegroundColor Red
}

Write-Host "`nCross-platform build process completed!" -ForegroundColor Cyan

# Exit with appropriate code
if ($failedBuilds.Count -eq 0) {
    exit 0
}
else {
    exit 1
}
