# Simple PowerShell syntax test
param(
    [Parameter(Mandatory=$false)]
    [ValidateSet("all", "copilot", "build", "integration", "unit")]
    [string]$TestCategory = "copilot"
)

Write-Host "🤖 Testing GitHub Copilot Agent Tests" -ForegroundColor Green

# Get the test project directory
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$testProjectPath = Split-Path -Parent $scriptPath
$workspaceRoot = Split-Path -Parent (Split-Path -Parent $testProjectPath)

Write-Host "📁 Workspace: $workspaceRoot" -ForegroundColor Cyan
Write-Host "🧪 Test Project: $testProjectPath" -ForegroundColor Cyan

# Run the test
Set-Location $testProjectPath

$args = @("test", $testProjectPath, "--filter", "TestCategory=$TestCategory", "--configuration", "Release", "--logger", "console;verbosity=normal")

Write-Host "🔍 Running dotnet test with args: $($args -join ' ')" -ForegroundColor Yellow

$process = Start-Process -FilePath "dotnet" -ArgumentList $args -Wait -PassThru -NoNewWindow

if ($process.ExitCode -eq 0) {
    Write-Host "✅ Tests PASSED" -ForegroundColor Green
} else {
    Write-Host "❌ Tests FAILED" -ForegroundColor Red
}

Set-Location $workspaceRoot
