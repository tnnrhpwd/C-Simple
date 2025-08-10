namespace CSimple.Tests.UnitTests;

/// <summary>
/// Basic unit tests for the C-Simple application.
/// These tests verify core functionality and serve as examples for future test development.
/// </summary>
[TestClass]
public class BasicApplicationTests
{
    [TestMethod]
    [TestCategory("Unit")]
    [Description("Verifies that basic arithmetic operations work correctly")]
    public void BasicMath_ShouldWorkCorrectly()
    {
        // Arrange
        var a = 5;
        var b = 3;

        // Act
        var sum = a + b;
        var difference = a - b;
        var product = a * b;

        // Assert
        Assert.AreEqual(8, sum, "Addition should work correctly");
        Assert.AreEqual(2, difference, "Subtraction should work correctly");
        Assert.AreEqual(15, product, "Multiplication should work correctly");
    }

    [TestMethod]
    [TestCategory("Unit")]
    [Description("Verifies that string operations work correctly")]
    public void StringOperations_ShouldWorkCorrectly()
    {
        // Arrange
        var testString = "CSimple";
        var prefix = "App: ";

        // Act
        var length = testString.Length;
        var upperCase = testString.ToUpper();
        var combined = prefix + testString;

        // Assert
        Assert.AreEqual(7, length, "String length should be calculated correctly");
        Assert.AreEqual("CSIMPLE", upperCase, "String should be converted to uppercase correctly");
        Assert.AreEqual("App: CSimple", combined, "String concatenation should work correctly");
    }

    [TestMethod]
    [TestCategory("Unit")]
    [Description("Verifies that DateTime operations work correctly")]
    public void DateTimeOperations_ShouldWorkCorrectly()
    {
        // Arrange
        var startDate = new DateTime(2024, 1, 1);
        var endDate = new DateTime(2024, 12, 31);

        // Act
        var daysDifference = (endDate - startDate).Days;
        var isLeapYear = DateTime.IsLeapYear(2024);

        // Assert
        Assert.AreEqual(365, daysDifference, "Days difference should be calculated correctly for leap year");
        Assert.IsTrue(isLeapYear, "2024 should be recognized as a leap year");
    }

    [TestMethod]
    [TestCategory("Unit")]
    [Description("Verifies that collections work correctly")]
    public void Collections_ShouldWorkCorrectly()
    {
        // Arrange
        var list = new List<string> { "Home", "Login", "Inputs", "Contact", "About" };

        // Act
        var count = list.Count;
        var firstItem = list.FirstOrDefault();
        var containsLogin = list.Contains("Login");

        // Assert
        Assert.AreEqual(5, count, "List should contain 5 items");
        Assert.AreEqual("Home", firstItem, "First item should be 'Home'");
        Assert.IsTrue(containsLogin, "List should contain 'Login'");
    }

    [TestMethod]
    [TestCategory("Unit")]
    [Description("Verifies that LINQ operations work correctly")]
    public void LinqOperations_ShouldWorkCorrectly()
    {
        // Arrange
        var numbers = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

        // Act
        var evenNumbers = numbers.Where(n => n % 2 == 0).ToList();
        var sum = numbers.Sum();
        var max = numbers.Max();

        // Assert
        Assert.AreEqual(5, evenNumbers.Count, "Should find 5 even numbers");
        Assert.AreEqual(55, sum, "Sum should be 55");
        Assert.AreEqual(10, max, "Maximum should be 10");
    }

    [TestMethod]
    [TestCategory("Unit")]
    [Description("Verifies that exception handling works correctly")]
    public void ExceptionHandling_ShouldWorkCorrectly()
    {
        // Arrange & Act & Assert
        Assert.ThrowsException<DivideByZeroException>(() =>
        {
            int zero = 0;
            var result = 10 / zero;
        }, "Division by zero should throw DivideByZeroException");

        Assert.ThrowsException<NullReferenceException>(() =>
        {
            string? nullString = null;
            var length = nullString!.Length;
        }, "Accessing Length on null string should throw NullReferenceException");
    }

    [TestMethod]
    [TestCategory("Unit")]
    [Description("Verifies that async operations work correctly")]
    public async Task AsyncOperations_ShouldWorkCorrectly()
    {
        // Arrange
        var delay = TimeSpan.FromMilliseconds(50); // Reduced delay to avoid timing issues
        var startTime = DateTime.Now;

        // Act
        await Task.Delay(delay);
        var endTime = DateTime.Now;
        var actualDelay = endTime - startTime;

        // Assert - Allow for some timing variance in async operations
        Assert.IsTrue(actualDelay >= TimeSpan.FromMilliseconds(40),
            $"Actual delay ({actualDelay.TotalMilliseconds}ms) should be at least 40ms (allowing for timing variance)");
        Assert.IsTrue(actualDelay < delay.Add(TimeSpan.FromMilliseconds(100)),
            "Actual delay should not be significantly longer than requested");
    }

    [TestMethod]
    [TestCategory("Unit")]
    [Description("Verifies that file path operations work correctly")]
    public void FilePathOperations_ShouldWorkCorrectly()
    {
        // Arrange
        var fileName = "test.txt";
        var directory = @"C:\temp";

        // Act
        var fullPath = Path.Combine(directory, fileName);
        var extension = Path.GetExtension(fileName);
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);

        // Assert
        Assert.AreEqual(@"C:\temp\test.txt", fullPath, "Full path should be constructed correctly");
        Assert.AreEqual(".txt", extension, "Extension should be extracted correctly");
        Assert.AreEqual("test", fileNameWithoutExtension, "Filename without extension should be extracted correctly");
    }
}
