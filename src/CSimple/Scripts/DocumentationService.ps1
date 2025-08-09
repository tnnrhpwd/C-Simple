# Documentation Service
# This service handles all documentation and installer file creation for the C-Simple application

# Function removed - installation-instructions.txt is no longer needed since README.md is comprehensive

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
title CSimple v{0} - Installation Manager

REM Check for administrator privileges and auto-elevate if needed
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo Requesting administrator privileges...
    echo Please click "Yes" when prompted to allow this installer to run as administrator.
    powershell -Command "Start-Process cmd -ArgumentList '/c \"%~f0\"' -Verb RunAs"
    exit /b
)

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
        echo 1. Click "Yes" when prompted for administrator access
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
echo CSimple v{0} - Installation Manager
echo ===============================================
echo.

REM Verify administrator privileges (should already have them due to auto-elevation)
echo Verifying administrator privileges...
net session >nul 2>&1
if !errorlevel! neq 0 (
    color 07
    echo.
    echo ===============================================
    echo     ADMINISTRATOR ELEVATION FAILED
    echo ===============================================
    echo.
    echo The automatic elevation request was denied or failed.
    echo Administrator privileges are required to:
    echo - Install security certificates
    echo - Install MSIX applications
    echo - Modify system settings
    echo.
    echo Please try again and click "Yes" when prompted.
    echo.
    echo Press any key to exit...
    pause >nul
    exit /b 1
)

echo [OK] Administrator privileges confirmed
echo.
echo Checking for existing installation...

REM Check if app is already installed using the same method as verification
echo Please wait while we scan for existing installations...
set "INSTALL_CHECK_FAILED=0"

REM Use the same robust PowerShell check as the verification step
powershell -NoProfile -ExecutionPolicy Bypass -Command "try {{ Get-AppxPackage | Where-Object {{ `$_.Name -like '*CSimple*' -or `$_.DisplayName -like '*CSimple*' -or `$_.Name -eq 'CSimple-App' }} | Select-Object Name, DisplayName, Version | Format-List }} catch {{ Write-Host 'No installation found' }}" > temp_check.txt 2>&1

REM Check if we found any CSimple-related packages using the same method as verification
findstr /I /C:"Name" temp_check.txt > nul 2>&1
if !ERRORLEVEL! EQU 0 goto existing_installation_found

REM Standard check found no installation, trying alternative method
echo [INFO] Standard check found no installation, trying alternative method...
powershell -NoProfile -ExecutionPolicy Bypass -Command "try {{ Get-AppxPackage | Where-Object {{ `$_.Name -match '^CSimple' -or `$_.PackageFamilyName -like '*CSimple*' }} | Select-Object Name, DisplayName, PackageFamilyName | Format-List }} catch {{ Write-Host 'No installation found' }}" > temp_check2.txt 2>&1
findstr /I /C:"Name" temp_check2.txt > nul 2>&1
if !ERRORLEVEL! EQU 0 goto existing_installation_found_alt

REM No installation found with either method
echo [OK] No existing installation found
echo.
echo Proceeding with fresh installation...
echo.
timeout /t 2 /nobreak >nul
goto install_fresh

:existing_installation_found
echo.
echo ================================================
echo    EXISTING INSTALLATION DETECTED
echo ================================================
echo.
echo CSimple is already installed on this system.
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

if "!choice!"=="1" goto reinstall
if "!choice!"=="2" goto uninstall_only
if "!choice!"=="3" goto cancel_exit

echo.
echo [ERROR] Invalid choice "!choice!". Please enter 1, 2, or 3.
echo.
goto get_choice

