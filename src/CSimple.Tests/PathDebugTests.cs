namespace CSimple.Tests.DebugTests;

/// <summary>
/// Debug tests to verify path resolution is working correctly.
/// </summary>
[TestClass]
public class PathDebugTests
{
    [TestMethod]
    [TestCategory("Debug")]
    [Description("Shows current working directory and calculated paths")]
    public void ShowPaths_ForDebugging()
    {
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

        // Show the results in the assertion message
        Assert.Fail($"Paths Debug Info:\n" +
                   $"Current: {currentDir}\n" +
                   $"Assembly: {assemblyLocation}\n" +
                   $"Test Dir: {testDirectory}\n" +
                   $"Src Dir: {srcDirectory}\n" +
                   $"Alt Path: {altProjectPath} (exists: {Directory.Exists(altProjectPath)})\n" +
                   $"Found Path: {foundPath}");
    }
}
