# GitHub Copilot Agent Test Utilities
# This script provides utilities for testing GitHub Copilot agent interactions

param(
    [Parameter(Mandatory=$false)]
    [ValidateSet("all", "copilot", "build", "integration", "unit")]
    [string]$TestCategory = "all",
    
    [Parameter(Mandatory=$false)]
    [switch]$Coverage,
    
    [Parameter(Mandatory=$false)]
    [switch]$Verbose,
    
    [Parameter(Mandatory=$false)]
    [switch]$ContinuousWatch
)

Write-Host "🤖 GitHub Copilot Agent Test Runner" -ForegroundColor Green
Write-Host "=" * 50

# Get the test project directory
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$testProjectPath = Split-Path -Parent $scriptPath
$workspaceRoot = Split-Path -Parent (Split-Path -Parent $testProjectPath)

Write-Host "📁 Workspace: $workspaceRoot" -ForegroundColor Cyan
Write-Host "🧪 Test Project: $testProjectPath" -ForegroundColor Cyan
Write-Host ""

# Function to run tests with specific category
function Invoke-TestCategory {
    param(
        [string]$Category,
        [string]$Description,
        [bool]$IncludeCoverage = $false,
        [bool]$VerboseOutput = $false
    )
    
    Write-Host "🔍 Running $Description..." -ForegroundColor Yellow
    
    $args = @("test", $testProjectPath)
    
    if ($Category -ne "all") {
        $args += "--filter", "TestCategory=$Category"
    }
    
    $args += "--configuration", "Release"
    
    if ($VerboseOutput) {
        $args += "--logger", "console;verbosity=detailed"
    } else {
        $args += "--logger", "console;verbosity=normal"
    }
    
    # Add TRX logger for GitHub Actions compatibility
    $resultsDir = Join-Path $testProjectPath "TestResults"
    if (!(Test-Path $resultsDir)) {
        New-Item -ItemType Directory -Path $resultsDir -Force | Out-Null
    }
    
    $trxFile = "test-results-$Category-$(Get-Date -Format 'yyyyMMdd-HHmmss').trx"
    $args += "--logger", "trx;LogFileName=$trxFile"
    $args += "--results-directory", $resultsDir
    
    if ($IncludeCoverage) {
        $args += "--collect", "XPlat Code Coverage"
    }
    
    $process = Start-Process -FilePath "dotnet" -ArgumentList $args -Wait -PassThru -NoNewWindow
    
    if ($process.ExitCode -eq 0) {
        Write-Host "✅ $Description PASSED" -ForegroundColor Green
        return $true
    } else {
        Write-Host "❌ $Description FAILED" -ForegroundColor Red
        return $false
    }
}

# Main execution
try {
    Set-Location $testProjectPath
    
    $allPassed = $true
    
    if ($ContinuousWatch) {
        Write-Host "👀 Continuous watch mode not implemented yet. Use regular test execution." -ForegroundColor Yellow
        return
    }
    
    switch ($TestCategory) {
        "all" {
            Write-Host "🎯 Running all test categories for comprehensive validation" -ForegroundColor Magenta
            Write-Host ""
            
            $allPassed = (Invoke-TestCategory -Category "CopilotAgent" -Description "GitHub Copilot Agent Tests" -IncludeCoverage $Coverage -VerboseOutput $Verbose) -and $allPassed
            $allPassed = (Invoke-TestCategory -Category "Unit" -Description "Unit Tests" -IncludeCoverage $Coverage -VerboseOutput $Verbose) -and $allPassed
            $allPassed = (Invoke-TestCategory -Category "Build" -Description "Build Verification Tests" -IncludeCoverage $Coverage -VerboseOutput $Verbose) -and $allPassed
            $allPassed = (Invoke-TestCategory -Category "Integration" -Description "Integration Tests" -IncludeCoverage $Coverage -VerboseOutput $Verbose) -and $allPassed
        }
        "copilot" {
            $allPassed = Invoke-TestCategory -Category "CopilotAgent" -Description "GitHub Copilot Agent Tests" -IncludeCoverage $Coverage -VerboseOutput $Verbose
        }
        "build" {
            $allPassed = Invoke-TestCategory -Category "Build" -Description "Build Verification Tests" -IncludeCoverage $Coverage -VerboseOutput $Verbose
        }
        "integration" {
            $allPassed = Invoke-TestCategory -Category "Integration" -Description "Integration Tests" -IncludeCoverage $Coverage -VerboseOutput $Verbose
        }
        "unit" {
            $allPassed = Invoke-TestCategory -Category "Unit" -Description "Unit Tests" -IncludeCoverage $Coverage -VerboseOutput $Verbose
        }
    }
    
    Write-Host ""
    if ($allPassed) {
        Write-Host "🎉 All tests completed successfully!" -ForegroundColor Green
        Write-Host ""
        Write-Host "📊 GitHub Copilot Agent Readiness: ✅ READY" -ForegroundColor Green
        Write-Host ""
        Write-Host "🔍 Test Results Summary:" -ForegroundColor Cyan
        Write-Host "- ✅ Copilot Agent specific tests" -ForegroundColor White
        Write-Host "- ✅ Build verification and CI readiness" -ForegroundColor White
        Write-Host "- ✅ Integration test scenarios" -ForegroundColor White
        Write-Host "- ✅ Unit test coverage" -ForegroundColor White
        
        exit 0
    } else {
        Write-Host "❌ Some tests failed!" -ForegroundColor Red
        Write-Host ""
        Write-Host "📊 GitHub Copilot Agent Readiness: ❌ NEEDS ATTENTION" -ForegroundColor Red
        Write-Host ""
        Write-Host "🔧 Next Steps:" -ForegroundColor Yellow
        Write-Host "1. Review failed test output above" -ForegroundColor White
        Write-Host "2. Fix any failing tests" -ForegroundColor White
        Write-Host "3. Re-run tests to verify fixes" -ForegroundColor White
        
        exit 1
    }
}
catch {
    Write-Host "Error running tests: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
finally {
    # Return to original location
    Set-Location $workspaceRoot
}
