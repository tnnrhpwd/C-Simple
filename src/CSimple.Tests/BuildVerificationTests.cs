using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;

namespace CSimple.Tests.BuildTests;

/// <summary>
/// Build verification tests for the C-Simple MAUI application.
/// These tests ensure the project builds successfully and meets basic requirements.
/// </summary>
[TestClass]
public class BuildVerificationTests
{
    private static readonly string ProjectPath = GetProjectPath();
    private static readonly string SolutionPath = GetSolutionPath();

    /// <summary>
    /// Gets the path to the CSimple project file.
    /// </summary>
    private static string GetProjectPath()
    {
        // Get the test assembly directory and navigate to find the src directory
        var testAssemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
        var testDirectory = Path.GetDirectoryName(testAssemblyLocation)!;

        // Navigate up to find the src directory
        var srcDirectory = testDirectory;
        while (srcDirectory != null && !Path.GetFileName(srcDirectory).Equals("src"))
        {
            srcDirectory = Directory.GetParent(srcDirectory)?.FullName;
            if (srcDirectory != null && Path.GetFileName(srcDirectory) == "src")
                break;
        }

        if (srcDirectory != null)
        {
            var projectFile = Path.Combine(srcDirectory, "CSimple", "CSimple.csproj");
            return projectFile;
        }

        // Fallback: use a relative path from the current directory
        var currentDirectory = Directory.GetCurrentDirectory();
        var projectFile2 = Path.GetFullPath(Path.Combine(currentDirectory, "..", "..", "..", "..", "CSimple", "CSimple.csproj"));
        return projectFile2;
    }

    /// <summary>
    /// Gets the path to the solution file.
    /// </summary>
    private static string GetSolutionPath()
    {
        // Get the test assembly directory and navigate to find the src directory
        var testAssemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
        var testDirectory = Path.GetDirectoryName(testAssemblyLocation)!;

        // Navigate up to find the src directory
        var srcDirectory = testDirectory;
        while (srcDirectory != null && !Path.GetFileName(srcDirectory).Equals("src"))
        {
            srcDirectory = Directory.GetParent(srcDirectory)?.FullName;
            if (srcDirectory != null && Path.GetFileName(srcDirectory) == "src")
                break;
        }

        if (srcDirectory != null)
        {
            var solutionFile = Path.Combine(srcDirectory, "CSimple.sln");
            return solutionFile;
        }

        // Fallback: use a relative path from the current directory
        var currentDirectory = Directory.GetCurrentDirectory();
        var solutionFile2 = Path.GetFullPath(Path.Combine(currentDirectory, "..", "..", "..", "..", "CSimple.sln"));
        return solutionFile2;
    }

    [TestMethod]
    [TestCategory("Build")]
    [Description("Verifies that the CSimple project file exists and is accessible")]
    public void ProjectFile_ShouldExist()
    {
        // Arrange & Act & Assert
        Assert.IsTrue(File.Exists(ProjectPath), $"Project file should exist at: {ProjectPath}");
    }

    [TestMethod]
    [TestCategory("Build")]
    [Description("Verifies that the solution file exists and is accessible")]
    public void SolutionFile_ShouldExist()
    {
        // Arrange & Act & Assert
        Assert.IsTrue(File.Exists(SolutionPath), $"Solution file should exist at: {SolutionPath}");
    }

    [TestMethod]
    [TestCategory("Build")]
    [Description("Verifies that the project can be loaded by MSBuild without errors")]
    public void Project_ShouldLoadSuccessfully()
    {
        // Arrange & Act
        Project? project = null;
        Exception? loadException = null;

        try
        {
            project = new Project(ProjectPath);
        }
        catch (Exception ex)
        {
            loadException = ex;
        }

        // Assert
        Assert.IsNull(loadException, $"Project should load without exceptions. Error: {loadException?.Message}");
        Assert.IsNotNull(project, "Project should be loaded successfully");

        // Cleanup
        project?.ProjectCollection.Dispose();
    }

    [TestMethod]
    [TestCategory("Build")]
    [Description("Verifies that the project builds successfully using MSBuild")]
    public void Project_ShouldBuildSuccessfully()
    {
        // Arrange
        var buildParameters = new BuildParameters()
        {
            Loggers = new List<ILogger> { new ConsoleLogger(LoggerVerbosity.Minimal) }
        };

        var buildRequest = new BuildRequestData(
            ProjectPath,
            new Dictionary<string, string>
            {
                { "Configuration", "Debug" },
                { "Platform", "Any CPU" }
            },
            toolsVersion: null,
            targetsToBuild: new[] { "Build" },
            hostServices: null);

        // Act
        BuildResult? buildResult = null;
        Exception? buildException = null;

        try
        {
            using var buildManager = BuildManager.DefaultBuildManager;
            buildResult = buildManager.Build(buildParameters, buildRequest);
        }
        catch (Exception ex)
        {
            buildException = ex;
        }

        // Assert
        Assert.IsNull(buildException, $"Build should not throw exceptions. Error: {buildException?.Message}");
        Assert.IsNotNull(buildResult, "Build result should not be null");
        Assert.AreEqual(BuildResultCode.Success, buildResult.OverallResult,
            $"Build should succeed. Build result: {buildResult.OverallResult}");

        // Additional assertions for build quality
        Assert.IsTrue(buildResult.ResultsByTarget.ContainsKey("Build"),
            "Build target should be executed");

        var buildTargetResult = buildResult.ResultsByTarget["Build"];
        Assert.AreEqual(TargetResultCode.Success, buildTargetResult.ResultCode,
            "Build target should complete successfully");
    }

