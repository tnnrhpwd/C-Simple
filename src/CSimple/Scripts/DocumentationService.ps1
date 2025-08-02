# Documentation Service
# This service handles all documentation and installer file creation for the C-Simple application

# Function to create installation instructions text file
function New-InstallationInstructions {
    param (
        [string]$instructionsPath,
        [string]$appVersion,
        [string]$certFileName,
        [string]$msixFileName
    )
    
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
    Write-Host "Installation instructions created at $instructionsPath" -ForegroundColor Green
}

# Function to create enhanced batch installer
function New-BatchInstaller {
    param (
        [string]$batchPath,
        [string]$appVersion,
        [string]$certFileName,
        [string]$msixFileName
    )
    
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
    Write-Host "Batch installer created at $batchPath" -ForegroundColor Green
}

# Function to create PowerShell installer
function New-PowerShellInstaller {
    param (
        [string]$psInstallerPath,
        [string]$appVersion,
        [string]$certFileName,
        [string]$msixFileName
    )
    
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
    Write-Host "PowerShell installer created at $psInstallerPath" -ForegroundColor Green
}

# Function to create user README.md
function New-UserReadme {
    param (
        [string]$readmePath,
        [string]$appVersion,
        [string]$releaseDate,
        [string]$certFileName,
        [string]$msixFileName
    )
    
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
``````powershell
# Right-click PowerShell → "Run as administrator", then:
.\install.ps1
``````
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
    Write-Host "User README created at $readmePath" -ForegroundColor Green
}

# Function to create build info file (primary build information source)
function New-BuildInfoFile {
    param (
        [string]$versionDir,
        [string]$buildInfoPath
    )
    
    # Copy build-info.json to version directory as the single source of build information
    Copy-Item -Path $buildInfoPath -Destination (Join-Path $versionDir "build-info.json") -Force
    
    Write-Host "Build info file copied to $versionDir (primary build information source)" -ForegroundColor Green
}

# Main function to create all documentation files
function New-InstallationDocumentation {
    param (
        [string]$versionDir,
        [string]$appVersion,
        [string]$releaseDate,
        [string]$certFileName,
        [string]$msixFileName,
        [string]$buildInfoPath
    )
    
    Write-Host "Creating installation documentation..." -ForegroundColor Cyan
    
    # Create all documentation files
    $instructionsPath = Join-Path $versionDir "installation-instructions.txt"
    $batchPath = Join-Path $versionDir "install.bat"
    $psInstallerPath = Join-Path $versionDir "install.ps1"
    $readmePath = Join-Path $versionDir "README.md"
    
    New-InstallationInstructions -instructionsPath $instructionsPath -appVersion $appVersion -certFileName $certFileName -msixFileName $msixFileName
    New-BatchInstaller -batchPath $batchPath -appVersion $appVersion -certFileName $certFileName -msixFileName $msixFileName
    New-PowerShellInstaller -psInstallerPath $psInstallerPath -appVersion $appVersion -certFileName $certFileName -msixFileName $msixFileName
    New-UserReadme -readmePath $readmePath -appVersion $appVersion -releaseDate $releaseDate -certFileName $certFileName -msixFileName $msixFileName
    New-BuildInfoFile -versionDir $versionDir -buildInfoPath $buildInfoPath
    
    Write-Host "All installation documentation created successfully!" -ForegroundColor Green
}
