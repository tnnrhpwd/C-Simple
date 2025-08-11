using System.Diagnostics;

namespace CSimple.Tests.IntegrationTests;

/// <summary>
/// Integration tests that verify the application can be built and run using dotnet CLI commands.
/// These tests simulate the actual build process that would be used in CI/CD pipelines.
/// Tests are designed to run sequentially to avoid MAUI build conflicts.
/// </summary>
[TestClass]
[DoNotParallelize] // Prevent parallel execution to avoid MAUI file locking issues
public class DotNetBuildIntegrationTests
{
    private static readonly string ProjectDirectory = GetProjectDirectory();
    private static readonly string SolutionDirectory = GetSolutionDirectory();

    [TestInitialize]
    public async Task TestInitialize()
    {
        // Give a small delay to ensure VS Code test runner stabilizes
        await Task.Delay(1000);
    }

    [TestCleanup]
    public void TestCleanup()
    {
        // No special cleanup needed - let the [DoNotParallelize] attribute handle sequencing
    }

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
    [Timeout(300000)] // 5 minutes timeout for VS Code test explorer
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
    [Timeout(180000)] // 3 minutes timeout for VS Code test explorer
    public async Task DotNetBuild_ShouldCompleteSuccessfully()
    {
        // Skip cleaning to avoid potential hanging issues - build should handle it
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

        // Act - Single execution without retry to avoid hanging
        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        // Assert
        Assert.AreEqual(0, process.ExitCode,
            $"dotnet build should exit with code 0. Exit code: {process.ExitCode}. Output: {output}. Error: {error}");

        // Check that build was successful - be more flexible about build success indicators
        // MAUI builds often succeed with warnings but no errors
        var isSuccessful = output.Contains("Build succeeded") ||
                          output.Contains("build succeeded") ||
                          output.Contains("succeeded") ||
                          (process.ExitCode == 0 && !HasCompilationErrors(output, error));

        Assert.IsTrue(isSuccessful,
            $"Build should indicate success. Exit code: {process.ExitCode}. Output: {output}. Error: {error}");

        // Additional check: ensure no compilation errors (warnings are acceptable)
        Assert.IsFalse(HasCompilationErrors(output, error),
            $"Build should not have compilation errors. Output: {output}. Error: {error}");
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
    [Timeout(300000)] // 5 minutes timeout for VS Code test explorer
    public async Task DotNetBuildWindows_ShouldCompleteSuccessfully()
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
                          $"Output: {output}. Error: {error}");
            }
        }

        Assert.AreEqual(0, exitCode,
            $"dotnet build (Windows framework) should exit with code 0. Exit code: {exitCode}. Output: {output}. Error: {error}");

        // Check that build was successful (more lenient check for MAUI with resource issues)
        var isSuccessful = output.Contains("Build succeeded") ||
                          output.Contains("build succeeded") ||
                          (exitCode == 0 && !output.Contains("Build FAILED"));

        Assert.IsTrue(isSuccessful,
            $"Windows framework build output should indicate success. Output: {output}");
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Clean")]
    [Description("Verifies that 'dotnet clean' completes successfully")]
    [Timeout(300000)] // 5 minutes timeout for VS Code test explorer
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
    [Timeout(240000)] // 4 minutes timeout for VS Code test explorer
    public async Task SolutionBuild_ShouldCompleteSuccessfully()
    {
        // Skip cleaning to avoid potential hanging issues - solution build should handle it
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

        // Act - Single execution without retry to avoid hanging
        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        // Assert
        Assert.AreEqual(0, process.ExitCode,
            $"Solution build should exit with code 0. Exit code: {process.ExitCode}. Output: {output}. Error: {error}");

        // Check that build was successful - be more flexible about build success indicators
        // MAUI solution builds often succeed with warnings but no errors
        var isSuccessful = output.Contains("Build succeeded") ||
                          output.Contains("build succeeded") ||
                          output.Contains("succeeded") ||
                          (process.ExitCode == 0 && !HasCompilationErrors(output, error));

        Assert.IsTrue(isSuccessful,
            $"Solution build should indicate success. Exit code: {process.ExitCode}. Output: {output}. Error: {error}");

        // Additional check: ensure no compilation errors (warnings are acceptable)
        Assert.IsFalse(HasCompilationErrors(output, error),
            $"Solution build should not have compilation errors. Output: {output}. Error: {error}");
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Performance")]
    [Description("Verifies that build completes within reasonable time limits")]
    [Timeout(600000)] // 10 minutes timeout for VS Code test explorer
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

    /// <summary>
    /// Cleans the project to avoid file locking issues common with MAUI builds.
    /// </summary>
    private static async Task CleanProjectAsync()
    {
        // Simple, non-blocking clean approach for integration tests
        try
        {
            await ExecuteCleanCommandAsync(ProjectDirectory);
        }
        catch (Exception)
        {
            // If clean fails, continue anyway - the build might still succeed
        }

        // Very short wait for file locks to release
        await Task.Delay(500);
    }

    /// <summary>
    /// Executes a clean command in the specified directory.
    /// </summary>
    private static async Task ExecuteCleanCommandAsync(string workingDirectory)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = "clean",
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        // Add timeout to prevent hanging
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill();
            }
            catch (Exception)
            {
                // Ignore kill errors
            }
        }
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
                // For MAUI resource issues, also try restore before retry
                if (output.Contains("CS7064") || error.Contains("CS7064") ||
                    output.Contains("resizetizer") || error.Contains("resizetizer"))
                {
                    await ExecuteRestoreAsync();
                }

                // Clean and wait before retry
                await CleanProjectAsync();
                await Task.Delay(3000); // Wait 3 seconds between retries for MAUI
                continue;
            }

            // Non-retryable error, return immediately
            return (process.ExitCode, output, error);
        }

        // This should never be reached
        throw new InvalidOperationException("Retry logic error");
    }

    /// <summary>
    /// Executes a restore command to regenerate MAUI resources.
    /// </summary>
    private static async Task ExecuteRestoreAsync()
    {
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

        using var process = new Process { StartInfo = startInfo };
        process.Start();
        await process.WaitForExitAsync();
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
            "CS7064", // Missing resource files (often resolved by clean rebuild)
            "APPX0002",
            "APPX1101",
            "file may be locked",
            "Microsoft.UI.Xaml.Markup.Compiler",
            "Payload contains two or more files",
            "Error opening icon file",
            "resizetizer"
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

        if (combinedOutput.Contains("cs7064") || combinedOutput.Contains("error opening icon file") || combinedOutput.Contains("resizetizer"))
        {
            issues.Add("Missing MAUI resource files - clean and rebuild required");
        }

        if (combinedOutput.Contains("appx0702") || combinedOutput.Contains("payload file") || combinedOutput.Contains("does not exist"))
        {
            issues.Add("Missing resource files - MAUI resource generation issue");
        }

        if (combinedOutput.Contains("appx1101") || combinedOutput.Contains("payload contains two or more files"))
        {
            issues.Add("Duplicate payload files - MAUI packaging issue");
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

    /// <summary>
    /// Checks if the build output contains actual compilation errors (not warnings).
    /// </summary>
    private static bool HasCompilationErrors(string output, string error)
    {
        var combinedOutput = $"{output} {error}".ToLowerInvariant();

        // Check for actual error indicators (not warnings)
        var errorIndicators = new[]
        {
            "build failed",
            "compilation failed",
            ": error cs",
            ": error msb",
            "microsoft.net.test.sdk",
            "fatal error",
            "could not execute because the specified command or file was not found",
            "no executable found matching command"
        };

        // Exclude warning patterns that might be mistaken for errors
        var warningPatterns = new[]
        {
            ": warning cs",
            ": warning msb",
            "warning:",
            "with warning(s)"
        };

        // First check if there are any actual errors
        bool hasErrors = errorIndicators.Any(indicator => combinedOutput.Contains(indicator));

        // If we found error patterns, make sure they're not just warnings
        if (hasErrors)
        {
            // Double-check that we're not just seeing warnings
            var lines = combinedOutput.Split('\n');
            foreach (var line in lines)
            {
                // If a line contains error patterns but also warning patterns, it's likely a warning
                if (errorIndicators.Any(err => line.Contains(err)) &&
                    !warningPatterns.Any(warn => line.Contains(warn)))
                {
                    return true; // Found a real error
                }
            }
        }

        return false;
    }
}
