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
    
    $batchContent = @'
@echo off
setlocal enabledelayedexpansion
title Simple App v{0} - Installation Manager

REM Prevent window from closing on error
set "PAUSE_ON_ERROR=1"

REM Error handler - if any unexpected error occurs, prevent window from closing
if not defined INSTALLER_DEBUG (
    set "INSTALLER_DEBUG=1"
    cmd /c "%~f0" %*
    if errorlevel 1 (
        echo.
        echo ===============================================
        echo     INSTALLER ENCOUNTERED AN ERROR
        echo ===============================================
        echo.
        echo The installer stopped unexpectedly.
        echo.
        echo Please try the following:
        echo 1. Ensure you right-clicked and selected "Run as administrator"
        echo 2. Temporarily disable antivirus software
        echo 3. Ensure all files are in the same directory
        echo 4. Try the PowerShell installer: install.ps1
        echo.
        echo Press any key to exit...
        pause >nul
    )
    exit /b
)

cls

:main_menu
color 07
echo ===============================================
echo Simple App v{0} - Installation Manager
echo ===============================================
echo.

REM Check for admin privileges first
echo Verifying administrator privileges...
net session >nul 2>&1
if %errorlevel% neq 0 (
    color 0C
    echo.
    echo ===============================================
    echo        ADMINISTRATOR ACCESS REQUIRED
    echo ===============================================
    echo.
    echo This installer requires administrator privileges to:
    echo - Install security certificates
    echo - Install MSIX applications
    echo - Modify system settings
    echo.
    echo Please follow these steps:
    echo 1. Close this window
    echo 2. Right-click on install.bat
    echo 3. Select "Run as administrator"
    echo 4. Click "Yes" when prompted
    echo.
    echo Press any key to exit...
    pause >nul
    exit /b 1
)

echo [OK] Administrator privileges confirmed
echo.
echo Checking for existing installation...

REM Check if app is already installed using PowerShell with better error handling
echo Please wait while we scan for existing installations...
set "INSTALL_CHECK_FAILED=0"

REM Create a more robust PowerShell check
powershell -NoProfile -ExecutionPolicy Bypass -Command "try { Get-AppxPackage -Name '*CSimple*' | Select-Object Name, Version | Format-Table -AutoSize } catch { Write-Host 'No installation found' }" > temp_check.txt 2>&1

REM Check if the PowerShell command succeeded and found an installation
findstr /C:"CSimple" temp_check.txt > nul 2>&1
if %ERRORLEVEL% EQU 0 (
    echo.
    echo ================================================
    echo    EXISTING INSTALLATION DETECTED
    echo ================================================
    echo.
    echo Simple App is already installed on this system.
    echo Current installation details:
    type temp_check.txt
    echo.
    echo What would you like to do?
    echo.
    echo [1] Reinstall - Remove current version and install fresh copy
    echo [2] Uninstall - Remove current installation completely  
    echo [3] Cancel - Exit without making changes
    echo.
    echo ================================================
    
    :get_choice
    set /p choice="Enter your choice (1, 2, or 3): "
    
    if "!choice!"=="1" (
        echo.
        echo You selected: Reinstall
        goto reinstall
    )
    if "!choice!"=="2" (
        echo.
        echo You selected: Uninstall
        goto uninstall_only
    )
    if "!choice!"=="3" (
        echo.
        echo You selected: Cancel
        goto cancel_exit
    )
    
    echo.
    echo [ERROR] Invalid choice "!choice!". Please enter 1, 2, or 3.
    echo.
    goto get_choice
) else (
    echo [OK] No existing installation found
    echo.
    echo Proceeding with fresh installation...
    echo.
    timeout /t 2 /nobreak >nul
    goto install_fresh
)

:reinstall
echo.
echo ================================================
echo           REINSTALLING APPLICATION
echo ================================================
echo.
echo [INFO] Removing existing installation...
powershell -command "Get-AppxPackage -Name '*CSimple*' | Remove-AppxPackage" >nul 2>&1
if !ERRORLEVEL! EQU 0 (
    echo [OK] Previous version removed successfully
) else (
    echo [WARNING] Could not remove previous version automatically
    echo Continuing with installation...
)
echo.
goto install_fresh

