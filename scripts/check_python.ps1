param (
    [string]$OutputDir
)

Write-Host "Checking for Python in build environment..."

# Check if Python is installed
$pythonInstalled = $false
try {
    $pythonVersion = python --version 2>&1
    if ($pythonVersion -match "Python 3") {
        Write-Host "Python 3 found: $pythonVersion"
        $pythonInstalled = $true
    }
    else {
        Write-Host "Python found but not version 3: $pythonVersion"
    }
}
catch {
    Write-Host "Python not found in PATH"
}

if (-not $pythonInstalled) {
    Write-Host "Python 3 not found, you may need to install it for full application functionality."
    Write-Host "The application will attempt to install Python at runtime if needed."
    exit 0 # Don't fail the build
}

# Create scripts directory in output if it doesn't exist
$scriptsOutputDir = Join-Path $OutputDir "Scripts"
if (-not (Test-Path $scriptsOutputDir)) {
    New-Item -ItemType Directory -Path $scriptsOutputDir | Out-Null
    Write-Host "Created Scripts directory at: $scriptsOutputDir"
}

# Output a helpful message
Write-Host "Build completed successfully with Python support"
exit 0
