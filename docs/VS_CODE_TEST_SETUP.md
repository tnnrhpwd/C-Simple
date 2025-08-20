# VS Code Test Explorer Setup Guide

## Current Status ✅
- ✅ Test project is built and ready (`CSimple.Tests`)
- ✅ 34 tests are discoverable via `dotnet test --list-tests`
- ✅ VS Code settings are configured for C# Dev Kit
- ✅ Target framework matches: `net8.0-windows10.0.19041.0`

## Steps to Get Tests in VS Code Test Explorer

### 1. Ensure C# Dev Kit Extension is Active
- Open VS Code
- Press `Ctrl+Shift+X` to open Extensions
- Verify "C# Dev Kit" is installed and enabled
- Also verify "C#" extension is installed

### 2. Force Test Discovery
Try these commands in order (press `Ctrl+Shift+P` for command palette):

1. **"Test: Reset and Reload All Test Data"**
2. **"Test: Refresh Tests"** 
3. **"Developer: Reload Window"**

### 3. Manual Test Discovery Steps
If the above doesn't work, try these steps:

1. **Open Terminal in VS Code** (`Ctrl+` `)
2. **Navigate to test project:**
   ```bash
   cd src/CSimple.Tests
   ```
3. **Build the test project:**
   ```bash
   dotnet build
   ```
4. **List tests to verify they're discoverable:**
   ```bash
   dotnet test --list-tests
   ```

### 4. Use VS Code Tasks
- Press `Ctrl+Shift+P`
- Type "Tasks: Run Task"
- Select "Build Test Project" to ensure tests are built
- Then select "Discover Tests" to force discovery

### 5. Check Test Explorer Panel
- Open Test Explorer by clicking the Testing icon in the Activity Bar (left sidebar)
- Or press `Ctrl+Shift+P` and type "Test: Focus on Test Explorer View"
- Look for the "CSimple.Tests" project in the test tree

### 6. Alternative: Use .NET Test Explorer Extension
If C# Dev Kit test discovery isn't working:
- Install ".NET Core Test Explorer" extension
- Configure it to point to your test project
- Add this to your settings.json:
  ```json
  {
    "dotnet-test-explorer.testProjectPath": "src/CSimple.Tests"
  }
  ```

## Troubleshooting

### Tests Still Not Showing?
1. **Check Output Panel:**
   - View → Output
   - Select "C# Dev Kit" from dropdown
   - Look for any error messages

2. **Verify Solution Detection:**
   - Bottom status bar should show "CSimple.sln" 
   - If not, open the solution file directly

3. **Force Reload:**
   - Close VS Code completely
   - Reopen the workspace folder
   - Wait for C# extension to fully load (watch status bar)

### Common Issues:
- **Multiple test frameworks:** Only MSTest should be used
- **Target framework mismatch:** Ensure both projects use same framework
- **Build errors:** Tests won't show if project has build errors
- **Extension conflicts:** Disable other test extensions temporarily

## Test Categories Available:
- **Unit Tests** (8 tests): Basic functionality testing
- **Build Tests** (17 tests): Build verification and integration  
- **Debug Tests** (1 test): Path debugging utilities
- **Integration Tests** (7 tests): End-to-end build scenarios
- **Performance Tests** (1 test): Build timing verification

## Quick Commands:
```bash
# Run all tests
dotnet test

# Run only unit tests  
dotnet test --filter "TestCategory=Unit"

# Run only build tests
dotnet test --filter "FullyQualifiedName~SimpleBuildTests"

# List all tests
dotnet test --list-tests
```

## Success Indicators:
- ✅ Test Explorer shows "CSimple.Tests" project
- ✅ Individual test methods are visible
- ✅ Play buttons appear next to tests
- ✅ Tests can be run from the UI
- ✅ Test results show pass/fail status
