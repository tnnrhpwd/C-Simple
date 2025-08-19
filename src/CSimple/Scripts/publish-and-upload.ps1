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
# Version: 3.0 (Now supports cross-platform builds)
# 
# ENHANCED FEATURES:
# - Cross-platform support (Windows, Linux, Android)
# - Use -LinuxOnly to build Linux packages
# - Use -AndroidOnly to build Android APK packages  
# - Use -AllPlatforms to build for all supported platforms
# - Maintains full backward compatibility for Windows-only builds

param(
    [switch]$LinuxOnly,
    [switch]$AndroidOnly,
    [switch]$AllPlatforms,
    [string]$Architecture = "x64"
)

Write-Host "CSimple Publisher - Redirecting to Cross-Platform Build System" -ForegroundColor Cyan

# Determine platforms to build
$splattingArgs = @{}
if ($LinuxOnly) {
    $splattingArgs['LinuxOnly'] = $true
    Write-Host "Building for Linux only..." -ForegroundColor Yellow
} elseif ($AndroidOnly) {
    $splattingArgs['AndroidOnly'] = $true
    Write-Host "Building for Android only..." -ForegroundColor Yellow
} elseif ($AllPlatforms) {
    $splattingArgs['AllPlatforms'] = $true
    Write-Host "Building for all platforms..." -ForegroundColor Yellow
} else {
    $splattingArgs['WindowsOnly'] = $true
    Write-Host "Building for Windows only (default)..." -ForegroundColor Yellow
}

if ($Architecture -ne "x64") {
    $splattingArgs['Architecture'] = $Architecture
}

# Call the new cross-platform script
$crossPlatformScript = Join-Path $PSScriptRoot "publish-cross-platform.ps1"

if (-not (Test-Path $crossPlatformScript)) {
    Write-Host "Cross-platform script not found at: $crossPlatformScript" -ForegroundColor Red
    Write-Host "Please ensure publish-cross-platform.ps1 is in the same directory." -ForegroundColor Red
    exit 1
}

Write-Host "Executing cross-platform build script..." -ForegroundColor Green

# Execute with properly splatted arguments
& $crossPlatformScript @splattingArgs

$exitCode = $LASTEXITCODE
Write-Host "`nBuild completed with exit code: $exitCode" -ForegroundColor $(if ($exitCode -eq 0) { "Green" } else { "Red" })
exit $exitCode

