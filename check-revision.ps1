#!/usr/bin/env powershell
# Check Current Build Info Script

$buildInfoFile = "build-info.json"

if (Test-Path $buildInfoFile) {
    try {
        $buildInfo = Get-Content $buildInfoFile -Raw | ConvertFrom-Json
        Write-Host "Current Build Info:" -ForegroundColor Green
        Write-Host "  Version: $($buildInfo.version)" -ForegroundColor White
        Write-Host "  Revision: #$($buildInfo.revision)" -ForegroundColor White
        Write-Host "  Build Count: $($buildInfo.buildCount)" -ForegroundColor White
        Write-Host "  Last Build: $($buildInfo.lastBuild)" -ForegroundColor White
    }
    catch {
        Write-Host "Error reading build info file: $_" -ForegroundColor Red
        
        # Fallback to legacy files
        if (Test-Path "revision.txt") {
            $currentRevision = Get-Content "revision.txt"
            Write-Host "Current Revision (legacy): #$currentRevision" -ForegroundColor Yellow
        }
    }
} else {
    Write-Host "Build info file not found. No builds have been created yet." -ForegroundColor Yellow
    Write-Host "Run the publish script to create your first build." -ForegroundColor Yellow
    
    # Check for legacy files
    if (Test-Path "revision.txt") {
        $currentRevision = Get-Content "revision.txt"
        Write-Host "Found legacy revision file: #$currentRevision" -ForegroundColor Yellow
    }
}

# Show latest build info if available
$publishedDir = "D:\My Drive\Simple\current"
if (Test-Path $publishedDir) {
    Write-Host ""
    Write-Host "Latest Published Build:" -ForegroundColor Cyan
    
    if (Test-Path (Join-Path $publishedDir "build-info.json")) {
        try {
            $publishedBuildInfo = Get-Content (Join-Path $publishedDir "build-info.json") -Raw | ConvertFrom-Json
            Write-Host "  Version: $($publishedBuildInfo.version)" -ForegroundColor White
            Write-Host "  Revision: #$($publishedBuildInfo.revision)" -ForegroundColor White
            Write-Host "  Published: $($publishedBuildInfo.lastBuild)" -ForegroundColor White
        }
        catch {
            # Fallback to legacy files
            if (Test-Path (Join-Path $publishedDir "version.txt")) {
                $version = Get-Content (Join-Path $publishedDir "version.txt")
                Write-Host "  Version: $version" -ForegroundColor White
            }
            
            if (Test-Path (Join-Path $publishedDir "revision.txt")) {
                $publishedRevision = Get-Content (Join-Path $publishedDir "revision.txt")
                Write-Host "  Revision: #$publishedRevision" -ForegroundColor White
            }
        }
    }
    
    # Show file sizes
    $msixFiles = Get-ChildItem -Path $publishedDir -Filter "*.msix"
    foreach ($file in $msixFiles) {
        $sizeMB = [math]::Round($file.Length / 1MB, 2)
        Write-Host "  File: $($file.Name) ($sizeMB MB)" -ForegroundColor White
    }
} else {
    Write-Host ""
    Write-Host "No published builds found." -ForegroundColor Yellow
}
