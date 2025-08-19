# Cross-Platform Packaging Service
# This service handles platform-specific packaging logic for CSimple

# Function to create Windows-specific installation documentation
function New-WindowsInstallationDocumentation {
    param (
        [string]$platformDir,
        [string]$appVersion,
        [string]$certFileName,
        [string]$msixFileName
    )
    
    # Create Windows-specific README
    $readmeContent = @"
# CSimple for Windows (v$appVersion)

## Installation

### Quick Installation (Recommended)
1. Run `install.bat` as administrator
2. Follow the prompts

### Manual Installation
1. Install the certificate: `$certFileName`
2. Install the application: `$msixFileName`

## System Requirements
- Windows 10 version 1809 or newer
- Windows 11 (all versions)
- Administrator privileges (for certificate installation)

## Files Included
- `$msixFileName` - Main application package
- `$certFileName` - Security certificate
- `install.bat` - Automated installer
- `install.ps1` - PowerShell installer
- `README.md` - This file

For detailed installation instructions, see the main README.md file.
"@
    
    Set-Content -Path (Join-Path $platformDir "README.md") -Value $readmeContent -Encoding UTF8
    Write-Host "Windows documentation created" -ForegroundColor Green
}

# Function to create Linux-specific installation documentation
function New-LinuxInstallationDocumentation {
    param (
        [string]$platformDir,
        [string]$appVersion,
        [string]$archiveFileName
    )
    
    # Create Linux-specific README
    $readmeContent = @"
# CSimple for Linux (v$appVersion)

## Installation

### Quick Installation
\`\`\`bash
# Extract the application
tar -xzf $archiveFileName

# Make executable (if needed)
chmod +x csimple/CSimple
chmod +x csimple.sh

# Run the application
./csimple.sh
\`\`\`

### System Installation (Optional)
\`\`\`bash
# Copy to system location
sudo cp -r csimple /opt/
sudo ln -s /opt/csimple/CSimple /usr/local/bin/csimple

# Or create a desktop entry
sudo cp csimple.desktop /usr/share/applications/
\`\`\`

## System Requirements
- Linux x64 (most distributions)
- .NET 8.0 runtime (self-contained, included)
- X11 or Wayland display server
- Minimum 100MB disk space

## Running
\`\`\`bash
# From extraction directory
./csimple.sh

# If installed system-wide
csimple
\`\`\`

## Files Included
- `$archiveFileName` - Application archive
- `csimple/` - Application directory (after extraction)
- `csimple.sh` - Launch script
- `README.md` - This file

## Troubleshooting
- Ensure the executable has run permissions: \`chmod +x csimple/CSimple\`
- For dependency issues, install .NET 8.0 runtime from Microsoft
- For display issues, ensure X11/Wayland is properly configured

## Uninstallation
Simply delete the extracted directory or remove from /opt/ if installed system-wide.
"@
    
    Set-Content -Path (Join-Path $platformDir "README.md") -Value $readmeContent -Encoding UTF8
    
    # Create desktop entry file
    $desktopEntry = @"
[Desktop Entry]
Version=1.0
Type=Application
Name=CSimple
Comment=Streamlined productivity and automation tools
Exec=/opt/csimple/CSimple
Icon=/opt/csimple/Resources/appicon.png
Terminal=false
Categories=Productivity;Office;Utility;
"@
    
    Set-Content -Path (Join-Path $platformDir "csimple.desktop") -Value $desktopEntry -Encoding UTF8
    
    Write-Host "Linux documentation and desktop entry created" -ForegroundColor Green
}

# Function to create Android-specific installation documentation
function New-AndroidInstallationDocumentation {
    param (
        [string]$platformDir,
        [string]$appVersion,
        [string]$apkFileName
    )
    
    # Create Android-specific README
    $readmeContent = @"
# CSimple for Android (v$appVersion)

## Installation

### Direct Installation
1. Enable "Unknown sources" in Android Settings > Security
2. Download and install: `$apkFileName`
3. Grant necessary permissions when prompted

### ADB Installation (Developer/Testing)
\`\`\`bash
# Install via ADB
adb install $apkFileName

# Uninstall (if needed)
adb uninstall com.csimple.app
\`\`\`

## System Requirements
- Android 7.0 (API level 24) or higher
- ARM64 or x86_64 architecture
- Minimum 50MB free storage space
- Internet connection for initial setup

## Permissions
The app may request the following permissions:
- **Storage Access** - For file operations and data storage
- **Network Access** - For synchronization and updates
- **Camera** (Optional) - For QR code scanning features
- **Notifications** - For background task notifications

## First Launch
1. Open CSimple from your app drawer
2. Complete the initial setup wizard
3. Configure your preferences
4. Start using the app!

## Files Included
- `$apkFileName` - Android application package
- `README.md` - This installation guide

## Troubleshooting

### Installation Issues
- **"App not installed"**: Enable "Unknown sources" in Settings
- **"Parse error"**: Download the APK again, file may be corrupted
- **"Insufficient storage"**: Clear space and try again

### Runtime Issues
- **App crashes on startup**: Clear app data in Settings > Apps > CSimple
- **Slow performance**: Restart the app or reboot device
- **Sync issues**: Check internet connection and app permissions

### Compatibility
- Minimum: Android 7.0 (API 24)
- Recommended: Android 10+ for best experience
- Architecture: ARM64 (most modern devices) or x86_64

## Uninstallation
1. Go to Settings > Apps > CSimple
2. Tap "Uninstall"
3. Confirm removal

Or use ADB:
\`\`\`bash
adb uninstall com.csimple.app
\`\`\`

## Developer Notes
- Package Name: com.csimple.app
- Target SDK: Android 14 (API 34)
- Min SDK: Android 7.0 (API 24)
- Signed with release keystore
"@
    
    Set-Content -Path (Join-Path $platformDir "README.md") -Value $readmeContent -Encoding UTF8
    Write-Host "Android documentation created" -ForegroundColor Green
}

# Function to create cross-platform documentation
function New-CrossPlatformDocumentation {
    param (
        [string]$versionDir,
        [string]$appVersion,
        [string]$releaseDate,
        [string[]]$platforms
    )
    
    $readmeContent = @"
# CSimple v$appVersion - Multi-Platform Release

**Release Date:** $releaseDate  
**Available Platforms:** $($platforms -join ', ')

## Platform-Specific Instructions

"@
    
    foreach ($platform in $platforms) {
        $platformDir = Join-Path $versionDir $platform
        if (Test-Path $platformDir) {
            $readmeContent += @"

### $($platform.ToUpper())
See the `$platform/README.md` file for platform-specific installation instructions.

"@
        }
    }
    
    $readmeContent += @"

## What's New in v$appVersion
- Cross-platform support added
- Improved build system
- Enhanced packaging for each platform

## System Requirements by Platform

### Windows
- Windows 10 version 1809+ or Windows 11
- Windows App Runtime 1.5+
- Administrator privileges (for certificate installation)

### Linux  
- Most modern Linux distributions (Ubuntu 18.04+, CentOS 8+, etc.)
- X11 or Wayland display server
- Self-contained .NET runtime included

### Android
- Android 7.0 (API level 24) or higher
- ARM64 or x86_64 architecture
- Minimum 50MB storage space

## Getting Started
1. Choose your platform directory
2. Follow the README.md instructions in that directory
3. Run the appropriate installer or extract the archive

## Support
For issues specific to a platform, check the troubleshooting section in the platform's README.md file.

## Technical Details
- Built with .NET 8.0 and .NET MAUI
- Cross-platform UI framework
- Self-contained deployments (no additional runtime installation required*)

*Windows may require Windows App Runtime dependency
"@
    
    Set-Content -Path (Join-Path $versionDir "README.md") -Value $readmeContent -Encoding UTF8
    Write-Host "Cross-platform documentation created" -ForegroundColor Green
}