    [TestMethod]
    [TestCategory("Build")]
    [Description("Verifies that the project contains required MAUI properties")]
    public void Project_ShouldHaveRequiredMauiProperties()
    {
        // Arrange & Act
        var project = new Project(ProjectPath);

        try
        {
            // Assert - Check for essential MAUI properties
            var useMaui = project.GetPropertyValue("UseMaui");
            Assert.AreEqual("true", useMaui, "Project should have UseMaui set to true");

            var outputType = project.GetPropertyValue("OutputType");
            Assert.AreEqual("Exe", outputType, "Project should have OutputType set to Exe");

            var singleProject = project.GetPropertyValue("SingleProject");
            Assert.AreEqual("true", singleProject, "Project should have SingleProject set to true");

            var targetFrameworks = project.GetPropertyValue("TargetFrameworks");
            Assert.IsFalse(string.IsNullOrEmpty(targetFrameworks),
                "Project should have TargetFrameworks defined");
        }
        finally
        {
            project.ProjectCollection.Dispose();
        }
    }

    [TestMethod]
    [TestCategory("Build")]
    [Description("Verifies that required NuGet packages are referenced")]
    public void Project_ShouldHaveRequiredPackageReferences()
    {
        // Arrange & Act
        var project = new Project(ProjectPath);
        var packageReferences = project.GetItems("PackageReference")
            .Select(item => item.EvaluatedInclude)
            .ToList();

        try
        {
            // Assert - Check for essential MAUI packages
            var requiredPackages = new[]
            {
                "Microsoft.Maui.Controls",
                "Microsoft.Extensions.Logging.Debug"
            };

            foreach (var requiredPackage in requiredPackages)
            {
                Assert.IsTrue(packageReferences.Contains(requiredPackage),
                    $"Project should reference {requiredPackage} package");
            }
        }
        finally
        {
            project.ProjectCollection.Dispose();
        }
    }

    [TestMethod]
    [TestCategory("Build")]
    [Description("Verifies that the project can restore NuGet packages successfully")]
    public void Project_ShouldRestorePackagesSuccessfully()
    {
        // Arrange
        var buildParameters = new BuildParameters()
        {
            Loggers = new List<ILogger> { new ConsoleLogger(LoggerVerbosity.Minimal) }
        };

        var restoreRequest = new BuildRequestData(
            ProjectPath,
            new Dictionary<string, string>
            {
                { "Configuration", "Debug" }
            },
            toolsVersion: null,
            targetsToBuild: new[] { "Restore" },
            hostServices: null);

        // Act
        BuildResult? restoreResult = null;
        Exception? restoreException = null;

        try
        {
            using var buildManager = BuildManager.DefaultBuildManager;
            restoreResult = buildManager.Build(buildParameters, restoreRequest);
        }
        catch (Exception ex)
        {
            restoreException = ex;
        }

        // Assert
        Assert.IsNull(restoreException, $"Package restore should not throw exceptions. Error: {restoreException?.Message}");
        Assert.IsNotNull(restoreResult, "Restore result should not be null");
        Assert.AreEqual(BuildResultCode.Success, restoreResult.OverallResult,
            $"Package restore should succeed. Result: {restoreResult.OverallResult}");
    }

    [TestMethod]
    [TestCategory("Build")]
    [Description("Verifies that essential application files exist")]
    public void Project_ShouldHaveEssentialFiles()
    {
        // Arrange
        var projectDirectory = Path.GetDirectoryName(ProjectPath)!;
        var essentialFiles = new[]
        {
            "App.xaml",
            "App.xaml.cs",
            "AppShell.xaml",
            "MauiProgram.cs"
        };

        // Act & Assert
        foreach (var file in essentialFiles)
        {
            var filePath = Path.Combine(projectDirectory, file);
            Assert.IsTrue(File.Exists(filePath), $"Essential file should exist: {file}");
        }
    }

    [TestMethod]
    [TestCategory("Build")]
    [Description("Verifies that the application has proper configuration for Windows platform")]
    public void Project_ShouldHaveWindowsConfiguration()
    {
        // Arrange & Act
        var project = new Project(ProjectPath);

        try
        {
            // Assert - Check Windows-specific configurations
            var targetFrameworks = project.GetPropertyValue("TargetFrameworks");
            Assert.IsTrue(targetFrameworks.Contains("net8.0-windows"),
                "Project should target Windows platform");

            var supportedOSPlatformVersion = project.GetPropertyValue("SupportedOSPlatformVersion");
            Assert.IsFalse(string.IsNullOrEmpty(supportedOSPlatformVersion),
                "Project should have SupportedOSPlatformVersion defined for Windows");

            var useWinUI = project.GetPropertyValue("UseWinUI");
            Assert.AreEqual("true", useWinUI, "Project should use WinUI");
        }
        finally
        {
            project.ProjectCollection.Dispose();
        }
    }
}