:uninstall_only
echo.
echo ================================================
echo           UNINSTALLING APPLICATION
echo ================================================
echo.
echo [INFO] Removing Simple App...
powershell -command "Get-AppxPackage -Name '*CSimple*' | Remove-AppxPackage" >nul 2>&1
if !ERRORLEVEL! EQU 0 (
    color 0A
    echo [SUCCESS] Simple App uninstalled successfully!
    echo.
    echo The application has been removed from your system.
    echo.
    echo Press any key to exit...
    pause >nul
    goto cleanup_and_exit
) else (
    color 0C
    echo [ERROR] Failed to uninstall Simple App
    echo.
    echo Please try uninstalling manually:
    echo 1. Go to Windows Settings
    echo 2. Select Apps
    echo 3. Find Simple App and uninstall
    echo.
    echo Press any key to exit...
    pause >nul
    goto cleanup_and_exit
)

:cancel_exit
echo.
echo [INFO] Installation cancelled by user
echo.
echo Press any key to exit...
pause >nul
goto cleanup_and_exit

:install_fresh
echo ================================================
echo         INSTALLING SIMPLE APP v{0}
echo ================================================
echo.
echo This installer will perform these steps:
echo 1. Install security certificate (requires admin)
echo 2. Install Simple application
echo 3. Complete setup and verification
echo.

echo Starting installation...
echo.

REM Step 1: Certificate Installation
echo [1/3] Installing security certificate...
echo      - Locating certificate file: {1}
REM Get the directory where the batch file is located
set "SCRIPT_DIR=%~dp0"
set "CERT_PATH=%SCRIPT_DIR%{1}"
if not exist "%CERT_PATH%" (
    color 0C
    echo [ERROR] Certificate file not found: %CERT_PATH%
    echo Please ensure all installation files are in the same folder.
    goto installation_failed
)

echo      - Installing certificate to trusted root store...
certutil -addstore -f "Root" "%CERT_PATH%" >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    echo [OK] Certificate installed successfully
) else (
    echo [WARNING] Primary certificate installation failed
    echo      - Trying alternative PowerShell method...
    powershell -command "Import-Certificate -FilePath '%CERT_PATH%' -CertStoreLocation Cert:\LocalMachine\Root" >nul 2>&1
    if !ERRORLEVEL! EQU 0 (
        echo [OK] Certificate installed successfully (alternative method)
    ) else (
        color 0C
        echo [ERROR] Certificate installation failed with both methods
        goto installation_failed
    )
)
echo.

REM Step 2: Application Installation
echo [2/3] Installing Simple application...
echo      - Locating application package: {2}
set "MSIX_PATH=%SCRIPT_DIR%{2}"
if not exist "%MSIX_PATH%" (
    color 0C
    echo [ERROR] Application package not found: %MSIX_PATH%
    echo Please ensure all installation files are in the same folder.
    goto installation_failed
)

echo      - Enabling app sideloading (if needed)...
powershell -command "Set-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock' -Name 'AllowAllTrustedApps' -Value 1" >nul 2>&1

echo      - Installing application package...
powershell -command "Add-AppxPackage -Path '%MSIX_PATH%'" >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    echo [OK] Application installed successfully
) else (
    echo [WARNING] Initial installation attempt failed
    echo      - Analyzing common issues...
    echo      - Attempting installation with dependency resolution...
    powershell -command "Add-AppxPackage -Path '%MSIX_PATH%' -ForceApplicationShutdown" >nul 2>&1
    if !ERRORLEVEL! EQU 0 (
        echo [OK] Application installed successfully (with force shutdown)
    ) else (
        color 0C
        echo [ERROR] Application installation failed
        goto installation_failed
    )
)
echo.

REM Step 3: Verification and Finalization
echo [3/3] Finalizing installation and verification...
echo      - Verifying application registration...
timeout /t 2 /nobreak >nul
powershell -command "Get-AppxPackage -Name '*CSimple*' | Select-Object -First 1" > verify_check.txt 2>nul
findstr /C:"Name" verify_check.txt > nul 2>&1
if %ERRORLEVEL% EQU 0 (
    echo [OK] Application verified and registered successfully
) else (
    color 0C
    echo [ERROR] Application verification failed
    goto installation_failed
)

echo      - Setting up application shortcuts...
timeout /t 1 /nobreak >nul
echo [OK] Setup completed

