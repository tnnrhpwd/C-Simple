using System.Diagnostics;
using System.Reflection;

namespace CSimple.Tests.DiagnosticTests;

/// <summary>
/// Comprehensive diagnostic tests to troubleshoot VS Code C# Dev Kit test explorer issues.
/// </summary>
[TestClass]
public class VSCodeTestExplorerDiagnostics
{
    [TestMethod]
    [TestCategory("Diagnostic")]
    [Description("Comprehensive environment and path diagnostics for VS Code test explorer")]
    public void VSCodeTestExplorer_EnvironmentDiagnostics()
    {
        Console.WriteLine("=== VS Code Test Explorer Diagnostics ===");

        // Assembly and execution context
        var assembly = Assembly.GetExecutingAssembly();
        var assemblyLocation = assembly.Location;
        var assemblyDirectory = Path.GetDirectoryName(assemblyLocation);
        var currentDirectory = Directory.GetCurrentDirectory();
        var workingDirectory = Environment.CurrentDirectory;

        Console.WriteLine($"Assembly Location: {assemblyLocation}");
        Console.WriteLine($"Assembly Directory: {assemblyDirectory}");
        Console.WriteLine($"Current Directory: {currentDirectory}");
        Console.WriteLine($"Working Directory: {workingDirectory}");
        Console.WriteLine($"Environment User: {Environment.UserName}");
        Console.WriteLine($"Process Name: {Process.GetCurrentProcess().ProcessName}");

        // Environment variables that might affect testing
        var dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT");
        var path = Environment.GetEnvironmentVariable("PATH");
        var vsCodeWorkspace = Environment.GetEnvironmentVariable("VSCODE_WORKSPACE");

        Console.WriteLine($"DOTNET_ROOT: {dotnetRoot ?? "Not set"}");
        Console.WriteLine($"VSCODE_WORKSPACE: {vsCodeWorkspace ?? "Not set"}");
        Console.WriteLine($"PATH contains dotnet: {(path?.Contains("dotnet") ?? false)}");

        // File system checks
        var projectPath = FindProjectPath();
        var solutionPath = FindSolutionPath();

        Console.WriteLine($"Project Path: {projectPath}");
        Console.WriteLine($"Project Exists: {File.Exists(projectPath)}");
        Console.WriteLine($"Solution Path: {solutionPath}");
        Console.WriteLine($"Solution Exists: {File.Exists(solutionPath)}");

        // Test framework info
        Console.WriteLine($"Test Framework: MSTest");
        Console.WriteLine($"Target Framework: {assembly.GetCustomAttribute<System.Runtime.Versioning.TargetFrameworkAttribute>()?.FrameworkName}");

        // Directory listings for troubleshooting
        if (assemblyDirectory != null)
        {
            Console.WriteLine($"Assembly Directory Contents:");
            try
            {
                var files = Directory.GetFiles(assemblyDirectory, "*", SearchOption.TopDirectoryOnly);
                foreach (var file in files.Take(10))
                {
                    Console.WriteLine($"  - {Path.GetFileName(file)}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Error listing files: {ex.Message}");
            }
        }

        Assert.IsTrue(true, "Diagnostic test always passes");
    }

    [TestMethod]
    [TestCategory("Diagnostic")]
    [Description("Tests dotnet CLI availability and functionality")]
    public async Task VSCodeTestExplorer_DotNetCliDiagnostics()
    {
        Console.WriteLine("=== .NET CLI Diagnostics ===");

        // Test dotnet --version
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = "--version",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        try
        {
            using var process = new Process { StartInfo = startInfo };
            process.Start();

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            Console.WriteLine($"dotnet --version exit code: {process.ExitCode}");
            Console.WriteLine($"dotnet version output: {output.Trim()}");

            if (!string.IsNullOrEmpty(error))
            {
                Console.WriteLine($"dotnet version error: {error.Trim()}");
            }

            Assert.AreEqual(0, process.ExitCode, "dotnet CLI should be available");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error running dotnet CLI: {ex.Message}");
            Assert.Fail($"dotnet CLI not available: {ex.Message}");
        }
    }

    [TestMethod]
    [TestCategory("Diagnostic")]
    [Description("Simple test that should always pass to verify basic test execution")]
    public void VSCodeTestExplorer_BasicTestExecution()
    {
        Console.WriteLine("=== Basic Test Execution ===");
        Console.WriteLine("This is a simple test that should always pass in any execution context.");
        Console.WriteLine($"Test executed at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

        // Basic assertions that should always work
        Assert.IsTrue(true, "Basic true assertion");
        Assert.AreEqual(1, 1, "Basic equality assertion");
        Assert.IsNotNull("test", "Basic not null assertion");

        Console.WriteLine("All basic assertions passed successfully.");
    }

    [TestMethod]
    [TestCategory("Diagnostic")]
    [Description("Shows current working directory and calculated paths for debugging")]
    public void VSCodeTestExplorer_PathResolutionDiagnostics()
    {
        Console.WriteLine("=== Path Resolution Diagnostics ===");

        // Current working directory when tests run
        var currentDir = Directory.GetCurrentDirectory();
        Console.WriteLine($"Current Directory: {currentDir}");

        // Assembly location
        var assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
        Console.WriteLine($"Assembly Location: {assemblyLocation}");

        // Test directory
        var testDirectory = Path.GetDirectoryName(assemblyLocation)!;
        Console.WriteLine($"Test Directory: {testDirectory}");

        // Try to find src directory
        var srcDirectory = testDirectory;
        while (srcDirectory != null && !Path.GetFileName(srcDirectory).Equals("src"))
        {
            srcDirectory = Directory.GetParent(srcDirectory)?.FullName;
            Console.WriteLine($"Checking directory: {srcDirectory}");
            if (srcDirectory != null && Path.GetFileName(srcDirectory) == "src")
                break;
        }
        Console.WriteLine($"Found src directory: {srcDirectory}");

        // Expected CSimple directory
        if (srcDirectory != null)
        {
            var csimpleDir = Path.Combine(srcDirectory, "CSimple");
            Console.WriteLine($"Expected CSimple directory: {csimpleDir}");
            Console.WriteLine($"CSimple directory exists: {Directory.Exists(csimpleDir)}");
        }

        // Alternative approach - navigate from current directory
        var altProjectPath = Path.GetFullPath(Path.Combine(currentDir, "..", "..", "..", "..", "CSimple"));
        Console.WriteLine($"Alternative CSimple path: {altProjectPath}");
        Console.WriteLine($"Alternative path exists: {Directory.Exists(altProjectPath)}");

        // Try a simpler approach - just look for the project file in known locations
        var possiblePaths = new[]
        {
            Path.GetFullPath(Path.Combine(currentDir, "..", "CSimple")),
            Path.GetFullPath(Path.Combine(currentDir, "..", "..", "CSimple")),
            Path.GetFullPath(Path.Combine(currentDir, "..", "..", "..", "CSimple")),
            Path.GetFullPath(Path.Combine(currentDir, "..", "..", "..", "..", "CSimple")),
            @"c:\Users\tanne\Documents\Github\C-Simple\src\CSimple"
        };

        string foundPath = "None found";
        foreach (var path in possiblePaths)
        {
            if (Directory.Exists(path))
            {
                foundPath = path;
                break;
            }
        }

        // Show the results in the console output
        Console.WriteLine($"Paths Debug Info:\n" +
                         $"Current: {currentDir}\n" +
                         $"Assembly: {assemblyLocation}\n" +
                         $"Test Dir: {testDirectory}\n" +
                         $"Src Dir: {srcDirectory}\n" +
                         $"Alt Path: {altProjectPath} (exists: {Directory.Exists(altProjectPath)})\n" +
                         $"Found Path: {foundPath}");

        // Assert that we found a valid path
        Assert.IsTrue(foundPath != "None found", "Should be able to find the CSimple project directory");
        Assert.IsTrue(Directory.Exists(foundPath), "The found path should exist");
    }

    [TestMethod]
    [TestCategory("Diagnostic")]
    [Description("Shows build-related path resolution details for troubleshooting")]
    public void VSCodeTestExplorer_BuildPathDiagnostics()
    {
        Console.WriteLine("=== Build Path Diagnostics ===");

        // Project path resolution similar to BuildVerificationTests
        var assemblyLocation = Assembly.GetExecutingAssembly().Location;
        var testDirectory = Path.GetDirectoryName(assemblyLocation) ?? Directory.GetCurrentDirectory();

        Console.WriteLine($"Assembly Location: {assemblyLocation}");
        Console.WriteLine($"Test Directory: {testDirectory}");

        // Start from the test directory and navigate up to find the CSimple project
        var directory = new DirectoryInfo(testDirectory);

        // Navigate up to find the src directory containing both test and main projects
        while (directory != null && !directory.GetDirectories("CSimple").Any())
        {
            Console.WriteLine($"Checking directory: {directory.FullName}");
            directory = directory.Parent;
        }

        if (directory?.GetDirectories("CSimple").FirstOrDefault() is DirectoryInfo projectDir)
        {
            var projectFile = Path.Combine(projectDir.FullName, "CSimple.csproj");
            Console.WriteLine($"Found Project File: {projectFile}");
            Console.WriteLine($"Project File Exists: {File.Exists(projectFile)}");

            if (File.Exists(projectFile))
            {
                var projectContent = File.ReadAllText(projectFile);
                Console.WriteLine($"Project contains MAUI: {projectContent.Contains("Microsoft.Maui") || projectContent.Contains("UseMaui")}");
                Console.WriteLine($"Project targets Windows: {projectContent.Contains("net8.0-windows")}");
            }
        }

        // Solution path resolution
        directory = new DirectoryInfo(testDirectory);
        while (directory != null && !directory.GetFiles("*.sln").Any())
        {
            directory = directory.Parent;
        }

        if (directory?.GetFiles("*.sln").FirstOrDefault() is FileInfo solutionFile)
        {
            Console.WriteLine($"Found Solution File: {solutionFile.FullName}");
            Console.WriteLine($"Solution File Exists: {File.Exists(solutionFile.FullName)}");
        }

        Assert.IsTrue(true, "Build path diagnostic test always passes");
    }

    [TestMethod]
    [TestCategory("Diagnostic")]
    [Description("Shows integration test path resolution details for troubleshooting")]
    public void VSCodeTestExplorer_IntegrationTestPathDiagnostics()
    {
        Console.WriteLine("=== Integration Test Path Diagnostics ===");

        // Get project and solution directories using the same logic as DotNetBuildIntegrationTests
        var assemblyLocation = Assembly.GetExecutingAssembly().Location;
        var testDirectory = Path.GetDirectoryName(assemblyLocation) ?? Directory.GetCurrentDirectory();
        var directory = new DirectoryInfo(testDirectory);

        Console.WriteLine($"Starting from test directory: {testDirectory}");

        // Navigate up to find the src directory containing both test and main projects
        while (directory != null && !directory.GetDirectories("CSimple").Any())
        {
            Console.WriteLine($"Looking for CSimple directory in: {directory.FullName}");
            directory = directory.Parent;
        }

        string projectDirectory = "Not found";
        if (directory?.GetDirectories("CSimple").FirstOrDefault() is DirectoryInfo projectDir)
        {
            projectDirectory = projectDir.FullName;
            Console.WriteLine($"Found Project Directory: {projectDirectory}");
            Console.WriteLine($"Project Directory Exists: {Directory.Exists(projectDirectory)}");

            // Check for essential project files
            var projectFile = Path.Combine(projectDirectory, "CSimple.csproj");
            var appXaml = Path.Combine(projectDirectory, "App.xaml");
            var mauiProgram = Path.Combine(projectDirectory, "MauiProgram.cs");

            Console.WriteLine($"CSimple.csproj exists: {File.Exists(projectFile)}");
            Console.WriteLine($"App.xaml exists: {File.Exists(appXaml)}");
            Console.WriteLine($"MauiProgram.cs exists: {File.Exists(mauiProgram)}");
        }

        // Find solution directory
        directory = new DirectoryInfo(testDirectory);
        while (directory != null && !directory.GetFiles("*.sln").Any())
        {
            directory = directory.Parent;
        }

        string solutionDirectory = "Not found";
        if (directory?.GetFiles("*.sln").FirstOrDefault() is FileInfo solutionFile)
        {
            solutionDirectory = directory.FullName;
            Console.WriteLine($"Found Solution Directory: {solutionDirectory}");
            Console.WriteLine($"Solution File: {solutionFile.FullName}");
            Console.WriteLine($"Solution File Exists: {File.Exists(solutionFile.FullName)}");
        }

        Console.WriteLine($"Summary - Project: {projectDirectory}, Solution: {solutionDirectory}");

        Assert.IsTrue(true, "Integration test path diagnostic always passes");
    }

    private static string FindProjectPath()
    {
        var assemblyLocation = Assembly.GetExecutingAssembly().Location;
        var directory = new DirectoryInfo(Path.GetDirectoryName(assemblyLocation)!);

        // Navigate up to find the src directory
        while (directory != null && !directory.GetDirectories("CSimple").Any())
        {
            directory = directory.Parent;
        }

        if (directory?.GetDirectories("CSimple").FirstOrDefault() is DirectoryInfo projectDir)
        {
            return Path.Combine(projectDir.FullName, "CSimple.csproj");
        }

        return "Not found";
    }

    private static string FindSolutionPath()
    {
        var assemblyLocation = Assembly.GetExecutingAssembly().Location;
        var directory = new DirectoryInfo(Path.GetDirectoryName(assemblyLocation)!);

        // Navigate up to find the directory containing the solution
        while (directory != null && !directory.GetFiles("*.sln").Any())
        {
            directory = directory.Parent;
        }

        if (directory?.GetFiles("*.sln").FirstOrDefault() is FileInfo solutionFile)
        {
            return solutionFile.FullName;
        }

        return "Not found";
    }
}
