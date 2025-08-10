using System.Diagnostics;
using System.Text.Json;

namespace CSimple.Tests.CopilotAgent;

/// <summary>
/// Tests specifically designed for GitHub Copilot agent scenarios.
/// These tests validate the project structure and configurations that are commonly
/// needed when GitHub Copilot agents work with this codebase.
/// </summary>
[TestClass]
public class CopilotAgentTests
{
    private static readonly string WorkspaceRoot = GetWorkspaceRoot();

    private static string GetWorkspaceRoot()
    {
        var currentDir = Directory.GetCurrentDirectory();
        while (currentDir != null && !File.Exists(Path.Combine(currentDir, ".git", "config")))
        {
            currentDir = Directory.GetParent(currentDir)?.FullName;
        }
        return currentDir ?? throw new InvalidOperationException("Could not find workspace root");
    }

    [TestMethod]
    [TestCategory("CopilotAgent")]
    [Description("Verifies that essential files for Copilot agents exist")]
    public void CopilotAgent_EssentialFilesExist()
    {
        // Arrange
        var essentialFiles = new[]
        {
            "README.md",
            ".vscode/settings.json",
            ".vscode/tasks.json",
            "src/CSimple.sln",
            "src/CSimple/CSimple.csproj",
            "src/CSimple.Tests/CSimple.Tests.csproj"
        };

        // Act & Assert
        foreach (var file in essentialFiles)
        {
            var filePath = Path.Combine(WorkspaceRoot, file);
            Assert.IsTrue(File.Exists(filePath),
                $"Essential file for Copilot agents should exist: {file}");
        }
    }

    [TestMethod]
    [TestCategory("CopilotAgent")]
    [Description("Verifies that VS Code configuration supports proper test discovery")]
    public void CopilotAgent_VSCodeTestConfiguration()
    {
        // Arrange
        var settingsPath = Path.Combine(WorkspaceRoot, ".vscode", "settings.json");

        // Act
        Assert.IsTrue(File.Exists(settingsPath), "VS Code settings.json should exist");

        var settingsContent = File.ReadAllText(settingsPath);

        // Assert
        Assert.IsTrue(settingsContent.Contains("testExplorer.useNativeTesting"),
            "VS Code settings should enable native testing");
        Assert.IsTrue(settingsContent.Contains("dotnet.defaultSolution"),
            "VS Code settings should specify default solution");
    }

    [TestMethod]
    [TestCategory("CopilotAgent")]
    [Description("Verifies that the project structure supports agent navigation")]
    public void CopilotAgent_ProjectStructureNavigation()
    {
        // Arrange
        var expectedDirectories = new[]
        {
            "src",
            "src/CSimple",
            "src/CSimple.Tests",
            ".vscode",
            ".github/workflows"
        };

        // Act & Assert
        foreach (var directory in expectedDirectories)
        {
            var dirPath = Path.Combine(WorkspaceRoot, directory);
            Assert.IsTrue(Directory.Exists(dirPath),
                $"Expected directory for agent navigation should exist: {directory}");
        }
    }

    [TestMethod]
    [TestCategory("CopilotAgent")]
    [Description("Verifies that test categories are properly configured for filtering")]
    public void CopilotAgent_TestCategoriesConfigured()
    {
        // Arrange
        var testFiles = Directory.GetFiles(
            Path.Combine(WorkspaceRoot, "src", "CSimple.Tests"),
            "*Tests.cs",
            SearchOption.TopDirectoryOnly);

        // Act & Assert
        Assert.IsTrue(testFiles.Length > 0, "Should find test files");

        var foundCategories = new HashSet<string>();
        foreach (var testFile in testFiles)
        {
            var content = File.ReadAllText(testFile);

            // Look for TestCategory attributes
            if (content.Contains("[TestCategory(\"Unit\")]"))
                foundCategories.Add("Unit");
            if (content.Contains("[TestCategory(\"Build\")]"))
                foundCategories.Add("Build");
            if (content.Contains("[TestCategory(\"Integration\")]"))
                foundCategories.Add("Integration");
            if (content.Contains("[TestCategory(\"CopilotAgent\")]"))
                foundCategories.Add("CopilotAgent");
        }

        // Assert essential categories exist
        Assert.IsTrue(foundCategories.Contains("Unit"), "Should have Unit test category");
        Assert.IsTrue(foundCategories.Contains("Build"), "Should have Build test category");
        Assert.IsTrue(foundCategories.Contains("Integration"), "Should have Integration test category");
    }