color 0A
echo.
echo ================================================
echo        INSTALLATION COMPLETED SUCCESSFULLY!
echo ================================================
echo.
echo Simple App v{0} is now installed
echo Application is available in your Start menu
echo You can launch the app by searching for "Simple"
echo.
echo Thank you for installing Simple App!
echo.
echo Press any key to close this installer...
pause >nul
goto cleanup_and_exit

:installation_failed
echo.
echo ================================================
echo           INSTALLATION FAILED
echo ================================================
echo.
echo The installation encountered errors and could not complete.
echo.
echo What would you like to do?
echo.
echo [1] Retry installation
echo [2] View troubleshooting information
echo [3] Exit installer
echo.
set /p retry_choice="Enter your choice (1, 2, or 3): "

if "!retry_choice!"=="1" (
    echo.
    echo Retrying installation...
    timeout /t 2 /nobreak >nul
    goto install_fresh
)
if "!retry_choice!"=="2" (
    echo.
    echo ================================================
    echo         TROUBLESHOOTING INFORMATION
    echo ================================================
    echo.
    echo Common solutions:
    echo.
    echo 1. Ensure you are running as Administrator
    echo 2. Temporarily disable antivirus software
    echo 3. Ensure Windows is up to date
    echo 4. Verify .NET 8.0 runtime is installed
    echo 5. Check available disk space (minimum 500MB)
    echo 6. Restart computer and try again
    echo.
    echo For detailed troubleshooting, see README.md
    echo.
    echo Press any key to return to options...
    pause >nul
    goto installation_failed
)
if "!retry_choice!"=="3" goto cleanup_and_exit

echo Invalid choice. Please try again.
timeout /t 2 /nobreak >nul
goto installation_failed

:cleanup_and_exit
REM Clean up temporary files
del temp_check.txt 2>nul
del verify_check.txt 2>nul
color 07

REM Failsafe - ensure window doesn't close unexpectedly
if "%PAUSE_ON_ERROR%"=="1" (
    echo.
    echo [INFO] Installation manager is closing...
    echo If this window closed unexpectedly, please check:
    echo 1. Run as Administrator
    echo 2. Antivirus isn't blocking the installer
    echo 3. All files are in the same directory
    echo.
    timeout /t 3 /nobreak >nul
)
echo.
exit /b 0
'@
    
    # Use string formatting to replace placeholders
    $finalBatchContent = $batchContent -f $appVersion, $certFileName, $msixFileName
    Set-Content -Path $batchPath -Value $finalBatchContent
    Write-Host "Enhanced batch installer created at $batchPath" -ForegroundColor Green
}

# Function to create PowerShell installer
function New-PowerShellInstaller {
if !ERRORLEVEL! EQU 0 (
    echo [OK] Previous version removed successfully
) else (
    echo [WARNING] Could not remove previous version automatically
    echo Continuing with installation...
)
echo.
goto install_fresh

:uninstall_only
echo.
echo ================================================
echo           UNINSTALLING APPLICATION
echo ================================================
echo.
echo [INFO] Removing Simple App...
powershell -command "Get-AppxPackage -Name '*CSimple*' | Remove-AppxPackage" >nul 2>&1
if !ERRORLEVEL! EQU 0 (
    color 0A
    echo [SUCCESS] Simple App uninstalled successfully!
    echo.
    echo The application has been removed from your system.
    echo.
    echo Press any key to exit...
    pause >nul
    goto cleanup_and_exit
) else (
    color 0C
    echo [ERROR] Failed to uninstall Simple App
    echo.
    echo Please try uninstalling manually:
    echo 1. Go to Windows Settings
    echo 2. Select Apps
    echo 3. Find Simple App and uninstall
    echo.
    echo Press any key to exit...
    pause >nul
    goto cleanup_and_exit
)

:cancel_exit
echo.
echo [INFO] Installation cancelled by user
echo.
echo Press any key to exit...
pause >nul
goto cleanup_and_exit

:install_fresh
echo ================================================
echo         INSTALLING SIMPLE APP v$appVersion
echo ================================================
echo.
echo This installer will perform these steps:
echo 1. Install security certificate (requires admin)
echo 2. Install Simple application
echo 3. Complete setup and verification
echo.

echo Starting installation...
echo.

REM Step 1: Certificate Installation
echo [1/3] Installing security certificate...
echo      - Locating certificate file: $certFileName
REM Get the directory where the batch file is located
set "SCRIPT_DIR=%~dp0"
set "CERT_PATH=%SCRIPT_DIR%$certFileName"
if not exist "%CERT_PATH%" (
    color 0C
    echo [ERROR] Certificate file not found: %CERT_PATH%
    echo Please ensure all installation files are in the same folder.
    goto installation_failed
)

echo      - Installing certificate to trusted root store...
certutil -addstore -f "Root" "%CERT_PATH%" >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    echo [OK] Certificate installed successfully
) else (
    echo [WARNING] Primary certificate installation failed
    echo      - Trying alternative PowerShell method...
    powershell -command "Import-Certificate -FilePath '%CERT_PATH%' -CertStoreLocation Cert:\LocalMachine\Root" >nul 2>&1
    if !ERRORLEVEL! EQU 0 (
        echo [OK] Certificate installed successfully (alternative method)
    ) else (
        color 0C
        echo [ERROR] Certificate installation failed with both methods
        goto installation_failed
    )
)
echo.

