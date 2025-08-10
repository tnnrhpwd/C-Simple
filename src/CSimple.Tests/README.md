# C-Simple Test Suite

This test project provides comprehensive testing for the C-Simple MAUI application. It includes build verification tests, integration tests, and unit tests to ensure the application builds and functions correctly.

## Test Categories

### Build Verification Tests (`BuildVerificationTests.cs`)
These tests verify that the project builds successfully and meets basic requirements:

- **Project File Existence**: Ensures the project file exists and is accessible
- **Solution File Existence**: Verifies the solution file exists
- **Project Loading**: Tests that MSBuild can load the project without errors
- **Build Success**: Verifies the project builds successfully using MSBuild
- **MAUI Properties**: Checks for required MAUI-specific properties
- **Package References**: Validates required NuGet packages are referenced
- **Package Restore**: Tests that NuGet packages can be restored successfully
- **Essential Files**: Ensures core application files exist
- **Windows Configuration**: Verifies Windows-specific configurations

### Integration Tests (`DotNetBuildIntegrationTests.cs`)
These tests simulate real-world build scenarios using dotnet CLI commands:

- **Restore Integration**: Tests `dotnet restore` command
- **Build Integration**: Tests `dotnet build` command with Debug configuration
- **Release Build**: Tests `dotnet build` with Release configuration
- **Windows Framework Build**: Tests building specifically for Windows platform
- **Clean Operation**: Tests `dotnet clean` command
- **Solution Build**: Tests building at the solution level
- **Performance Testing**: Ensures builds complete within reasonable time limits

### Unit Tests (`BasicApplicationTests.cs`)
Basic unit tests that verify core functionality works correctly:

- **Math Operations**: Basic arithmetic operations
- **String Operations**: String manipulation and formatting
- **DateTime Operations**: Date and time calculations
- **Collections**: List and array operations
- **LINQ Operations**: Query operations
- **Exception Handling**: Error handling verification
- **Async Operations**: Asynchronous task operations
- **File Path Operations**: Path manipulation utilities

## Running Tests

### In VS Code
1. Open the Test Explorer (Testing icon in the sidebar)
2. The tests should automatically appear in the explorer
3. Click the play button next to any test or test group to run them
4. Use the filter options to run specific test categories

### Command Line
Run all tests:
```bash
dotnet test
```

Run only build verification tests:
```bash
dotnet test --filter "TestCategory=Build"
```

Run only integration tests:
```bash
dotnet test --filter "TestCategory=Integration"
```

Run only unit tests:
```bash
dotnet test --filter "TestCategory=Unit"
```

### VS Code Tasks
You can also use the predefined VS Code tasks:
- `Test C-Simple Application`: Runs all tests
- `Build Verification Tests`: Runs only build-related tests
- `Integration Tests`: Runs only integration tests

## Test Framework
This project uses:
- **MSTest**: Microsoft's unit testing framework
- **Microsoft.Build**: For programmatic build operations
- **.NET 8**: Target framework for the tests

## Configuration
The test project is configured to:
- Reference the main CSimple project
- Use MSTest as the testing framework
- Include code coverage collection
- Support async testing patterns
- Provide detailed test output

## Adding New Tests
When adding new tests:
1. Place them in the appropriate category folder structure
2. Use descriptive test names and the `[Description]` attribute
3. Include appropriate `[TestCategory]` attributes for filtering
4. Follow the Arrange-Act-Assert pattern
5. Add comprehensive error messages to assertions

## Integration with CI/CD
These tests are designed to work well in continuous integration environments:
- All tests are deterministic and reliable
- Build tests verify the project can be built in different configurations
- Integration tests simulate real build scenarios
- Performance tests ensure builds don't take too long
