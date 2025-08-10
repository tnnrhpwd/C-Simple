# Test Runner Script for C-Simple

Write-Host "C-Simple Test Runner" -ForegroundColor Cyan
Write-Host "===================" -ForegroundColor Cyan
Write-Host ""

# Get the current script directory and navigate to the test project
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$testProjectPath = Split-Path -Parent $scriptPath
Set-Location $testProjectPath

Write-Host "📁 Test project path: $testProjectPath" -ForegroundColor Blue
Write-Host ""

Write-Host "Running Build Verification Tests..." -ForegroundColor Yellow
dotnet test --filter "FullyQualifiedName~SimpleBuildTests" --logger "console;verbosity=minimal"

if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ Build verification tests PASSED" -ForegroundColor Green
} else {
    Write-Host "❌ Build verification tests FAILED" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Running Unit Tests..." -ForegroundColor Yellow
dotnet test --filter "TestCategory=Unit" --logger "console;verbosity=minimal"

if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ Unit tests PASSED" -ForegroundColor Green
} else {
    Write-Host "❌ Unit tests FAILED" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "🎉 All tests completed successfully!" -ForegroundColor Green
Write-Host ""
Write-Host "Test Summary:" -ForegroundColor Cyan
Write-Host "- Build Verification: Tests that the project builds correctly" -ForegroundColor White
Write-Host "- Unit Tests: Tests basic functionality and logic" -ForegroundColor White
Write-Host ""
Write-Host "To run tests in VS Code:" -ForegroundColor Cyan
Write-Host "1. Open the Test Explorer (Testing icon in sidebar)" -ForegroundColor White
Write-Host "2. Click the play button to run tests" -ForegroundColor White
Write-Host "3. Use the filter options to run specific test categories" -ForegroundColor White
