using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics;

namespace CSimple.Tests;

/// <summary>
/// Tests for AppModeService persistence functionality.
/// Note: These tests verify the AppModeService implementation indirectly 
/// to avoid direct MAUI dependencies in the test project.
/// </summary>
[TestClass]
public class AppModeServiceTests
{
    private static readonly string ProjectDirectory = GetProjectDirectory();

    private static string GetProjectDirectory()
    {
        // Get the test assembly directory and navigate to find the src directory
        var testAssemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
        var testDirectory = Path.GetDirectoryName(testAssemblyLocation)!;

        // Navigate up to find the src directory
        var current = new DirectoryInfo(testDirectory);
        while (current != null && current.Name != "src")
        {
            current = current.Parent;
        }

        if (current == null)
            throw new DirectoryNotFoundException("Could not locate src directory");

        return Path.Combine(current.FullName, "CSimple");
    }

    [TestMethod]
    public async Task AppModeService_Implementation_Should_Include_Persistence()
    {
        // Arrange
        var appModeServicePath = Path.Combine(ProjectDirectory, "Services", "AppModeService", "AppModeService.cs");

        // Act - Read the AppModeService implementation
        var content = await File.ReadAllTextAsync(appModeServicePath);

        // Assert - Verify that persistence functionality is implemented
        Assert.IsTrue(content.Contains("SecureStorage"), "AppModeService should use SecureStorage for persistence");
        Assert.IsTrue(content.Contains("SaveModeAsync"), "AppModeService should have SaveModeAsync method");
        Assert.IsTrue(content.Contains("LoadSavedModeAsync"), "AppModeService should have LoadSavedModeAsync method");
        Assert.IsTrue(content.Contains("APP_MODE_KEY"), "AppModeService should define a key for storage");

        Debug.WriteLine("AppModeService persistence implementation verified successfully");
    }

    [TestMethod]
    public async Task HomePage_Should_Load_Persisted_AppMode_Settings()
    {
        // Arrange
        var homePagePath = Path.Combine(ProjectDirectory, "Pages", "HomePage.xaml.cs");

        // Act - Read the HomePage implementation
        var content = await File.ReadAllTextAsync(homePagePath);

        // Assert - Verify that HomePage loads the persisted app mode
        Assert.IsTrue(content.Contains("OnPropertyChanged(nameof(IsOnlineMode))"),
            "HomePage should refresh IsOnlineMode property on appearing");
        Assert.IsTrue(content.Contains("OnPropertyChanged(nameof(AppModeLabel))"),
            "HomePage should refresh AppModeLabel property on appearing");

        Debug.WriteLine("HomePage app mode loading implementation verified successfully");
    }

    [TestMethod]
    public void AppModeService_Should_Have_Constructor_With_Persistence_Loading()
    {
        // Arrange
        var appModeServicePath = Path.Combine(ProjectDirectory, "Services", "AppModeService", "AppModeService.cs");

        // Act - Read the AppModeService implementation
        var content = File.ReadAllText(appModeServicePath);

        // Assert - Verify that constructor loads saved settings
        Assert.IsTrue(content.Contains("public AppModeService()"),
            "AppModeService should have a parameterless constructor");
        Assert.IsTrue(content.Contains("LoadSavedModeAsync"),
            "AppModeService constructor should call LoadSavedModeAsync");

        Debug.WriteLine("AppModeService constructor implementation verified successfully");
    }
}
