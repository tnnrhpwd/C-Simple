# Test-DynamicPath.ps1
# Simple test script to verify the dynamic path construction works

Write-Host "🧪 Testing Dynamic Path Construction" -ForegroundColor Green
Write-Host "=" * 40

# Test the same logic used in Monitor-InputTypeChanges.ps1
$documentsPath = [Environment]::GetFolderPath("MyDocuments")
$defaultBasePath = Join-Path $documentsPath "CSimple"
$jsonPath = Join-Path $defaultBasePath "Resources\huggingFaceModels.json"

Write-Host "📁 Documents folder: $documentsPath" -ForegroundColor Cyan
Write-Host "🏠 Default base path: $defaultBasePath" -ForegroundColor Cyan  
Write-Host "📄 Target JSON file: $jsonPath" -ForegroundColor Cyan
Write-Host ""

# Check if paths exist
Write-Host "✅ Path Validation:" -ForegroundColor Yellow
Write-Host "   Documents exists: $(Test-Path $documentsPath)" -ForegroundColor White
Write-Host "   Base path exists: $(Test-Path $defaultBasePath)" -ForegroundColor White
Write-Host "   JSON file exists: $(Test-Path $jsonPath)" -ForegroundColor White
Write-Host ""

# Show old vs new approach
$oldPath = "c:\Users\tanne\Documents\CSimple\Resources\huggingFaceModels.json"
Write-Host "🔄 Comparison:" -ForegroundColor Magenta
Write-Host "   Old (hardcoded): $oldPath" -ForegroundColor Red
Write-Host "   New (dynamic):   $jsonPath" -ForegroundColor Green

if ($oldPath -eq $jsonPath) {
    Write-Host "   ✅ Paths match! The dynamic approach works correctly." -ForegroundColor Green
} else {
    Write-Host "   ⚠️ Paths differ - this is expected if running on a different user account." -ForegroundColor Yellow
}
