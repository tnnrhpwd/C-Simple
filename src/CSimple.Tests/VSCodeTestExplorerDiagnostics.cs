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
