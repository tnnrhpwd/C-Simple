# Variables
$APP_NAME = "Simple"
$ScriptBaseDir = $PSScriptRoot # Get the directory where the script is located
$PROJECT_PATH = Join-Path $ScriptBaseDir "..\CSimple.csproj"  # Path relative to script location
$OUTPUT_DIR = (Resolve-Path (Join-Path $ScriptBaseDir "..\..\..\published")).Path  # Publish output folder in base directory
$TARGET_FRAMEWORK = "net8.0-windows10.0.19041.0"  # Target framework
$BUILD_CONFIG = "Release"  # Adjust as necessary
$MSI_OUTPUT_DIR = Join-Path $ScriptBaseDir "..\..\..\msi_output" # MSI output folder in base directory
$newCertPassword = "CSimpleNew"  # Password for the new .pfx file
$CertDir = Join-Path $ScriptBaseDir "..\..\..\certs" # Certificate directory in base directory
$cerPath = Join-Path $CertDir "SimpleCert.cer"
$pfxPath = Join-Path $CertDir "CSimple_NewCert.pfx"
$env:PATH += ";C:\Program Files\dotnet"
$subject = "CN=CSimple, O=Simple Org, C=US"
$mappingFilePath = Join-Path $ScriptBaseDir "..\..\..\mapping.txt" # Mapping file in base directory

# Build info file (consolidates version and revision tracking)
$buildInfoPath = Join-Path $ScriptBaseDir "..\..\..\build-info.json" # Build info file in base directory

# Root destination directory 
$rootDestDir = "D:\My Drive\Simple"
# Version-specific paths will be set after determining the version

# Maximum number of versions to keep in the main directory before archiving
$maxVersionsToKeep = 3

# Release metadata file
$releaseMetadataPath = Join-Path $rootDestDir "releases.json"

# Function to check if a certificate with the same CN and O exists
function Test-CertificateExists {
    param (
        [string]$certPath,
        [string]$subject,
        [string]$password
    )
    if (Test-Path $certPath) {
        try {
            # Create secure string for the password
            $securePassword = ConvertTo-SecureString -String $password -Force -AsPlainText
            
            # Use X509Certificate2 class instead which supports password
            $cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2($certPath, $securePassword)
            return $cert.Subject -eq $subject
        }
        catch {
            Write-Host "Error loading certificate: $_"
            return $false
        }
    }
    return $false
}

