# Model Training and Alignment PowerShell Script
# Usage: .\run_training.ps1 -ConfigFile "config.json" -DatasetPath "data/" -OutputPath "output/" [-ModelPath "model/"] [-ArchitectureSpec "arch.json"]

param(
    [Parameter(Mandatory = $false)]
    [string]$ConfigFile = "example_config.json",
    
    [Parameter(Mandatory = $true)]
    [string]$DatasetPath,
    
    [Parameter(Mandatory = $true)]
    [string]$OutputPath,
    
    [Parameter(Mandatory = $false)]
    [string]$ModelPath = "",
    
    [Parameter(Mandatory = $false)]
    [string]$ArchitectureSpec = ""
)

# Get script directory
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# Resolve config file path
if (-not [System.IO.Path]::IsPathRooted($ConfigFile)) {
    $ConfigFile = Join-Path $ScriptDir $ConfigFile
}

# Check if Python is available
try {
    $pythonVersion = python --version 2>&1
    Write-Host "Using Python: $pythonVersion" -ForegroundColor Green
}
catch {
    Write-Error "Python is not installed or not in PATH"
    exit 1
}

# Check if required packages are installed
Write-Host "Checking Python dependencies..." -ForegroundColor Yellow
try {
    python -c "import torch; import transformers; print('Dependencies OK')" 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw "Dependencies not found"
    }
    Write-Host "Dependencies OK" -ForegroundColor Green
}
catch {
    Write-Host "Installing requirements..." -ForegroundColor Yellow
    $requirementsFile = Join-Path $ScriptDir "training_requirements.txt"
    pip install -r $requirementsFile
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to install requirements"
        exit 1
    }
}

# Build command
$scriptPath = Join-Path $ScriptDir "model_training_alignment.py"
$command = @("python", "`"$scriptPath`"", "--config", "`"$ConfigFile`"", "--dataset_path", "`"$DatasetPath`"", "--output_path", "`"$OutputPath`"")

if ($ModelPath -ne "") {
    $command += @("--model_path", "`"$ModelPath`"")
}

if ($ArchitectureSpec -ne "") {
    $command += @("--architecture_spec", "`"$ArchitectureSpec`"")
}

# Display configuration
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Model Training and Alignment" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Config File: $ConfigFile"
Write-Host "Dataset Path: $DatasetPath"
Write-Host "Output Path: $OutputPath"
if ($ModelPath -ne "") { Write-Host "Model Path: $ModelPath" }
if ($ArchitectureSpec -ne "") { Write-Host "Architecture Spec: $ArchitectureSpec" }
Write-Host ""
Write-Host "Command: $($command -join ' ')"
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Run the training script
Write-Host "Starting training..." -ForegroundColor Green
$process = Start-Process -FilePath "python" -ArgumentList ($command[1..($command.Length - 1)]) -Wait -PassThru -NoNewWindow

# Check result
if ($process.ExitCode -eq 0) {
    Write-Host ""
    Write-Host "Training completed successfully!" -ForegroundColor Green
    Write-Host "Model saved to: $OutputPath" -ForegroundColor Green
    Write-Host "Check training.log for details" -ForegroundColor Yellow
}
else {
    Write-Host ""
    Write-Error "Training failed with exit code $($process.ExitCode)"
    Write-Host "Check training.log for details" -ForegroundColor Yellow
    exit $process.ExitCode
}
