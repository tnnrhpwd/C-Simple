# VS Code C# Dev Kit Test Explorer Troubleshooting Guide

This document provides solutions for common issues when running tests through the VS Code C# Dev Kit Test Explorer.

## Overview

The C-Simple project includes comprehensive tests organized into multiple categories:
- **Unit Tests**: 8 tests - Core functionality validation
- **Build Tests**: 21 tests - Build and compilation verification  
- **Integration Tests**: 7 tests - Project integration scenarios
- **CopilotAgent Tests**: 8 tests - GitHub Copilot agent readiness

## Common Issues and Solutions

### 1. Path Resolution Issues

**Problem**: Tests fail in VS Code Test Explorer but pass when run via CLI
**Root Cause**: Different working directory context between test explorer and CLI execution

**Solution**: 
- Tests have been updated with robust path resolution using assembly location
- Multiple fallback paths ensure compatibility across execution contexts
- Diagnostic tests available to troubleshoot path issues

### 2. Test Discovery Problems

**Problem**: Tests not appearing in VS Code Test Explorer
**Solutions**:
1. Reload VS Code window (`Ctrl+Shift+P` → "Developer: Reload Window")
2. Rebuild the test project: `Ctrl+Shift+P` → "Test: Reset and Reload All Test Data"
3. Check VS Code settings configuration

### 3. Performance Issues

**Problem**: Tests take too long or timeout
**Solutions**:
- Test timeout increased to 60 seconds in VS Code settings
- Use category-based test execution for faster feedback
- Run specific test classes rather than all tests

### 4. Build Integration Tests Failing

**Problem**: dotnet build commands fail in test explorer context
**Troubleshooting Steps**:
1. Run diagnostic tests to check path resolution:
   ```bash
   dotnet test --filter "FullyQualifiedName~PathResolution_DiagnosticInfo"
   ```
2. Verify project paths exist and are accessible
3. Check that dotnet CLI is available in the test execution environment

## VS Code Configuration

### Required Extensions
- C# Dev Kit
- C# (powered by OmniSharp)
- .NET Install Tool for Extension Authors

### Optimal Settings
Key settings in `.vscode/settings.json`:
```json
{
  "dotnet.defaultSolution": "src/CSimple.sln",
  "dotnet.server.useOmnisharp": false,
  "testExplorer.useNativeTesting": true,
  "dotnet.unitTests.runSettingsPath": "src/CSimple.Tests/test.runsettings",
  "dotnet.test.defaultTimeout": 60000,
  "dotnet.test.enableTestDiscovery": true,
  "dotnet.test.enableCodeLens": true,
  "testing.saveBeforeTest": true
}
```

## Test Execution Options

### 1. CLI Execution (Most Reliable)
```bash
# All tests
dotnet test

# Specific category
dotnet test --filter "TestCategory=Build"
dotnet test --filter "TestCategory=Integration"
dotnet test --filter "TestCategory=CopilotAgent"

# Specific test class
dotnet test --filter "FullyQualifiedName~BuildVerificationTests"
```

### 2. VS Code Tasks
Use predefined tasks in Command Palette:
- "Test C-Simple Application"
- "Run GitHub Copilot Agent Tests"
- "Run All Test Categories"

### 3. PowerShell Test Runner
```powershell
# All tests with coverage
.\src\CSimple.Tests\TestUtilities\Invoke-CopilotAgentTests.ps1 -TestCategory all -Coverage

# Specific category
.\src\CSimple.Tests\TestUtilities\Invoke-CopilotAgentTests.ps1 -TestCategory build
```

## Diagnostic Commands

### Path Resolution Debugging
```bash
dotnet test --filter "TestCategory=Diagnostic" --logger console --verbosity detailed
```

### Test Discovery
```bash
dotnet test --list-tests
```

### Build Verification
```bash
# Verify project builds correctly
dotnet build src/CSimple/CSimple.csproj
dotnet build src/CSimple.Tests/CSimple.Tests.csproj
```

## GitHub Actions Integration

Tests are automatically executed in CI/CD pipeline with:
- Matrix testing across multiple configurations
- Test result artifacts
- Code coverage reports
- Parallel execution for performance

## Troubleshooting Checklist

- [ ] VS Code extensions installed and updated
- [ ] .NET SDK 8.0 or later installed
- [ ] Project builds successfully via CLI
- [ ] Test project references are correct
- [ ] VS Code workspace opened at repository root
- [ ] Test discovery enabled in settings
- [ ] Recent VS Code window reload performed

## Getting Help

1. Run diagnostic tests first to gather context
2. Check the Test Output panel in VS Code for detailed error messages
3. Compare CLI test results with VS Code test explorer results
4. Review recent changes to test files or configuration

## Performance Tips

- Use test categories to run subset of tests during development
- Enable parallel test execution in `test.runsettings`
- Consider using `dotnet watch test` for continuous testing during development
- Use background task execution for long-running test suites
