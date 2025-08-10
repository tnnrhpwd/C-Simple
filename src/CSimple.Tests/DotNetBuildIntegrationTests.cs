using System.Diagnostics;

namespace CSimple.Tests.IntegrationTests;

/// <summary>
/// Integration tests that verify the application can be built and run using dotnet CLI commands.
/// These tests simulate the actual build process that would be used in CI/CD pipelines.
/// </summary>
[TestClass]
public class DotNetBuildIntegrationTests
{
    private static readonly string ProjectDirectory = GetProjectDirectory();
    private static readonly string SolutionDirectory = GetSolutionDirectory();

    /// <summary>
    /// Gets the directory containing the CSimple project.
    /// </summary>
    private static string GetProjectDirectory()
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
            return projectDir.FullName;
        }

        // Additional fallback methods for VS Code test explorer and other contexts
        var fallbackPaths = new[]
        {
            // Relative from current working directory
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "CSimple")),
            // Relative from test assembly location
            Path.GetFullPath(Path.Combine(testDirectory, "..", "..", "..", "..", "CSimple")),
            // Navigate from workspace root
            Path.GetFullPath(Path.Combine(testDirectory, "..", "..", "..", "..", "..", "src", "CSimple"))
        };

        foreach (var fallbackPath in fallbackPaths)
        {
            if (Directory.Exists(fallbackPath) && File.Exists(Path.Combine(fallbackPath, "CSimple.csproj")))
                return fallbackPath;
        }

        // Final fallback: return the first attempt for error reporting
        return fallbackPaths[0];
    }

    /// <summary>
    /// Gets the directory containing the solution file.
    /// </summary>
    private static string GetSolutionDirectory()
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
            return solutionFile.Directory!.FullName;
        }

        // Additional fallback methods for VS Code test explorer and other contexts
        var fallbackPaths = new[]
        {
            // Relative from current working directory
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..")),
            // Relative from test assembly location
            Path.GetFullPath(Path.Combine(testDirectory, "..", "..", "..", "..")),
            // Navigate from workspace root
            Path.GetFullPath(Path.Combine(testDirectory, "..", "..", "..", "..", "..", "src"))
        };

        foreach (var fallbackPath in fallbackPaths)
        {
            if (Directory.Exists(fallbackPath) && Directory.GetFiles(fallbackPath, "*.sln").Any())
                return fallbackPath;
        }

        // Final fallback: return the first attempt for error reporting
        return fallbackPaths[0];
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Build")]
    [Description("Verifies that 'dotnet restore' completes successfully")]
    public async Task DotNetRestore_ShouldCompleteSuccessfully()
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
            $"dotnet restore should exit with code 0. Output: {output}. Error: {error}");
        Assert.IsTrue(string.IsNullOrEmpty(error) || !error.Contains("error"),
            $"dotnet restore should not produce errors. Error output: {error}");
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
        Console.WriteLine($"Project Directory: {ProjectDirectory}");
        Console.WriteLine($"Solution Directory: {SolutionDirectory}");
        Console.WriteLine($"Project Directory Exists: {Directory.Exists(ProjectDirectory)}");
        Console.WriteLine($"Solution Directory Exists: {Directory.Exists(SolutionDirectory)}");

        // This test should always pass - it's just for diagnostics
        Assert.IsTrue(true, "Diagnostic test always passes");
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Build")]
    [Description("Verifies that 'dotnet build' completes successfully")]
    public async Task DotNetBuild_ShouldCompleteSuccessfully()
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
            $"dotnet build should exit with code 0. Output: {output}. Error: {error}");

        // Check that build was successful
        Assert.IsTrue(output.Contains("Build succeeded") || output.Contains("build succeeded"),
            $"Build output should indicate success. Output: {output}");
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Build")]
    [Description("Verifies that 'dotnet build' with Release configuration completes successfully")]
    public async Task DotNetBuildRelease_ShouldCompleteSuccessfully()
    {
        // Arrange
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = "build --configuration Release --verbosity minimal",
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
            $"dotnet build (Release) should exit with code 0. Output: {output}. Error: {error}");

        // Check that build was successful
        Assert.IsTrue(output.Contains("Build succeeded") || output.Contains("build succeeded"),
            $"Release build output should indicate success. Output: {output}");
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Build")]
    [Description("Verifies that 'dotnet build' with Windows framework completes successfully")]
    public async Task DotNetBuildWindows_ShouldCompleteSuccessfully()
    {
        // Arrange - Build specifically for Windows platform
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
            $"dotnet build (Windows framework) should exit with code 0. Output: {output}. Error: {error}");

        // Check that build was successful
        Assert.IsTrue(output.Contains("Build succeeded") || output.Contains("build succeeded"),
            $"Windows framework build output should indicate success. Output: {output}");
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Clean")]
    [Description("Verifies that 'dotnet clean' completes successfully")]
    public async Task DotNetClean_ShouldCompleteSuccessfully()
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
            $"dotnet clean should exit with code 0. Output: {output}. Error: {error}");
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Solution")]
    [Description("Verifies that solution-level 'dotnet build' completes successfully")]
    public async Task SolutionBuild_ShouldCompleteSuccessfully()
    {
        // Arrange
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = "build --configuration Debug --verbosity minimal",
            WorkingDirectory = SolutionDirectory,
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
            $"Solution build should exit with code 0. Output: {output}. Error: {error}");

        // Check that build was successful
        Assert.IsTrue(output.Contains("Build succeeded") || output.Contains("build succeeded"),
            $"Solution build output should indicate success. Output: {output}");
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Performance")]
    [Description("Verifies that build completes within reasonable time limits")]
    public async Task DotNetBuild_ShouldCompleteWithinTimeLimit()
    {
        // Arrange
        var maxBuildTimeMinutes = 10; // Reasonable time limit for a MAUI app build
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
            $"Build should complete within {maxBuildTimeMinutes} minutes. Actual time: {stopwatch.Elapsed.TotalMinutes:F2} minutes");
    }
}
