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
        // Arrange - Clean first to avoid file locks
        await CleanProjectAsync();

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

        // Act - Retry on common MAUI build issues
        var (exitCode, output, error) = await ExecuteWithRetryAsync(startInfo, maxRetries: 2);

        // Assert
        if (exitCode != 0)
        {
            // Check for known MAUI issues and provide helpful guidance
            var knownIssues = AnalyzeBuildFailure(output, error);
            if (knownIssues.Any())
            {
                Assert.Fail($"Build failed with known MAUI issues: {string.Join(", ", knownIssues)}. " +
                          $"Consider cleaning build artifacts or restarting VS Code. Output: {output}. Error: {error}");
            }
        }

        Assert.AreEqual(0, exitCode,
            $"dotnet build should succeed. Output: {output}. Error: {error}");

        // More lenient success check for MAUI projects
        var isSuccessful = output.Contains("Build succeeded") ||
                          output.Contains("build succeeded") ||
                          (exitCode == 0 && !output.Contains("Build FAILED"));

        Assert.IsTrue(isSuccessful,
            $"Build output should indicate success. Output: {output}");
    }

    [TestMethod]
    [TestCategory("Build")]
    [Description("Verifies that 'dotnet build' with Windows framework succeeds")]
    public async Task DotNetBuildWindows_ShouldSucceed()
    {
        // Arrange - Clean first to avoid file locks
        await CleanProjectAsync();

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

        // Act - Retry on common MAUI build issues
        var (exitCode, output, error) = await ExecuteWithRetryAsync(startInfo, maxRetries: 2);

        // Assert
        if (exitCode != 0)
        {
            // Check for known MAUI issues and provide helpful guidance
            var knownIssues = AnalyzeBuildFailure(output, error);
            if (knownIssues.Any())
            {
                Assert.Fail($"Windows build failed with known MAUI issues: {string.Join(", ", knownIssues)}. " +
                          $"Consider cleaning build artifacts or restarting VS Code. Output: {output}. Error: {error}");
            }
        }

        Assert.AreEqual(0, exitCode,
            $"dotnet build (Windows) should succeed. Exit code: {exitCode}. Output: {output}. Error: {error}");

        // More lenient success check for MAUI projects
        var isSuccessful = output.Contains("Build succeeded") ||
                          output.Contains("build succeeded") ||
                          (exitCode == 0 && !output.Contains("Build FAILED"));

        Assert.IsTrue(isSuccessful,
            $"Windows build output should indicate success. Output: {output}");
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

    /// <summary>
    /// Cleans the project to avoid file locking issues common with MAUI builds.
    /// </summary>
    private static async Task CleanProjectAsync()
    {
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

        using var process = new Process { StartInfo = startInfo };
        process.Start();
        await process.WaitForExitAsync();

        // Wait a bit for file locks to release
        await Task.Delay(1000);
    }

    /// <summary>
    /// Executes a process with retry logic for common MAUI build issues.
    /// </summary>
    private static async Task<(int exitCode, string output, string error)> ExecuteWithRetryAsync(
        ProcessStartInfo startInfo, int maxRetries = 1)
    {
        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            using var process = new Process { StartInfo = startInfo };
            process.Start();

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            // If successful or last attempt, return result
            if (process.ExitCode == 0 || attempt == maxRetries)
            {
                return (process.ExitCode, output, error);
            }

            // Check if this is a retryable error
            if (IsRetryableError(output, error))
            {
                // Clean and wait before retry
                await CleanProjectAsync();
                await Task.Delay(2000); // Wait 2 seconds between retries
                continue;
            }

            // Non-retryable error, return immediately
            return (process.ExitCode, output, error);
        }

        // This should never be reached
        throw new InvalidOperationException("Retry logic error");
    }

    /// <summary>
    /// Checks if an error is retryable (typically file locking issues).
    /// </summary>
    private static bool IsRetryableError(string output, string error)
    {
        var retryableIndicators = new[]
        {
            "cannot access the file",
            "being used by another process",
            "CS2012",
            "APPX0002",
            "APPX1101",
            "file may be locked",
            "Microsoft.UI.Xaml.Markup.Compiler",
            "Payload contains two or more files"
        };

        var combinedOutput = $"{output} {error}".ToLowerInvariant();
        return retryableIndicators.Any(indicator => combinedOutput.Contains(indicator.ToLowerInvariant()));
    }

    /// <summary>
    /// Analyzes build failures and returns known MAUI issues for better error reporting.
    /// </summary>
    private static List<string> AnalyzeBuildFailure(string output, string error)
    {
        var issues = new List<string>();
        var combinedOutput = $"{output} {error}".ToLowerInvariant();

        if (combinedOutput.Contains("cannot access the file") || combinedOutput.Contains("being used by another process"))
        {
            issues.Add("File locking issue - try closing VS Code, running applications, or restarting");
        }

        if (combinedOutput.Contains("appx0702") || combinedOutput.Contains("payload file") || combinedOutput.Contains("does not exist"))
        {
            issues.Add("Missing resource files - MAUI resource generation issue");
        }

        if (combinedOutput.Contains("cs2012"))
        {
            issues.Add("Compiler file access issue - typically resolved by cleaning and rebuilding");
        }

        if (combinedOutput.Contains("xaml") && combinedOutput.Contains("compile"))
        {
            issues.Add("XAML compilation issue - check XAML syntax");
        }

        return issues;
    }
}
