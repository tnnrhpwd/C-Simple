# VS Code C# Dev Kit Test Explorer Resolution Guide

## Current Status
- ✅ All tests pass via `dotnet test` CLI (47 tests total)
- ✅ Project and solution paths are correctly resolved
- ✅ .NET CLI is functional (version 9.0.300)
- ❌ Tests may be failing in VS Code C# Dev Kit Test Explorer

## Quick Resolution Steps

### 1. Immediate Actions
1. **Reload VS Code Window**: `Ctrl+Shift+P` → "Developer: Reload Window"
2. **Clear Test Cache**: `Ctrl+Shift+P` → "Test: Reset and Reload All Test Data"
3. **Rebuild Solution**: `Ctrl+Shift+P` → "Tasks: Run Task" → "Build C-Simple Application"

### 2. Verify Configuration
Check that VS Code is using the correct settings:
- Solution path: `src/CSimple.sln`
- RunSettings: `src/CSimple.Tests/test.simple.runsettings` (simplified version)
- Test timeout: 120 seconds

### 3. Run Diagnostic Tests
Use the Command Palette (`Ctrl+Shift+P`) → "Tasks: Run Task" → "Run Diagnostic Tests"

This will output detailed environment information to help troubleshoot.

### 4. Alternative Test Execution Methods

If VS Code Test Explorer continues to fail, use these alternatives:

**Option A: VS Code Tasks**
- `Ctrl+Shift+P` → "Tasks: Run Task" → "Test C-Simple Application"
- `Ctrl+Shift+P` → "Tasks: Run Task" → "Test DotNetBuildIntegrationTests Only"

**Option B: Integrated Terminal**
```bash
cd src/CSimple.Tests
dotnet test --filter "FullyQualifiedName~DotNetBuildIntegrationTests"
```

**Option C: PowerShell Test Runner**
```bash
Ctrl+Shift+P → "Tasks: Run Task" → "Run GitHub Copilot Agent Tests"
```

## Troubleshooting Common Issues

### Issue 1: Tests Not Discovered
**Symptoms**: Tests don't appear in VS Code Test Explorer
**Solutions**:
1. Ensure C# Dev Kit extension is installed and enabled
2. Check that `dotnet.defaultSolution` points to correct solution
3. Verify test project builds successfully
4. Restart VS Code completely

### Issue 2: Tests Timeout
**Symptoms**: Tests start but never complete
**Solutions**:
1. Increase timeout in settings: `dotnet.test.defaultTimeout`
2. Use simplified runsettings: `test.simple.runsettings`
3. Disable parallel execution temporarily

### Issue 3: Path Resolution Errors
**Symptoms**: Tests fail with "file not found" or path errors
**Solutions**:
1. All tests now use assembly-based path resolution
2. Multiple fallback paths implemented
3. Run diagnostic tests to verify paths

### Issue 4: Process/Permission Issues
**Symptoms**: Tests fail with access denied or process errors
**Solutions**:
1. Run VS Code as administrator (temporarily)
2. Check antivirus software blocking test execution
3. Verify .NET SDK permissions

## Configuration Files Summary

### VS Code Settings (`.vscode/settings.json`)
- Uses simple runsettings for better compatibility
- Increased timeouts and enhanced discovery
- Disabled some advanced features that might interfere

### Test RunSettings (`test.simple.runsettings`)
- Simplified configuration for VS Code compatibility
- Shorter timeouts, basic parallelization
- Minimal data collection to avoid conflicts

### Tasks (`tasks.json`)
- Multiple test execution options
- Diagnostic test runner
- Direct test class execution

## Verification Commands

Run these in VS Code terminal to verify functionality:

```bash
# Verify build
dotnet build src/CSimple.Tests

# List all tests
dotnet test src/CSimple.Tests --list-tests

# Run all tests
dotnet test src/CSimple.Tests

# Run specific failing tests
dotnet test src/CSimple.Tests --filter "FullyQualifiedName~DotNetBuildIntegrationTests"

# Run with simple settings
dotnet test src/CSimple.Tests --settings src/CSimple.Tests/test.simple.runsettings
```

## Known Working Configurations

✅ **CLI Execution**: All 47 tests pass consistently
✅ **PowerShell Runner**: Custom test categorization works
✅ **GitHub Actions**: CI/CD pipeline executes tests successfully
✅ **VS Code Tasks**: Task-based test execution works

## Contact Information

If issues persist after trying these solutions:
1. Check VS Code Extensions panel for C# Dev Kit updates
2. Review VS Code "Output" panel for detailed error messages
3. Try running tests with the diagnostic test task first
4. Consider temporarily switching to OmniSharp if C# Dev Kit continues to have issues

## Emergency Fallback

If VS Code Test Explorer remains problematic, you can switch back to OmniSharp temporarily by changing this setting:

```json
"dotnet.server.useOmnisharp": true
```

Then reload VS Code window.