    [TestMethod]
    [TestCategory("CopilotAgent")]
    [Description("Verifies that GitHub Actions workflow exists and is properly configured")]
    public void CopilotAgent_GitHubActionsConfigured()
    {
        // Arrange
        var workflowPath = Path.Combine(WorkspaceRoot, ".github", "workflows", "ci.yml");

        // Act
        Assert.IsTrue(File.Exists(workflowPath), "GitHub Actions CI workflow should exist");

        var workflowContent = File.ReadAllText(workflowPath);

        // Assert
        Assert.IsTrue(workflowContent.Contains("dotnet test"),
            "Workflow should include dotnet test command");
        Assert.IsTrue(workflowContent.Contains("TestCategory=CopilotAgent") ||
                     workflowContent.Contains("copilot-agent"),
            "Workflow should include Copilot agent specific tests");
    }

    [TestMethod]
    [TestCategory("CopilotAgent")]
    [Description("Verifies that test results can be generated in multiple formats")]
    public async Task CopilotAgent_TestResultFormats()
    {
        // Arrange
        var testProjectPath = Path.Combine(WorkspaceRoot, "src", "CSimple.Tests");
        var tempResultsPath = Path.Combine(Path.GetTempPath(), "CSimpleTestResults", Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempResultsPath);

        try
        {
            // Act - Run a simple test with TRX logger
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = $"test \"{testProjectPath}\" --filter \"TestCategory=Unit\" --logger \"trx;LogFileName=test-results.trx\" --results-directory \"{tempResultsPath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            // Assert
            Assert.AreEqual(0, process.ExitCode,
                $"Test run should succeed. Output: {output}. Error: {error}");

            var trxFiles = Directory.GetFiles(tempResultsPath, "*.trx", SearchOption.AllDirectories);
            Assert.IsTrue(trxFiles.Length > 0, "Should generate TRX test result files");
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempResultsPath))
            {
                Directory.Delete(tempResultsPath, true);
            }
        }
    }

    [TestMethod]
    [TestCategory("CopilotAgent")]
    [Description("Verifies that project metadata is accessible for agent context")]
    public void CopilotAgent_ProjectMetadataAccessible()
    {
        // Arrange
        var csprojPath = Path.Combine(WorkspaceRoot, "src", "CSimple", "CSimple.csproj");
        var testCsprojPath = Path.Combine(WorkspaceRoot, "src", "CSimple.Tests", "CSimple.Tests.csproj");

        // Act
        Assert.IsTrue(File.Exists(csprojPath), "Main project file should exist");
        Assert.IsTrue(File.Exists(testCsprojPath), "Test project file should exist");

        var mainProjectContent = File.ReadAllText(csprojPath);
        var testProjectContent = File.ReadAllText(testCsprojPath);

        // Assert - Check for key metadata
        Assert.IsTrue(mainProjectContent.Contains("net8.0-windows"),
            "Main project should target .NET 8 Windows");
        Assert.IsTrue(mainProjectContent.Contains("Microsoft.Maui") ||
                     mainProjectContent.Contains("UseMaui"),
            "Main project should be a MAUI project");

        Assert.IsTrue(testProjectContent.Contains("IsTestProject"),
            "Test project should be marked as a test project");
        Assert.IsTrue(testProjectContent.Contains("MSTest"),
            "Test project should use MSTest framework");
    }

    [TestMethod]
    [TestCategory("CopilotAgent")]
    [Description("Verifies that test documentation exists and is up-to-date")]
    public void CopilotAgent_TestDocumentationExists()
    {
        // Arrange
        var testReadmePath = Path.Combine(WorkspaceRoot, "src", "CSimple.Tests", "README.md");
        var mainReadmePath = Path.Combine(WorkspaceRoot, "README.md");

        // Act & Assert
        Assert.IsTrue(File.Exists(testReadmePath), "Test project README should exist");
        Assert.IsTrue(File.Exists(mainReadmePath), "Main project README should exist");

        var testReadmeContent = File.ReadAllText(testReadmePath);
        Assert.IsTrue(testReadmeContent.Contains("Test Categories"),
            "Test README should document test categories");
        Assert.IsTrue(testReadmeContent.Contains("dotnet test"),
            "Test README should include dotnet test examples");
    }
}
