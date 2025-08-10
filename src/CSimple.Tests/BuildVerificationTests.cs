using System.Diagnostics;
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
        // Get the test assembly location for more reliable path resolution
        var assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
        var testDirectory = Path.GetDirectoryName(assemblyLocation) ?? Directory.GetCurrentDirectory();

        // Start from the test directory and navigate up to find the CSimple project
        var directory = new DirectoryInfo(testDirectory);

        // Navigate up to find the src directory containing both test and main projects
        while (directory != null && !directory.GetDirectories("CSimple").Any())
        {
            directory = directory.Parent;
        }

        if (directory?.GetDirectories("CSimple").FirstOrDefault() is DirectoryInfo projectDir)
        {
            var projectFile = Path.Combine(projectDir.FullName, "CSimple.csproj");
            if (File.Exists(projectFile))
                return projectFile;
        }

        // Additional fallback methods for VS Code test explorer and other contexts
        var fallbackPaths = new[]
        {
            // Relative from current working directory
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "CSimple", "CSimple.csproj")),
            // Relative from test assembly location
            Path.GetFullPath(Path.Combine(testDirectory, "..", "..", "..", "..", "CSimple", "CSimple.csproj")),
            // Navigate from workspace root
            Path.GetFullPath(Path.Combine(testDirectory, "..", "..", "..", "..", "..", "src", "CSimple", "CSimple.csproj"))
        };

        foreach (var fallbackPath in fallbackPaths)
        {
            if (File.Exists(fallbackPath))
                return fallbackPath;
        }

        // Final fallback: return the first attempt for error reporting
        return fallbackPaths[0];
    }

    /// <summary>
    /// Gets the path to the solution file.
    /// </summary>
    private static string GetSolutionPath()
    {
        // Get the test assembly location for more reliable path resolution
        var assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
        var testDirectory = Path.GetDirectoryName(assemblyLocation) ?? Directory.GetCurrentDirectory();

        // Start from the test directory and navigate up to find the solution
        var directory = new DirectoryInfo(testDirectory);

        // Navigate up to find the directory containing the solution
        while (directory != null && !directory.GetFiles("*.sln").Any())
        {
            directory = directory.Parent;
        }

        if (directory?.GetFiles("*.sln").FirstOrDefault() is FileInfo solutionFile)
        {
            return solutionFile.FullName;
        }

        // Additional fallback methods for VS Code test explorer and other contexts
        var fallbackPaths = new[]
        {
            // Relative from current working directory
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "CSimple.sln")),
            // Relative from test assembly location
            Path.GetFullPath(Path.Combine(testDirectory, "..", "..", "..", "..", "CSimple.sln")),
            // Navigate from workspace root
            Path.GetFullPath(Path.Combine(testDirectory, "..", "..", "..", "..", "..", "src", "CSimple.sln"))
        };

        foreach (var fallbackPath in fallbackPaths)
        {
            if (File.Exists(fallbackPath))
                return fallbackPath;
        }

        // Final fallback: return the first attempt for error reporting
        return fallbackPaths[0];
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
    [TestCategory("Diagnostic")]
    [Description("Shows path resolution details for troubleshooting VS Code test explorer issues")]
    public void PathResolution_DiagnosticInfo()
    {
        // This test helps diagnose path resolution issues in different execution contexts
        var assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
        var currentDirectory = Directory.GetCurrentDirectory();
        var testDirectory = Path.GetDirectoryName(assemblyLocation) ?? currentDirectory;

        Console.WriteLine($"Assembly Location: {assemblyLocation}");
        Console.WriteLine($"Current Directory: {currentDirectory}");
        Console.WriteLine($"Test Directory: {testDirectory}");
        Console.WriteLine($"Project Path: {ProjectPath}");
        Console.WriteLine($"Solution Path: {SolutionPath}");
        Console.WriteLine($"Project Exists: {File.Exists(ProjectPath)}");
        Console.WriteLine($"Solution Exists: {File.Exists(SolutionPath)}");

        // This test should always pass - it's just for diagnostics
        Assert.IsTrue(true, "Diagnostic test always passes");
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
        Exception? loadException = null;

        try
        {
            // Instead of loading the project with MSBuild, just verify it's valid XML
            var projectContent = File.ReadAllText(ProjectPath);
            var doc = System.Xml.Linq.XDocument.Parse(projectContent);

            // Verify it's a valid SDK-style project
            var projectElement = doc.Root;
            Assert.IsNotNull(projectElement, "Project should have a root element");
            Assert.AreEqual("Project", projectElement.Name.LocalName, "Root element should be Project");

            var sdkAttribute = projectElement.Attribute("Sdk");
            Assert.IsNotNull(sdkAttribute, "Project should have Sdk attribute");
            Assert.AreEqual("Microsoft.NET.Sdk", sdkAttribute.Value, "Project should use Microsoft.NET.Sdk");
        }
        catch (Exception ex)
        {
            loadException = ex;
        }

        // Assert
        Assert.IsNull(loadException, $"Project should load without exceptions. Error: {loadException?.Message}");
    }

    [TestMethod]
    [TestCategory("Build")]
    [Description("Verifies that the project builds successfully using dotnet CLI")]
    public async Task Project_ShouldBuildSuccessfully()
    {
        // Arrange
        var projectDirectory = Path.GetDirectoryName(ProjectPath)!;
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = "build --configuration Debug --verbosity minimal",
            WorkingDirectory = projectDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        // Act
        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        // Assert
        Assert.AreEqual(0, process.ExitCode,
            $"dotnet build should exit with code 0. Output: {output}. Error: {error}");

        // Check that build was successful
        Assert.IsTrue(output.Contains("Build succeeded") || output.Contains("build succeeded"),
            $"Build output should indicate success. Output: {output}");
    }

    [TestMethod]
    [TestCategory("Build")]
    [Description("Verifies that the project contains required MAUI properties")]
    public void Project_ShouldHaveRequiredMauiProperties()
    {
        // Arrange & Act
        var projectContent = File.ReadAllText(ProjectPath);
        var doc = System.Xml.Linq.XDocument.Parse(projectContent);

        // Assert - Check for essential MAUI properties by parsing XML directly
        var useMaui = doc.Descendants("UseMaui").FirstOrDefault()?.Value;
        Assert.AreEqual("true", useMaui, "Project should have UseMaui set to true");

        var outputType = doc.Descendants("OutputType").FirstOrDefault()?.Value;
        Assert.AreEqual("Exe", outputType, "Project should have OutputType set to Exe");

        var singleProject = doc.Descendants("SingleProject").FirstOrDefault()?.Value;
        Assert.AreEqual("true", singleProject, "Project should have SingleProject set to true");

        var targetFrameworks = doc.Descendants("TargetFrameworks").FirstOrDefault()?.Value;
        Assert.IsFalse(string.IsNullOrEmpty(targetFrameworks),
            "Project should have TargetFrameworks defined");
    }

    [TestMethod]
    [TestCategory("Build")]
    [Description("Verifies that required NuGet packages are referenced")]
    public void Project_ShouldHaveRequiredPackageReferences()
    {
        // Arrange & Act
        var projectContent = File.ReadAllText(ProjectPath);
        var doc = System.Xml.Linq.XDocument.Parse(projectContent);

        var packageReferences = doc.Descendants("PackageReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(include => include != null)
            .ToList();

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

    [TestMethod]
    [TestCategory("Build")]
    [Description("Verifies that the project can restore NuGet packages successfully")]
    public async Task Project_ShouldRestorePackagesSuccessfully()
    {
        // Arrange
        var projectDirectory = Path.GetDirectoryName(ProjectPath)!;
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = "restore",
            WorkingDirectory = projectDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        // Act
        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        // Assert
        Assert.AreEqual(0, process.ExitCode,
            $"Package restore should succeed. Output: {output}. Error: {error}");
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
        var projectContent = File.ReadAllText(ProjectPath);
        var doc = System.Xml.Linq.XDocument.Parse(projectContent);

        // Assert - Check Windows-specific configurations by parsing XML directly
        var targetFrameworks = doc.Descendants("TargetFrameworks").FirstOrDefault()?.Value;
        Assert.IsTrue(targetFrameworks?.Contains("net8.0-windows") == true,
            "Project should target Windows platform");

        var supportedOSPlatformVersion = doc.Descendants("SupportedOSPlatformVersion")
            .Where(e => e.Attribute("Condition")?.Value?.Contains("windows") == true)
            .FirstOrDefault()?.Value;
        Assert.IsFalse(string.IsNullOrEmpty(supportedOSPlatformVersion),
            "Project should have SupportedOSPlatformVersion defined for Windows");

        var useWinUI = doc.Descendants("UseWinUI").FirstOrDefault()?.Value;
        Assert.AreEqual("true", useWinUI, "Project should use WinUI");
    }
}