# Function to check if tool exists
function Test-ToolExists {
    param (
        [string]$toolName,
        [string]$toolPath = ""
    )
    
    if ($toolPath -and (Test-Path $toolPath)) {
        return $true
    }
    
    return (Get-Command $toolName -ErrorAction SilentlyContinue) -ne $null
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

# Function to archive old versions
function Invoke-VersionArchiving {
    param (
        [string]$rootPath,
        [int]$keepCount
    )
    
    $versionDirs = Get-VersionDirectories -rootPath $rootPath
    
    # Create archive directory if it doesn't exist
    $archivePath = Join-Path $rootPath "archive"
    if (-not (Test-Path $archivePath)) {
        New-Item -ItemType Directory -Path $archivePath -Force | Out-Null
    }
    
    # Skip archiving if we don't have more than the keep count
    if ($versionDirs.Count -le $keepCount) {
        return
    }
    
    # Archive older versions (skip the first $keepCount which are the newest)
    $toArchive = $versionDirs | Select-Object -Skip $keepCount
    
    foreach ($dir in $toArchive) {
        $archiveDestination = Join-Path $archivePath $dir.Name
        
        # Skip if already in archive
        if (Test-Path $archiveDestination) {
            continue
        }
        
        Write-Host "Archiving older version: $($dir.Name)" -ForegroundColor Yellow
        
        # Move the directory to the archive
        Move-Item -Path $dir.FullName -Destination $archiveDestination -Force
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
            $metadata = Get-Content $metadataPath -Raw | ConvertFrom-Json -AsHashtable
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
if (-not (Test-Path $CertDir)) { New-Item -ItemType Directory -Path $CertDir } # Create certs directory if it doesn't exist

# Create root destination directory if it doesn't exist
if (-not (Test-Path $rootDestDir)) {
    New-Item -ItemType Directory -Path $rootDestDir -Force | Out-Null
}

# Get build info and increment it
$buildInfo = Get-IncrementedBuildInfo
$currentRevision = $buildInfo.revision
$appVersion = $buildInfo.version

Write-Host "Building revision #$currentRevision (v$appVersion)" -ForegroundColor Green

# Get release date for folder naming
$releaseDate = Get-FormattedReleaseDate

# Setup version-specific paths and names
$versionDirName = "v$appVersion-$releaseDate"
$versionDir = Join-Path $rootDestDir $versionDirName

$msixFileName = "$APP_NAME-v$appVersion.msix"
$certFileName = "$APP_NAME-v$appVersion.cer"
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

# Publish .NET MAUI App
Write-Host "Publishing app..."
# Add /p:AppxPackageSigningEnabled=false to disable signing during publish
dotnet publish $PROJECT_PATH -f $TARGET_FRAMEWORK -c $BUILD_CONFIG -o $OUTPUT_DIR --self-contained /p:AppxPackageSigningEnabled=false
if ($LASTEXITCODE -ne 0) { Write-Host "Publish failed." ; exit 1 }

# Check if the certificate already exists
Write-Host "Checking if certificate with the same CN and O already exists..."
$certificateExists = Test-CertificateExists -certPath $cerPath -subject $subject -password $newCertPassword -or (Test-CertificateExists -certPath $pfxPath -subject $subject -password $newCertPassword)

if ($certificateExists) {
    Write-Host "Certificate with the same CN and O already exists. Using existing certificate."
}
else {
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
        & $signtoolPath sign /f "$pfxPath" /p "$newCertPassword" /fd SHA256 /t http://timestamp.digicert.com "$($file.FullName)"
        if ($LASTEXITCODE -ne 0) {
            Write-Host "Signing $($file.FullName) failed. Please check errors above."
            exit 1
        }
    }

    Write-Host "All files signed successfully with the new certificate."
}

Write-Host "Creating mapping file..."

# Always create a new AppxManifest.xml with the correct format
Write-Host "Creating a new AppxManifest.xml file..."
$defaultManifest = @"
<?xml version="1.0" encoding="utf-8"?>
<Package 
  xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10" 
  xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10" 
  xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities" 
  IgnorableNamespaces="uap rescap">
  
  <Identity 
    Name="CSimple-App" 
    Publisher="CN=CSimple, O=Simple Org, C=US" 
    Version="$appVersion" />
  
  <Properties>
    <DisplayName>Simple</DisplayName>
    <PublisherDisplayName>Simple Org</PublisherDisplayName>
    <Logo>Assets\StoreLogo.png</Logo>
  </Properties>
  
  <Dependencies>
    <TargetDeviceFamily Name="Windows.Desktop" MinVersion="10.0.17763.0" MaxVersionTested="10.0.19041.0" />
  </Dependencies>
  
  <!-- Removed the Resources section that was causing issues -->
  
  <Capabilities>
    <rescap:Capability Name="runFullTrust"/>
  </Capabilities>
  
  <Applications>
    <Application Id="CSimple" Executable="CSimple.exe" EntryPoint="Windows.FullTrustApplication">
      <uap:VisualElements 
        DisplayName="Simple" 
        Description="Simple App" 
        BackgroundColor="transparent" 
        Square150x150Logo="Assets\Square150x150Logo.png" 
        Square44x44Logo="Assets\Square44x44Logo.png">
      </uap:VisualElements>
    </Application>
  </Applications>
</Package>
"@

Set-Content -Path $MSIX_MANIFEST_PATH -Value $defaultManifest -Encoding UTF8

# Check if Assets directory exists, create placeholder assets if not
$assetsDir = Join-Path $OUTPUT_DIR "Assets"
if (-not (Test-Path $assetsDir)) {
    Write-Host "Creating Assets directory and placeholders..."
    New-Item -ItemType Directory -Path $assetsDir -Force | Out-Null
    
    # Create empty placeholder images
    $emptyPng = [byte[]]::new(1024)
    Set-Content -Path (Join-Path $assetsDir "StoreLogo.png") -Value $emptyPng -Encoding Byte
    Set-Content -Path (Join-Path $assetsDir "Square150x150Logo.png") -Value $emptyPng -Encoding Byte
    Set-Content -Path (Join-Path $assetsDir "Square44x44Logo.png") -Value $emptyPng -Encoding Byte
}

# Create the mapping file with only the files from OUTPUT_DIR
# Don't include AppxManifest.xml in mapping when using /m parameter
$mappingContent = "[Files]`r`n"

# Ensure $OUTPUT_DIR is set
if (-not $OUTPUT_DIR) {
    Write-Host "Error: OUTPUT_DIR is not defined." -ForegroundColor Red
    exit 1
}

# Process each file and directory in $OUTPUT_DIR, excluding resources.pri
Get-ChildItem -Path $OUTPUT_DIR -Recurse -File | Where-Object { $_.Name -ne "resources.pri" } | ForEach-Object {
    # Ensure the file path starts with OUTPUT_DIR (case-insensitive)
    if ($_.FullName.StartsWith($OUTPUT_DIR, [System.StringComparison]::OrdinalIgnoreCase)) {
        $relativePath = $_.FullName.Substring($OUTPUT_DIR.Length + 1).Replace("\", "/")
        $mappingContent += "`"$($_.FullName -replace '\\', '/')`" `"$relativePath`"`r`n"
    }
    else {
        Write-Host "Warning: File path doesn't start with OUTPUT_DIR: $($_.FullName)" -ForegroundColor Yellow
        Write-Host "  OUTPUT_DIR: $OUTPUT_DIR" -ForegroundColor Yellow
    }
}

# Output the mapping file content to a file
Set-Content -Path $mappingFilePath -Value $mappingContent -Encoding UTF8

# Debug: Display the first few lines of the mapping file
Write-Host "Debug: First few lines of mapping file:"
Get-Content $mappingFilePath -TotalCount 5 | ForEach-Object { Write-Host $_ }

Write-Host "Mapping file created successfully at $mappingFilePath" -ForegroundColor Green

# Create MSIX package
Write-Host "Creating MSIX package..."
& $makeAppxPath pack /m $MSIX_MANIFEST_PATH /f $mappingFilePath /p $MSIX_OUTPUT_PATH
if ($LASTEXITCODE -ne 0) {
    Write-Host "MSIX package creation failed." -ForegroundColor Red
    exit 1
}

# Sign the MSIX package
Write-Host "Signing MSIX package with the certificate..."
& $signtoolPath sign /f "$pfxPath" /p "$newCertPassword" /fd SHA256 /t http://timestamp.digicert.com "$MSIX_OUTPUT_PATH"
if ($LASTEXITCODE -ne 0) {
    Write-Host "MSIX package signing failed." -ForegroundColor Red
    exit 1
}

# Copy files to the version-specific directory
Write-Host "Copying files to version directory: $versionDir"
try {
    # Copy MSIX and certificate to version directory
    Copy-Item -Path $MSIX_OUTPUT_PATH -Destination (Join-Path $versionDir $msixFileName) -Force
    Copy-Item -Path $cerPath -Destination (Join-Path $versionDir $certFileName) -Force
    
    # Create installation instructions in version directory
    $instructionsPath = Join-Path $versionDir "installation-instructions.txt"
    $instructionsContent = @"
SIMPLE APP INSTALLATION INSTRUCTIONS (v$appVersion)

Before installing the Simple app (v$appVersion), you need to install the certificate:

1. First, double-click on the file: $certFileName
2. Click "Open" if prompted with a security warning
3. In the Certificate window, click "Install Certificate"
4. Select "Local Machine" and click "Next" (requires admin rights)
5. Select "Place all certificates in the following store"
6. Click "Browse" and select "Trusted Root Certification Authorities"
7. Click "Next" and then "Finish"
8. Confirm the security warning by clicking "Yes"

After installing the certificate, you can install the app:
1. Double-click on the file: $msixFileName
2. Click "Install"

Once you've installed the certificate, you won't need to reinstall it for future updates.
"@
    Set-Content -Path $instructionsPath -Value $instructionsContent
    
    # Create enhanced installer batch file with better automation
    $batchPath = Join-Path $versionDir "install.bat"
    $batchContent = @"
@echo off
setlocal enabledelayedexpansion
cls
echo ===============================================
echo Simple App v$appVersion - Quick Installer
echo ===============================================
echo.
echo This installer will:
echo 1. Install security certificate (requires admin)
echo 2. Install Simple application
echo 3. Complete setup automatically
echo.

REM Check for admin privileges
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo ERROR: Administrator privileges required!
    echo.
    echo Please right-click this file and select "Run as administrator"
    echo.
    pause
    exit /b 1
)

echo [1/3] Installing security certificate...
certutil -addstore -f "Root" "$certFileName" >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    echo ✓ Certificate installed successfully
) else (
    echo ✗ Certificate installation failed
    echo.
    echo Trying alternative installation method...
    powershell -command "Import-Certificate -FilePath '$certFileName' -CertStoreLocation Cert:\LocalMachine\Root" >nul 2>&1
    if !ERRORLEVEL! EQU 0 (
        echo ✓ Certificate installed successfully (alternative method)
    ) else (
        echo ✗ Certificate installation failed. Manual installation required.
        echo Please see README.md for manual installation steps.
        pause
        exit /b 1
    )
)
echo.

echo [2/3] Installing Simple application...
powershell -command "Add-AppxPackage -Path '$msixFileName'" >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    echo ✓ Application installed successfully
) else (
    echo ✗ Application installation failed
    echo.
    echo Trying to resolve common issues...
    REM Try to enable sideloading
    powershell -command "Set-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock' -Name 'AllowAllTrustedApps' -Value 1" >nul 2>&1
    echo Retrying application installation...
    powershell -command "Add-AppxPackage -Path '$msixFileName'" >nul 2>&1
    if !ERRORLEVEL! EQU 0 (
        echo ✓ Application installed successfully (after fixes)
    ) else (
        echo ✗ Application installation failed. Please see README.md for troubleshooting.
        pause
        exit /b 1
    )
)
echo.

echo [3/3] Finalizing setup...
timeout /t 2 /nobreak >nul
echo ✓ Installation completed successfully!
echo.
echo Simple App is now available in your Start menu.
echo You can close this window and start using the app.
echo.
echo Thank you for installing Simple App v$appVersion!
echo.
pause
"@
    Set-Content -Path $batchPath -Value $batchContent
    
    # Create PowerShell installer for advanced scenarios
    $psInstallerPath = Join-Path $versionDir "install.ps1"
    $psInstallerContent = @"
# Simple App PowerShell Installer v$appVersion
# This installer provides more robust error handling and diagnostics

param(
    [switch]`$Force,
    [switch]`$Quiet
)

`$ErrorActionPreference = "Stop"

function Write-Status {
    param([string]`$Message, [string]`$Status = "INFO")
    if (-not `$Quiet) {
        switch (`$Status) {
            "SUCCESS" { Write-Host "✓ `$Message" -ForegroundColor Green }
            "ERROR"   { Write-Host "✗ `$Message" -ForegroundColor Red }
            "WARNING" { Write-Host "⚠ `$Message" -ForegroundColor Yellow }
            default   { Write-Host "ℹ `$Message" -ForegroundColor Cyan }
        }
    }
}

