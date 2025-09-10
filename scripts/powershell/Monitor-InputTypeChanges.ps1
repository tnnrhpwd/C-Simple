# Monitor-InputTypeChanges.ps1
# PowerShell script to monitor InputType changes in huggingFaceModels.json

# Get the user's Documents folder dynamically and construct the path
$documentsPath = [Environment]::GetFolderPath("MyDocuments")
$defaultBasePath = Join-Path $documentsPath "CSimple"

# Try to read the app's base path preference if available
# This would require reading from MAUI preferences, but for simplicity we'll use the default
# In a more advanced version, this could read from registry or app config

# Function to try reading the app's configured base path
function Get-AppBasePath {
    try {
        # MAUI preferences are typically stored in the registry under HKCU for Windows
        # The exact location depends on the app's package identity
        # For now, we'll use the default path, but this could be enhanced
        
        # Example of how to read from registry if the app stores preferences there:
        # $regPath = "HKCU:\Software\CSimple"
        # if (Test-Path $regPath) {
        #     $configuredPath = Get-ItemProperty -Path $regPath -Name "AppBasePath" -ErrorAction SilentlyContinue
        #     if ($configuredPath -and $configuredPath.AppBasePath) {
        #         return $configuredPath.AppBasePath
        #     }
        # }
        
        return $null
    }
    catch {
        return $null
    }
}

# Try to get configured path, fall back to default
$configuredPath = Get-AppBasePath
if ($configuredPath -and (Test-Path $configuredPath)) {
    $basePath = $configuredPath
    Write-Host "✅ Found configured base path: $basePath" -ForegroundColor Green
} else {
    $basePath = $defaultBasePath
    Write-Host "📂 Using default base path: $basePath" -ForegroundColor Yellow
}

$jsonPath = Join-Path $basePath "Resources\huggingFaceModels.json"

Write-Host "🔧 Using base path: $basePath" -ForegroundColor Cyan
Write-Host "📁 Monitoring file: $jsonPath" -ForegroundColor Cyan
Write-Host ""

Write-Host "🔍 Monitoring InputType changes in: $jsonPath" -ForegroundColor Green
Write-Host "📝 Press Ctrl+C to stop monitoring" -ForegroundColor Yellow
Write-Host ""

# Function to read and display current InputType values
function Show-CurrentInputTypes {
    if (Test-Path $jsonPath) {
        try {
            $models = Get-Content $jsonPath | ConvertFrom-Json
            Write-Host "📊 Current InputType values:" -ForegroundColor Cyan
            foreach ($model in $models) {
                $name = $model.Name
                $inputType = $model.InputType
                Write-Host "  • $name : $inputType" -ForegroundColor White
            }
            Write-Host ""
        }
        catch {
            Write-Host "❌ Error reading JSON: $($_.Exception.Message)" -ForegroundColor Red
        }
    }
    else {
        Write-Host "❌ File not found: $jsonPath" -ForegroundColor Red
    }
}

# Show initial state
Write-Host "🚀 Initial state:" -ForegroundColor Green
Show-CurrentInputTypes

# Monitor for file changes
$watcher = New-Object System.IO.FileSystemWatcher
$watcher.Path = [System.IO.Path]::GetDirectoryName($jsonPath)
$watcher.Filter = [System.IO.Path]::GetFileName($jsonPath)
$watcher.NotifyFilter = [System.IO.NotifyFilters]::LastWrite

# Event handler for file changes
$action = {
    $changeTime = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    Write-Host "🔔 File changed at $changeTime" -ForegroundColor Yellow
    Start-Sleep -Milliseconds 100  # Wait a bit for file to be fully written
    Show-CurrentInputTypes
}

# Register the event
Register-ObjectEvent -InputObject $watcher -EventName Changed -Action $action

# Start monitoring
$watcher.EnableRaisingEvents = $true

try {
    Write-Host "🎯 Now change a model's InputType in the NetPage of your app..." -ForegroundColor Magenta
    Write-Host "🔄 Monitoring for changes... (Press Ctrl+C to stop)" -ForegroundColor Green
    
    # Keep the script running
    while ($true) {
        Start-Sleep -Seconds 1
    }
}
finally {
    # Cleanup
    $watcher.EnableRaisingEvents = $false
    $watcher.Dispose()
    Write-Host "🛑 Monitoring stopped." -ForegroundColor Red
}