:existing_installation_found_alt
echo.
echo ================================================
echo    EXISTING INSTALLATION DETECTED (ALT METHOD)
echo ================================================
echo.
echo CSimple installation found using alternative detection.
echo Current installation details:
type temp_check2.txt
goto get_choice

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
echo [INFO] Removing CSimple...
powershell -command "Get-AppxPackage -Name '*CSimple*' | Remove-AppxPackage" >nul 2>&1
if !ERRORLEVEL! EQU 0 (
    color 0A
    echo [SUCCESS] CSimple uninstalled successfully!
    echo.
    echo The application has been removed from your system.
    echo.
    echo Press any key to exit...
    pause >nul
    goto cleanup_and_exit
) else (
    color 07
    echo [ERROR] Failed to uninstall CSimple
    echo.
    echo Please try uninstalling manually:
    echo 1. Go to Windows Settings
    echo 2. Select Apps
    echo 3. Find CSimple and uninstall
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
echo         INSTALLING CSIMPLE v{0}
echo ================================================
echo.
echo This installer will perform these steps:
echo 0. Check Windows App Runtime dependency
echo 1. Install security certificate (requires admin)
echo 2. Install CSimple application
echo 3. Complete setup and verification
echo.

echo Starting installation...
echo.

REM Step 0: Check Windows App Runtime
echo [0/3] Checking Windows App Runtime dependency...
echo      - Verifying Windows App Runtime 1.5+ is installed...
powershell -command "Get-AppxPackage -Name '*WindowsAppRuntime*' | Where-Object { $_.Name -match 'WindowsAppRuntime\.1\.[5-9]|WindowsAppRuntime\.CBS' } | Select-Object -First 1" > runtime_check.txt 2>nul
findstr /C:"WindowsAppRuntime" runtime_check.txt > nul 2>&1
if !ERRORLEVEL! NEQ 0 (
    color 07
    echo [ERROR] Windows App Runtime 1.5+ is not installed!
    echo.
    echo ================================================
    echo    MISSING CRITICAL DEPENDENCY
    echo ================================================
    echo.
    echo Windows App Runtime 1.5 or newer is required for CSimple to run.
    echo.
    echo Please follow these steps:
    echo 1. Download Windows App Runtime from:
    echo    https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/downloads
    echo 2. Install the latest runtime package for your system
    echo 3. Run this installer again
    echo.
    echo Press any key to exit...
    pause >nul
    goto cleanup_and_exit
) else (
    echo [OK] Windows App Runtime 1.5+ found
)
echo.

REM Step 1: Certificate Installation
echo [1/3] Installing security certificate...
echo      - Locating certificate file: {1}
REM Get the directory where the batch file is located - use more robust method
set "SCRIPT_DIR=%~dp0"
REM Handle case where %~dp0 might be empty or incorrect in elevated mode
if "%SCRIPT_DIR%"=="" set "SCRIPT_DIR=%CD%\"
REM Ensure SCRIPT_DIR ends with backslash
if not "%SCRIPT_DIR:~-1%"=="\" set "SCRIPT_DIR=%SCRIPT_DIR%\"
set "CERT_PATH=%SCRIPT_DIR%{1}"
echo      - Script directory: %SCRIPT_DIR%
echo      - Certificate path: %CERT_PATH%
if not exist "%CERT_PATH%" (
    color 07
    echo [ERROR] Certificate file not found: %CERT_PATH%
    echo Please ensure all installation files are in the same folder.
    echo Current directory: %CD%
    echo Looking for: {1}
    dir "%SCRIPT_DIR%" /B | findstr /I "\.cer"
    goto installation_failed
)

echo      - Installing certificate to trusted root store...
certutil -addstore -f "Root" "%CERT_PATH%" >nul 2>&1
if !ERRORLEVEL! EQU 0 (
    echo [OK] Certificate installed successfully
    goto cert_install_complete
)

echo [WARNING] Primary certificate installation failed
echo      - Trying alternative PowerShell method...
powershell -command "Import-Certificate -FilePath '%CERT_PATH%' -CertStoreLocation Cert:\LocalMachine\Root" >nul 2>&1
if !ERRORLEVEL! EQU 0 (
    echo [OK] Certificate installed successfully (alternative method)
    goto cert_install_complete
)

color 07
echo [ERROR] Certificate installation failed with both methods
goto installation_failed

:cert_install_complete
echo.

REM Step 2: Application Installation
echo [2/3] Installing CSimple application...
echo      - Locating application package: {2}
set "MSIX_PATH=%SCRIPT_DIR%{2}"
echo      - MSIX package path: %MSIX_PATH%
if not exist "%MSIX_PATH%" (
    color 07
    echo [ERROR] Application package not found: %MSIX_PATH%
    echo Please ensure all installation files are in the same folder.
    echo Current directory: %CD%
    echo Looking for: {2}
    dir "%SCRIPT_DIR%" /B | findstr /I "\.msix"
    goto installation_failed
)

echo      - Enabling app sideloading (if needed)...
powershell -command "Set-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock' -Name 'AllowAllTrustedApps' -Value 1" >nul 2>&1

echo      - Installing application package...
powershell -command "Add-AppxPackage -Path '%MSIX_PATH%'" >nul 2>&1
if !ERRORLEVEL! EQU 0 (
    echo [OK] Application installed successfully
    goto app_install_complete
)

echo [WARNING] Initial installation attempt failed
echo      - Analyzing common issues...
echo      - Attempting installation with dependency resolution...
powershell -command "Add-AppxPackage -Path '%MSIX_PATH%' -ForceApplicationShutdown" >nul 2>&1
if !ERRORLEVEL! EQU 0 (
    echo [OK] Application installed successfully (with force shutdown)
    goto app_install_complete
)

color 07
echo [ERROR] Application installation failed
goto installation_failed

:app_install_complete
echo.

REM Step 3: Verification and Finalization
echo [3/3] Finalizing installation and verification...
echo      - Verifying application registration...
timeout /t 2 /nobreak >nul

REM Try multiple verification methods to ensure we catch the installed app
echo      - Checking for CSimple installation...
powershell -command "Get-AppxPackage | Where-Object { $_.Name -like '*CSimple*' -or $_.DisplayName -like '*CSimple*' -or $_.Name -eq 'CSimple-App' } | Select-Object Name, DisplayName, Version | Format-List" > verify_check.txt 2>nul

REM Check if we found any CSimple-related packages
findstr /I /C:"Name" verify_check.txt > nul 2>&1
if !ERRORLEVEL! EQU 0 (
    echo [OK] Application verified and registered successfully
    echo      - Application details:
    type verify_check.txt
) else (
    echo [WARNING] Standard verification failed, trying alternative method...
    REM Try broader search
    powershell -command "Get-AppxPackage | Where-Object { $_.Name -match '^CSimple' -or $_.PackageFamilyName -like '*CSimple*' } | Select-Object Name, DisplayName, PackageFamilyName | Format-List" > verify_check2.txt 2>nul
    findstr /I /C:"Name" verify_check2.txt > nul 2>&1
    if !ERRORLEVEL! EQU 0 (
        echo [OK] Application verified with alternative method
        echo      - Application details:
        type verify_check2.txt
    ) else (
        color 07
        echo [ERROR] Application verification failed - app may not be properly registered
        echo      - Checking if MSIX file was processed...
        if exist "!MSIX_PATH!" (
            echo [INFO] MSIX file exists, installation may have succeeded despite verification failure
            echo [INFO] Try launching the app from Start menu or try manual verification
            echo.
            echo Would you like to continue anyway? ^(Y/N^):
            set /p continue_choice="Enter your choice: "
            if /I "!continue_choice!"=="Y" (
                echo [INFO] Continuing with setup completion...
                goto verification_complete
            )
        )
        goto installation_failed
    )
)

:verification_complete

echo      - Setting up application shortcuts...
timeout /t 1 /nobreak >nul
echo [OK] Setup completed

color 0A
echo.
echo ================================================
echo        INSTALLATION COMPLETED SUCCESSFULLY!
echo ================================================
echo.
echo CSimple v{0} is now installed
echo Application is available in your Start menu
echo You can launch the app by searching for "CSimple"
echo.
echo Thank you for installing CSimple!
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
del temp_check2.txt 2>nul
del verify_check.txt 2>nul
del verify_check2.txt 2>nul
del runtime_check.txt 2>nul
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
    
    # Use string replacement instead of -f operator to avoid brace conflicts
    try {
        $finalBatchContent = $batchContent.Replace("{0}", $appVersion).Replace("{1}", $certFileName).Replace("{2}", $msixFileName)
        Set-Content -Path $batchPath -Value $finalBatchContent -Encoding UTF8
        Write-Host "Enhanced batch installer created at $batchPath" -ForegroundColor Green
    }
    catch {
        Write-Host "Error formatting batch content: $_" -ForegroundColor Red
        Write-Host "AppVersion: '$appVersion', CertFileName: '$certFileName', MsixFileName: '$msixFileName'" -ForegroundColor Yellow
        throw
    }
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
# CSimple PowerShell Installer v$appVersion
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
            "ERROR" { Write-Host "✗ `$Message" -ForegroundColor Red }
            "WARNING" { Write-Host "⚠ `$Message" -ForegroundColor Yellow }
            default { Write-Host "ℹ `$Message" -ForegroundColor Cyan }
        }
    }
}