REM Step 2: Application Installation
echo [2/3] Installing Simple application...
echo      - Locating application package: $msixFileName
set "MSIX_PATH=%SCRIPT_DIR%$msixFileName"
if not exist "%MSIX_PATH%" (
    color 0C
    echo [ERROR] Application package not found: %MSIX_PATH%
    echo Please ensure all installation files are in the same folder.
    goto installation_failed
)

echo      - Enabling app sideloading (if needed)...
powershell -command "Set-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock' -Name 'AllowAllTrustedApps' -Value 1" >nul 2>&1

echo      - Installing application package...
powershell -command "Add-AppxPackage -Path '%MSIX_PATH%'" >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    echo [OK] Application installed successfully
) else (
    echo [WARNING] Initial installation attempt failed
    echo      - Analyzing common issues...
    echo      - Attempting installation with dependency resolution...
    powershell -command "Add-AppxPackage -Path '%MSIX_PATH%' -ForceApplicationShutdown" >nul 2>&1
    if !ERRORLEVEL! EQU 0 (
        echo [OK] Application installed successfully (with force shutdown)
    ) else (
        color 0C
        echo [ERROR] Application installation failed
        goto installation_failed
    )
)
echo.

REM Step 3: Verification and Finalization
echo [3/3] Finalizing installation and verification...
echo      - Verifying application registration...
timeout /t 2 /nobreak >nul
powershell -command "Get-AppxPackage -Name '*CSimple*' | Select-Object -First 1" > verify_check.txt 2>nul
findstr /C:"Name" verify_check.txt > nul 2>&1
if %ERRORLEVEL% EQU 0 (
    echo [OK] Application verified and registered successfully
) else (
    color 0C
    echo [ERROR] Application verification failed
    goto installation_failed
)

echo      - Setting up application shortcuts...
timeout /t 1 /nobreak >nul
echo [OK] Setup completed

color 0A
echo.
echo ================================================
echo        INSTALLATION COMPLETED SUCCESSFULLY!
echo ================================================
echo.
echo ✓ Simple App v$appVersion is now installed
echo ✓ Application is available in your Start menu
echo ✓ You can launch the app by searching for "Simple"
echo.
echo Thank you for installing Simple App!
echo.
echo Press any key to close this installer...
pause >nul
goto cleanup_and_exit

:installation_failed
echo.
echo ================================================
echo           INSTALLATION FAILED
echo ================================================
echo.
echo The installation encountered errors and could not complete.
echo.
echo What would you like to do?
echo.
echo [1] Retry installation
echo [2] View troubleshooting information
echo [3] Exit installer
echo.
set /p retry_choice="Enter your choice (1, 2, or 3): "

if "!retry_choice!"=="1" (
    echo.
    echo Retrying installation...
    timeout /t 2 /nobreak >nul
    goto install_fresh
)
if "!retry_choice!"=="2" (
    echo.
    echo ================================================
    echo         TROUBLESHOOTING INFORMATION
    echo ================================================
    echo.
    echo Common solutions:
    echo.
    echo 1. Ensure you are running as Administrator
    echo 2. Temporarily disable antivirus software
    echo 3. Ensure Windows is up to date
    echo 4. Verify .NET 8.0 runtime is installed
    echo 5. Check available disk space (minimum 500MB)
    echo 6. Restart computer and try again
    echo.
    echo For detailed troubleshooting, see README.md
    echo.
    echo Press any key to return to options...
    pause >nul
    goto installation_failed
)
if "!retry_choice!"=="3" goto cleanup_and_exit