function Test-AdminPrivileges {
    `$currentUser = [Security.Principal.WindowsIdentity]::GetCurrent()
    `$principal = New-Object Security.Principal.WindowsPrincipal(`$currentUser)
    return `$principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Install-Certificate {
    param([string]`$CertPath)
    
    try {
        # Method 1: PowerShell Import-Certificate
        Import-Certificate -FilePath `$CertPath -CertStoreLocation Cert:\LocalMachine\Root -ErrorAction Stop
        Write-Status "Certificate installed successfully" "SUCCESS"
        return `$true
    }
    catch {
        Write-Status "PowerShell method failed, trying certutil..." "WARNING"
        
        # Method 2: certutil
        `$result = Start-Process -FilePath "certutil" -ArgumentList @("-addstore", "-f", "Root", `$CertPath) -Wait -PassThru -NoNewWindow
        if (`$result.ExitCode -eq 0) {
            Write-Status "Certificate installed successfully (certutil)" "SUCCESS"
            return `$true
        }
        else {
            Write-Status "Certificate installation failed with both methods" "ERROR"
            return `$false
        }
    }
}

function Install-Application {
    param([string]`$AppPath)
    
    try {
        # Enable developer mode and sideloading if needed
        Set-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock" -Name "AllowAllTrustedApps" -Value 1 -ErrorAction SilentlyContinue
        Set-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock" -Name "AllowDevelopmentWithoutDevLicense" -Value 1 -ErrorAction SilentlyContinue
        
        # Install the MSIX package
        Add-AppxPackage -Path `$AppPath -ErrorAction Stop
        Write-Status "Application installed successfully" "SUCCESS"
        return `$true
    }
    catch {
        Write-Status "Application installation failed: `$(`$_.Exception.Message)" "ERROR"
        return `$false
    }
}

# Main installation process
if (-not `$Quiet) {
    Clear-Host
    Write-Host "===============================================" -ForegroundColor Cyan
    Write-Host "Simple App v$appVersion - PowerShell Installer" -ForegroundColor Cyan
    Write-Host "===============================================" -ForegroundColor Cyan
    Write-Host ""
}

# Check admin privileges
if (-not (Test-AdminPrivileges)) {
    Write-Status "Administrator privileges required!" "ERROR"
    Write-Status "Please run PowerShell as Administrator and try again" "ERROR"
    if (-not `$Quiet) { Read-Host "Press Enter to exit" }
    exit 1
}

Write-Status "Starting installation process..."

# Install certificate
Write-Status "Installing security certificate..."
if (-not (Install-Certificate "$certFileName")) {
    Write-Status "Certificate installation failed. Installation cannot continue." "ERROR"
    if (-not `$Quiet) { Read-Host "Press Enter to exit" }
    exit 1
}

# Install application
Write-Status "Installing Simple application..."
if (-not (Install-Application "$msixFileName")) {
    Write-Status "Application installation failed." "ERROR"
    Write-Status "Please check Windows version (requires Windows 10 1809+) and try again" "WARNING"
    if (-not `$Quiet) { Read-Host "Press Enter to exit" }
    exit 1
}

Write-Status "Installation completed successfully!" "SUCCESS"
Write-Status "Simple App is now available in your Start menu" "SUCCESS"

if (-not `$Quiet) {
    Write-Host ""
    Write-Host "Thank you for installing Simple App v$appVersion!" -ForegroundColor Green
    Read-Host "Press Enter to exit"
}
"@
    Set-Content -Path $psInstallerPath -Value $psInstallerContent -Encoding UTF8
    
    # Create README.md for end users in version directory
    $readmePath = Join-Path $versionDir "README.md"
    $readmeContent = @"
# 🚀 Simple App Installation Guide

**Version:** $appVersion | **Build Date:** $releaseDate

## ⚡ One-Click Installation (Recommended)

### 🎯 Super Quick Setup (30 seconds)
1. **Right-click** on `install.bat` → **"Run as administrator"**
2. **Click "Yes"** when Windows asks for permission
3. **Wait** for the automated installation (certificate + app)
4. **Done!** Find "Simple" in your Start menu

> **💡 Pro Tip:** The installer handles everything automatically - no manual certificate steps needed!

---

## 🔧 Alternative Installation Methods

### Method 2: PowerShell Installer (Advanced Users)
```powershell
# Right-click PowerShell → "Run as administrator", then:
.\install.ps1
```
*Better error handling and diagnostics*

### Method 3: Manual Installation (If Automated Fails)

**Step 1: Install Certificate**
- Double-click `$certFileName`
- Click "Install Certificate" → "Local Machine" → "Next"
- Select "Trusted Root Certification Authorities" → "Next" → "Finish"

**Step 2: Install App**
- Double-click `$msixFileName` → "Install"

---

## 📁 What's in This Folder?

| File | What It Does |
|------|--------------|
| **`install.bat`** | 🟢 **One-click installer** (recommended) |
| `install.ps1` | 🔧 PowerShell installer (advanced) |
| `$msixFileName` | 📱 Main app installer |
| `$certFileName` | 🔐 Security certificate |
| `installation-instructions.txt` | 📄 Detailed text instructions |
| `README.md` | 📖 This guide |

---

## ❗ Quick Troubleshooting

### "Access Denied" / "Permission Error"
**Solution:** Right-click `install.bat` → "Run as administrator"

### "Certificate not trusted"
**Solution:** The automated installer handles this - use `install.bat`

### "App installation failed"
**Solutions:**
- Try the PowerShell installer: `install.ps1`
- Check Windows version (needs Windows 10 1809+)
- Disable antivirus temporarily during installation

### Still Having Issues?
1. Use `install.ps1` for detailed error messages
2. Check Windows Update (install latest updates)
3. Restart computer and try again

---

## 💻 System Requirements

- **Windows 10** version 1809 or newer
- **Administrator access** (for certificate installation)
- **5 minutes** of your time ⏱️

---

## 🔄 Installing Updates

**Good News:** Future updates are even easier!
- Certificate stays installed ✅
- Just run the new `install.bat` from newer versions
- No admin rights needed for app updates

---

## 🎉 Success!

**Installation worked?** Look for "Simple" in your Start menu and you're ready to go!

"@
    Set-Content -Path $readmePath -Value $readmeContent -Encoding UTF8
    
    # Create a version.txt in version directory (for backward compatibility)
    Set-Content -Path (Join-Path $versionDir "version.txt") -Value $appVersion
    
    # Create a revision.txt in version directory (for backward compatibility)
    Set-Content -Path (Join-Path $versionDir "revision.txt") -Value $currentRevision
    
    # Copy build-info.json to version directory
    Copy-Item -Path $buildInfoPath -Destination (Join-Path $versionDir "build-info.json") -Force
    
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
if (Test-Path $MSIX_OUTPUT_PATH) { Remove-Item $MSIX_OUTPUT_PATH -Force }

Write-Host "Process complete! Files have been published to:" -ForegroundColor Green
Write-Host "- Version directory: $versionDir" -ForegroundColor Green
Write-Host ""
Write-Host "MSIX package: $(Join-Path $versionDir $msixFileName)" -ForegroundColor Green
Write-Host "Certificate: $(Join-Path $versionDir $certFileName)" -ForegroundColor Green