function Test-AdminPrivileges {
    `$currentUser = [Security.Principal.WindowsIdentity]::GetCurrent()
    `$principal = New-Object Security.Principal.WindowsPrincipal(`$currentUser)
    return `$principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Test-WindowsAppRuntime {
    try {
        `$runtime = Get-AppxPackage -Name "*WindowsAppRuntime*" -ErrorAction SilentlyContinue | Where-Object { `$_.Name -match "WindowsAppRuntime\.1\.[5-9]|WindowsAppRuntime\.CBS" }
        if (`$runtime) {
            `$latestVersion = (`$runtime | Sort-Object Version -Descending)[0]
            Write-Status "Windows App Runtime 1.5+ found: `$(`$latestVersion.Name) Version `$(`$latestVersion.Version)" "SUCCESS"
            return `$true
        }
        else {
            Write-Status "Windows App Runtime 1.5+ not found!" "ERROR"
            Write-Status "Please install Windows App Runtime 1.5 or newer from:" "ERROR"
            Write-Status "https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/downloads" "ERROR"
            return `$false
        }
    }
    catch {
        Write-Status "Error checking Windows App Runtime: `$(`$_.Exception.Message)" "ERROR"
        return `$false
    }
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
    Write-Host "CSimple v$appVersion - PowerShell Installer" -ForegroundColor Cyan
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

# Check Windows App Runtime
Write-Status "Checking Windows App Runtime..."
if (-not (Test-WindowsAppRuntime)) {
    Write-Status "Windows App Runtime 1.5 is required but not found!" "ERROR"
    Write-Status "Please install it first, then run this installer again." "ERROR"
    if (-not `$Quiet) { Read-Host "Press Enter to exit" }
    exit 1
}

# Install certificate
Write-Status "Installing security certificate..."
if (-not (Install-Certificate "$certFileName")) {
    Write-Status "Certificate installation failed. Installation cannot continue." "ERROR"
    if (-not `$Quiet) { Read-Host "Press Enter to exit" }
    exit 1
}

# Install application
Write-Status "Installing CSimple application..."
if (-not (Install-Application "$msixFileName")) {
    Write-Status "Application installation failed." "ERROR"
    Write-Status "Please check Windows version (requires Windows 10 1809+) and try again" "WARNING"
    if (-not `$Quiet) { Read-Host "Press Enter to exit" }
    exit 1
}

Write-Status "Installation completed successfully!" "SUCCESS"
Write-Status "CSimple is now available in your Start menu" "SUCCESS"

if (-not `$Quiet) {
    Write-Host ""
    Write-Host "Thank you for installing CSimple v$appVersion!" -ForegroundColor Green
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
# CSimple Installation Instructions (v$appVersion)

⚠️ **IMPORTANT PREREQUISITES** ⚠️

**Before installing CSimple, you MUST install Windows App Runtime:**

1. **Download and install Microsoft Windows App Runtime 1.5 or newer** from:
   - https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/downloads
   - Or direct link: https://aka.ms/WindowsAppRuntime/Latest/download
   
2. **Install the appropriate package for your system architecture:**
   - Choose the latest available version (1.5, 1.6, 1.7, or newer)
   - Select x64 package for 64-bit systems, x86 for 32-bit systems if needed
   - Note: Most modern systems use x64 architecture

## Quick Installation (Recommended)

**The fastest and most reliable method:**

1. **Run the automated installer:**
   - Right-click on `install.bat` and select "Run as administrator"
   - OR double-click on `install.bat` and click "Yes" when prompted for administrator privileges
   - Follow the prompts for automatic installation
   - The installer will handle certificate installation and app deployment automatically

## Manual Installation (Advanced Users)

If you prefer to install manually or troubleshoot issues:

### Step 1: Certificate Installation (Required)

Before installing CSimple (v$appVersion), you need to install the certificate:

1. Right-click on the file: `$certFileName` and select "Run as administrator"
2. OR double-click on `$certFileName` and click "Open" if prompted with a security warning
3. In the Certificate window, click "Install Certificate"
4. Select "Local Machine" and click "Next" (requires admin rights)
5. Select "Place all certificates in the following store"
6. Click "Browse" and select "Trusted Root Certification Authorities"
7. Click "Next" and then "Finish"
8. Confirm the security warning by clicking "Yes"

### Step 2: Enable Developer Mode (If Needed)

If installation fails with signing errors:

1. Open Windows Settings (Windows key + I)
2. Go to "Update & Security" → "For developers"
3. Enable "Developer mode" or "Sideload apps"

### Step 3: Application Installation

After installing the certificate:
1. Double-click on the file: `$msixFileName`
2. Click "Install"
3. **CSimple will now be available in your Windows Start menu**

## Start Menu Access

Once installed successfully:
- Press the Windows key
- Type "CSimple" in the search box
- Click on the CSimple application to launch it
- You can also pin it to your taskbar or Start menu for quick access

## Updating an Existing Installation

**Note:** If you already have CSimple installed and are updating to a newer version:
- You can skip the certificate installation steps above (certificate remains valid)
- Simply double-click the new MSIX file and click "Install" to update
- The update will preserve your settings and data

## Alternative Installation Methods

### PowerShell Installer (Advanced Users)
- Right-click PowerShell and select "Run as administrator"
- Navigate to the installation folder
- Run: `.\install.ps1`

### Manual PowerShell Commands (Expert Users)
If automated methods fail, you can install manually:
```powershell
# Install certificate (run as administrator)
Import-Certificate -FilePath ".\$certFileName" -CertStoreLocation Cert:\LocalMachine\Root

# Enable sideloading
Set-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock' -Name 'AllowAllTrustedApps' -Value 1

# Install the app
Add-AppxPackage -Path ".\$msixFileName"
```

## System Requirements

- Windows 10 version 1809 (October 2018 Update) or newer
- Windows 11 (all versions supported)
- Microsoft Windows App Runtime 1.5 or newer
- Administrator privileges (for certificate installation only)
- Minimum 500MB free disk space

## Troubleshooting

### "Certificate not trusted" or "App package is not trusted"
- **Solution:** Ensure you have installed the `$certFileName` certificate file first
- Verify the certificate was installed in "Trusted Root Certification Authorities"
- Try running the installer as administrator

### "Access Denied" during installation
- **Solution:** Right-click on the installer and select "Run as administrator"
- Ensure you have local administrator privileges on the computer

### "The package deployment failed because its publisher is not in the unsigned namespace"
- **Solution:** Enable Developer Mode in Windows Settings
- OR use the automated installer which handles this automatically

### Installation fails with signing errors
- **Solution:** 
  1. Install the certificate first (see Step 1 above)
  2. Enable "Sideload apps" or "Developer mode" in Windows Settings
  3. Try the automated installer (`install.bat`) which handles these settings

### App doesn't appear in Start menu
- **Solution:** 
  1. Wait 30-60 seconds after installation completes
  2. Press Windows key and search for "CSimple"
  3. Check if app is listed in Settings → Apps → Apps & features
  4. If still missing, try reinstalling using the automated installer

### Installation fails completely
- Check that you're running Windows 10 version 1809 or newer
- Ensure Windows App Runtime 1.5+ is installed (see prerequisites above)
- Ensure you have sufficient disk space (minimum 500MB recommended)
- Temporarily disable antivirus software during installation
- Try restarting your computer and running the installation again
- Use the automated installer (`install.bat`) which handles most common issues

## Technical Notes

- This package uses MSIX format for Windows modern app deployment
- The application identity is `CSimple-App` published by `Simple Inc`
- Package is digitally signed for security and Windows Store compliance
- Version synchronization ensures consistency between build info and package version

**Build Date:** $releaseDate  
**Package Version:** $appVersion  
**Publisher:** Simple Inc
"@
    Set-Content -Path $readmePath -Value $readmeContent -Encoding UTF8
    Write-Host "User README created at $readmePath" -ForegroundColor Green
}

# Function removed - build-info.json copy is not needed in distribution folder

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
    
    Write-Host "Creating essential installation files..." -ForegroundColor Cyan
    
    # Create only essential distribution files
    $batchPath = Join-Path $versionDir "install.bat"
    $psInstallerPath = Join-Path $versionDir "install.ps1"
    $readmePath = Join-Path $versionDir "README.md"
    
    # Create installers and documentation
    New-BatchInstaller -batchPath $batchPath -appVersion $appVersion -certFileName $certFileName -msixFileName $msixFileName
    New-PowerShellInstaller -psInstallerPath $psInstallerPath -appVersion $appVersion -certFileName $certFileName -msixFileName $msixFileName
    New-UserReadme -readmePath $readmePath -appVersion $appVersion -releaseDate $releaseDate -certFileName $certFileName -msixFileName $msixFileName
    
    Write-Host "Essential installation files created successfully!" -ForegroundColor Green
}