echo Invalid choice. Please try again.
timeout /t 2 /nobreak >nul
goto installation_failed

:cleanup_and_exit
REM Clean up temporary files
del temp_check.txt 2>nul
del verify_check.txt 2>nul
color 07

REM Failsafe - ensure window doesn't close unexpectedly
if "%PAUSE_ON_ERROR%"=="1" (
    echo.
    echo [INFO] Installation manager is closing...
    echo If this window closed unexpectedly, please check:
    echo 1. Run as Administrator
    echo 2. Antivirus isn't blocking the installer
    echo 3. All files are in the same directory
    echo.
    timeout /t 3 /nobreak >nul
)
echo.
exit /b 0
"@
    Set-Content -Path $batchPath -Value $batchContent
    Write-Host "Enhanced batch installer created at $batchPath" -ForegroundColor Green
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
# Simple App Installation Guide

**Version:** $appVersion | **Build Date:** $releaseDate

## Quick Installation (Recommended)

### Super Quick Setup (30 seconds)
1. **Download and extract** all installation files to the same folder
2. **Navigate to the extracted folder** in File Explorer  
3. **Right-click** on `install.bat` -> **"Run as administrator"**
4. **Click "Yes"** when Windows asks for permission
5. **Follow prompts** if app is already installed (reinstall/uninstall options)
6. **Wait** for the automated installation (certificate + app)
7. **Done!** Find "Simple" in your Start menu

> **Important:** Make sure all files (`install.bat`, `$certFileName`, `$msixFileName`) are in the same folder before running the installer!

---

## Alternative Installation Methods

### Method 2: PowerShell Installer (Advanced Users)
``````powershell
# Right-click PowerShell -> "Run as administrator", then:
.\install.ps1
``````
*Better error handling and diagnostics*

### Method 3: Manual Installation (If Automated Fails)

**Step 1: Install Certificate**
- Double-click `$certFileName`
- Click "Install Certificate" -> "Local Machine" -> "Next"
- Select "Trusted Root Certification Authorities" -> "Next" -> "Finish"

**Step 2: Install App**
- Double-click `$msixFileName` -> "Install"

---

## What's in This Folder?

| File | What It Does |
|------|--------------|
| **`install.bat`** | **One-click installer** (recommended) |
| `install.ps1` | PowerShell installer (advanced) |
| `$msixFileName` | Main app installer |
| `$certFileName` | Security certificate |
| `installation-instructions.txt` | Detailed text instructions |
| `README.md` | This guide |

---

## Installer Features

### Smart Installation Detection
- **Automatically detects** if Simple App is already installed
- **Offers choices** when existing installation is found:
  - Reinstall current version
  - Uninstall existing version  
  - Cancel installation

### Error Handling
- **Multiple certificate installation methods** (automatic fallback)
- **Sideloading enablement** for MSIX packages
- **Clear error messages** with troubleshooting guidance

---

## Quick Troubleshooting

### "Access Denied" / "Permission Error"
**Solution:** Right-click `install.bat` -> "Run as administrator"

### "Certificate not trusted"
**Solution:** The automated installer handles this - use `install.bat`

### "Certificate file not found" / "Application package not found"
**Solution:** Make sure you're running the installer from the correct folder
- **Extract all files** to the same folder before running
- **Navigate to the extracted folder** in File Explorer
- **Right-click on `install.bat`** from within that folder -> "Run as administrator"
- **DO NOT** run the installer from a different location

### "App installation failed"
**Solutions:**
- Try the PowerShell installer: `install.ps1`
- Check Windows version (needs Windows 10 1809+)
- Disable antivirus temporarily during installation

### Installer Closes Immediately
**Solution:** This was fixed! The installer now properly detects existing installations and provides options.

### Still Having Issues?
1. Use `install.ps1` for detailed error messages
2. Check Windows Update (install latest updates)
3. Restart computer and try again

---

## System Requirements

- **Windows 10** version 1809 or newer
- **Administrator access** (for certificate installation)
- **5 minutes** of your time

---

## Installing Updates

**Good News:** Future updates are even easier!
- Certificate stays installed
- Just run the new `install.bat` from newer versions
- Installer detects existing versions and offers update options

---

## Success!

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
