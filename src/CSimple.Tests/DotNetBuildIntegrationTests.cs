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
        var currentDirectory = Directory.GetCurrentDirectory();
        var projectDirectory = Path.Combine(currentDirectory, "..", "CSimple");
        return Path.GetFullPath(projectDirectory);
    }

    /// <summary>
    /// Gets the directory containing the solution file.
    /// </summary>
    private static string GetSolutionDirectory()
    {
        var currentDirectory = Directory.GetCurrentDirectory();
        var solutionDirectory = Path.Combine(currentDirectory, "..");
        return Path.GetFullPath(solutionDirectory);
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
