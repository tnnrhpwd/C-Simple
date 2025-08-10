using System.Diagnostics;

namespace CSimple.Tests.BuildTests;

/// <summary>
/// Simple build verification tests that test the ability to build the main project
/// without directly referencing it. This avoids MAUI compatibility issues.
/// </summary>
[TestClass]
public class SimpleBuildTests
{
    private static readonly string ProjectDirectory = GetProjectDirectory();

    /// <summary>
    /// Gets the directory containing the CSimple project.
    /// </summary>
    private static string GetProjectDirectory()
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
            return Path.Combine(srcDirectory, "CSimple");
        }

        // Fallback: use a relative path from the current directory
        var currentDirectory = Directory.GetCurrentDirectory();
        var projectDirectory = Path.GetFullPath(Path.Combine(currentDirectory, "..", "..", "..", "..", "CSimple"));
        return projectDirectory;
    }

    [TestMethod]
    [TestCategory("Build")]
    [Description("Verifies that the CSimple project directory exists")]
    public void ProjectDirectory_ShouldExist()
    {
        // Arrange & Act & Assert
        Assert.IsTrue(Directory.Exists(ProjectDirectory),
            $"Project directory should exist at: {ProjectDirectory}");
    }

    [TestMethod]
    [TestCategory("Build")]
    [Description("Verifies that the CSimple.csproj file exists")]
    public void ProjectFile_ShouldExist()
    {
        // Arrange
        var projectFile = Path.Combine(ProjectDirectory, "CSimple.csproj");

        // Act & Assert
        Assert.IsTrue(File.Exists(projectFile),
            $"Project file should exist at: {projectFile}");
    }

    [TestMethod]
    [TestCategory("Build")]
    [Description("Verifies that essential MAUI files exist")]
    public void EssentialMauiFiles_ShouldExist()
    {
        // Arrange
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
            var filePath = Path.Combine(ProjectDirectory, file);
            Assert.IsTrue(File.Exists(filePath), $"Essential file should exist: {file}");
        }
    }

    [TestMethod]
    [TestCategory("Build")]
    [Description("Verifies that 'dotnet restore' succeeds for the main project")]
    public async Task DotNetRestore_ShouldSucceed()
    {
        // Arrange
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = "restore",
            WorkingDirectory = ProjectDirectory,
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
            $"dotnet restore should succeed. Output: {output}. Error: {error}");
    }

    [TestMethod]
    [TestCategory("Build")]
    [Description("Verifies that 'dotnet build' succeeds for the main project")]
    public async Task DotNetBuild_ShouldSucceed()
    {
        // Arrange
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = "build --configuration Debug --verbosity minimal",
            WorkingDirectory = ProjectDirectory,
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
            $"dotnet build should succeed. Output: {output}. Error: {error}");

        Assert.IsTrue(output.Contains("Build succeeded") || output.Contains("build succeeded"),
            $"Build output should indicate success. Output: {output}");
    }

    [TestMethod]
    [TestCategory("Build")]
    [Description("Verifies that 'dotnet build' with Windows framework succeeds")]
    public async Task DotNetBuildWindows_ShouldSucceed()
    {
        // Arrange
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = "build --framework net8.0-windows10.0.19041.0 --configuration Debug --verbosity minimal",
            WorkingDirectory = ProjectDirectory,
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
            $"dotnet build (Windows) should succeed. Exit code: {process.ExitCode}. Output: {output}. Error: {error}");
    }

    [TestMethod]
    [TestCategory("Build")]
    [Description("Verifies that 'dotnet clean' succeeds")]
    public async Task DotNetClean_ShouldSucceed()
    {
        // Arrange
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = "clean",
            WorkingDirectory = ProjectDirectory,
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
            $"dotnet clean should succeed. Output: {output}. Error: {error}");
    }

    [TestMethod]
    [TestCategory("Build")]
    [Description("Verifies that Resources directory exists with required files")]
    public void ResourcesDirectory_ShouldExistWithRequiredFiles()
    {
        // Arrange
        var resourcesDirectory = Path.Combine(ProjectDirectory, "Resources");

        // Act & Assert
        Assert.IsTrue(Directory.Exists(resourcesDirectory),
            "Resources directory should exist");

        // Check for some common resource files
        var resourceSubdirectories = new[] { "Images", "Fonts", "Styles" };
        foreach (var subdir in resourceSubdirectories)
        {
            var subdirPath = Path.Combine(resourcesDirectory, subdir);
            Assert.IsTrue(Directory.Exists(subdirPath),
                $"Resources/{subdir} directory should exist");
        }
    }

    [TestMethod]
    [TestCategory("Performance")]
    [Description("Verifies that build completes within reasonable time")]
    public async Task Build_ShouldCompleteWithinReasonableTime()
    {
        // Arrange
        var maxBuildTimeMinutes = 5; // Reasonable time for a build test
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = "build --configuration Debug --verbosity quiet",
            WorkingDirectory = ProjectDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        // Act
        var stopwatch = Stopwatch.StartNew();
        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();
        stopwatch.Stop();

        // Assert
        Assert.AreEqual(0, process.ExitCode,
            $"Build should succeed. Output: {output}. Error: {error}");

        Assert.IsTrue(stopwatch.Elapsed.TotalMinutes < maxBuildTimeMinutes,
            $"Build should complete within {maxBuildTimeMinutes} minutes. Actual: {stopwatch.Elapsed.TotalMinutes:F2} minutes");
    }
}
