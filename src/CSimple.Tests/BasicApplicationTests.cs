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

    [TestMethod]
    [TestCategory("Unit")]
    [Description("Verifies that JSON serialization and deserialization work correctly")]
    public void JsonOperations_ShouldWorkCorrectly()
    {
        // Arrange
        var testObject = new { Name = "CSimple", Version = "1.0", IsActive = true, Count = 42 };

        // Act
        var json = System.Text.Json.JsonSerializer.Serialize(testObject);
        var deserializedObject = System.Text.Json.JsonSerializer.Deserialize<dynamic>(json);

        // Assert
        Assert.IsTrue(!string.IsNullOrEmpty(json), "JSON string should not be null or empty");
        Assert.IsTrue(json.Contains("CSimple"), "JSON should contain the Name value");
        Assert.IsTrue(json.Contains("1.0"), "JSON should contain the Version value");
        Assert.IsNotNull(deserializedObject, "Deserialized object should not be null");
    }

    [TestMethod]
    [TestCategory("Unit")]
    [Description("Verifies that numeric type conversions work correctly")]
    public void NumericConversions_ShouldWorkCorrectly()
    {
        // Arrange
        var intValue = 42;
        var doubleValue = 3.14159;
        var stringNumber = "123";
        var invalidString = "abc";

        // Act
        var intToDouble = Convert.ToDouble(intValue);
        var doubleToInt = Convert.ToInt32(doubleValue);
        var stringToInt = int.Parse(stringNumber);
        var tryParseSuccess = int.TryParse(stringNumber, out var parsedValue);
        var tryParseFail = int.TryParse(invalidString, out var failedValue);

        // Assert
        Assert.AreEqual(42.0, intToDouble, "Int to double conversion should work");
        Assert.AreEqual(3, doubleToInt, "Double to int conversion should truncate correctly");
        Assert.AreEqual(123, stringToInt, "String to int parsing should work");
        Assert.IsTrue(tryParseSuccess, "TryParse should succeed for valid number string");
        Assert.AreEqual(123, parsedValue, "TryParse should return correct value");
        Assert.IsFalse(tryParseFail, "TryParse should fail for invalid string");
        Assert.AreEqual(0, failedValue, "Failed TryParse should return default value");
    }

    [TestMethod]
    [TestCategory("Unit")]
    [Description("Verifies that GUID operations work correctly")]
    public void GuidOperations_ShouldWorkCorrectly()
    {
        // Arrange & Act
        var guid1 = Guid.NewGuid();
        var guid2 = Guid.NewGuid();
        var emptyGuid = Guid.Empty;
        var guidString = guid1.ToString();
        var parsedGuid = Guid.Parse(guidString);

        // Assert
        Assert.AreNotEqual(guid1, guid2, "Two new GUIDs should be different");
        Assert.AreNotEqual(guid1, emptyGuid, "New GUID should not equal empty GUID");
        Assert.AreEqual(guid1, parsedGuid, "Parsed GUID should equal original");
        Assert.AreEqual(36, guidString.Length, "GUID string should be 36 characters long");
        Assert.IsTrue(guidString.Contains("-"), "GUID string should contain hyphens");
    }

    [TestMethod]
    [TestCategory("Unit")]
    [Description("Verifies that environment and system information operations work correctly")]
    public void SystemInformation_ShouldWorkCorrectly()
    {
        // Act
        var machineName = Environment.MachineName;
        var userName = Environment.UserName;
        var osVersion = Environment.OSVersion;
        var processorCount = Environment.ProcessorCount;
        var workingSet = Environment.WorkingSet;
        var currentDirectory = Environment.CurrentDirectory;

        // Assert
        Assert.IsTrue(!string.IsNullOrEmpty(machineName), "Machine name should not be empty");
        Assert.IsTrue(!string.IsNullOrEmpty(userName), "User name should not be empty");
        Assert.IsNotNull(osVersion, "OS version should not be null");
        Assert.IsTrue(processorCount > 0, "Processor count should be greater than 0");
        Assert.IsTrue(workingSet > 0, "Working set should be greater than 0");
        Assert.IsTrue(!string.IsNullOrEmpty(currentDirectory), "Current directory should not be empty");
    }

    [TestMethod]
    [TestCategory("Unit")]
    [Description("Verifies that regular expression operations work correctly")]
    public void RegularExpressions_ShouldWorkCorrectly()
    {
        // Arrange
        var emailPattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
        var phonePattern = @"^\(\d{3}\) \d{3}-\d{4}$";
        var validEmail = "test@example.com";
        var invalidEmail = "invalid-email";
        var validPhone = "(123) 456-7890";
        var invalidPhone = "123-456-7890";

        // Act
        var emailMatch = System.Text.RegularExpressions.Regex.IsMatch(validEmail, emailPattern);
        var invalidEmailMatch = System.Text.RegularExpressions.Regex.IsMatch(invalidEmail, emailPattern);
        var phoneMatch = System.Text.RegularExpressions.Regex.IsMatch(validPhone, phonePattern);
        var invalidPhoneMatch = System.Text.RegularExpressions.Regex.IsMatch(invalidPhone, phonePattern);

        // Assert
        Assert.IsTrue(emailMatch, "Valid email should match email pattern");
        Assert.IsFalse(invalidEmailMatch, "Invalid email should not match email pattern");
        Assert.IsTrue(phoneMatch, "Valid phone should match phone pattern");
        Assert.IsFalse(invalidPhoneMatch, "Invalid phone should not match phone pattern");
    }

    [TestMethod]
    [TestCategory("Unit")]
    [Description("Verifies that dictionary and key-value operations work correctly")]
    public void DictionaryOperations_ShouldWorkCorrectly()
    {
        // Arrange
        var dictionary = new Dictionary<string, int>
        {
            ["apple"] = 5,
            ["banana"] = 3,
            ["orange"] = 8
        };

        // Act
        var appleCount = dictionary["apple"];
        var hasApple = dictionary.ContainsKey("apple");
        var hasGrape = dictionary.ContainsKey("grape");
        var tryGetApple = dictionary.TryGetValue("apple", out var appleValue);
        var tryGetGrape = dictionary.TryGetValue("grape", out var grapeValue);
        var keys = dictionary.Keys.ToList();
        var values = dictionary.Values.ToList();

        // Assert
        Assert.AreEqual(5, appleCount, "Apple count should be 5");
        Assert.IsTrue(hasApple, "Dictionary should contain apple");
        Assert.IsFalse(hasGrape, "Dictionary should not contain grape");
        Assert.IsTrue(tryGetApple, "TryGetValue should succeed for apple");
        Assert.AreEqual(5, appleValue, "TryGetValue should return correct apple value");
        Assert.IsFalse(tryGetGrape, "TryGetValue should fail for grape");
        Assert.AreEqual(0, grapeValue, "Failed TryGetValue should return default value");
        Assert.AreEqual(3, keys.Count, "Should have 3 keys");
        Assert.AreEqual(3, values.Count, "Should have 3 values");
        Assert.IsTrue(keys.Contains("apple"), "Keys should contain apple");
    }

    [TestMethod]
    [TestCategory("Unit")]
    [Description("Verifies that enumerable and set operations work correctly")]
    public void SetOperations_ShouldWorkCorrectly()
    {
        // Arrange
        var set1 = new HashSet<int> { 1, 2, 3, 4, 5 };
        var set2 = new HashSet<int> { 4, 5, 6, 7, 8 };
        var list = new List<int> { 1, 2, 2, 3, 3, 3 };

        // Act
        var union = set1.Union(set2).ToList();
        var intersection = set1.Intersect(set2).ToList();
        var difference = set1.Except(set2).ToList();
        var distinctValues = list.Distinct().ToList();
        var symmetricDifference = set1.Union(set2).Except(set1.Intersect(set2)).ToList();

        // Assert
        Assert.AreEqual(8, union.Count, "Union should contain 8 elements");
        Assert.AreEqual(2, intersection.Count, "Intersection should contain 2 elements");
        Assert.IsTrue(intersection.Contains(4) && intersection.Contains(5), "Intersection should contain 4 and 5");
        Assert.AreEqual(3, difference.Count, "Difference should contain 3 elements");
        Assert.IsTrue(difference.Contains(1) && difference.Contains(2) && difference.Contains(3), "Difference should contain 1, 2, 3");
        Assert.AreEqual(3, distinctValues.Count, "Distinct should remove duplicates");
        Assert.AreEqual(6, symmetricDifference.Count, "Symmetric difference should contain 6 elements");
    }

    [TestMethod]
    [TestCategory("Unit")]
    [Description("Verifies that text encoding and decoding operations work correctly")]
    public void TextEncoding_ShouldWorkCorrectly()
    {
        // Arrange
        var originalText = "Hello, 世界! 🌍";
        var utf8Encoding = System.Text.Encoding.UTF8;
        var asciiEncoding = System.Text.Encoding.ASCII;

        // Act
        var utf8Bytes = utf8Encoding.GetBytes(originalText);
        var decodedUtf8 = utf8Encoding.GetString(utf8Bytes);
        var base64Encoded = Convert.ToBase64String(utf8Bytes);
        var base64Decoded = Convert.FromBase64String(base64Encoded);
        var decodedFromBase64 = utf8Encoding.GetString(base64Decoded);

        // Assert
        Assert.IsTrue(utf8Bytes.Length > originalText.Length, "UTF-8 bytes should be longer due to multibyte characters");
        Assert.AreEqual(originalText, decodedUtf8, "UTF-8 decoding should restore original text");
        Assert.IsTrue(!string.IsNullOrEmpty(base64Encoded), "Base64 encoding should produce non-empty string");
        Assert.AreEqual(originalText, decodedFromBase64, "Base64 round-trip should restore original text");
    }

    [TestMethod]
    [TestCategory("Unit")]
    [Description("Verifies that mathematical operations and constants work correctly")]
    public void MathOperations_ShouldWorkCorrectly()
    {
        // Arrange
        var angle = Math.PI / 4; // 45 degrees in radians
        var number = 16.7;

        // Act
        var sine = Math.Sin(angle);
        var cosine = Math.Cos(angle);
        var tangent = Math.Tan(angle);
        var sqrt = Math.Sqrt(number);
        var ceiling = Math.Ceiling(number);
        var floor = Math.Floor(number);
        var round = Math.Round(number);
        var absolute = Math.Abs(-5.5);
        var power = Math.Pow(2, 3);
        var logarithm = Math.Log10(100);

        // Assert
        Assert.AreEqual(0.707, Math.Round(sine, 3), "Sin(π/4) should be approximately 0.707");
        Assert.AreEqual(0.707, Math.Round(cosine, 3), "Cos(π/4) should be approximately 0.707");
        Assert.AreEqual(1.0, Math.Round(tangent, 3), "Tan(π/4) should be approximately 1.0");
        Assert.AreEqual(4.087, Math.Round(sqrt, 3), "Square root of 16.7 should be approximately 4.087");
        Assert.AreEqual(17, ceiling, "Ceiling of 16.7 should be 17");
        Assert.AreEqual(16, floor, "Floor of 16.7 should be 16");
        Assert.AreEqual(17, round, "Round of 16.7 should be 17");
        Assert.AreEqual(5.5, absolute, "Absolute value of -5.5 should be 5.5");
        Assert.AreEqual(8, power, "2^3 should be 8");
        Assert.AreEqual(2, logarithm, "Log10(100) should be 2");
    }

    [TestMethod]
    [TestCategory("Unit")]
    [Description("Verifies that reflection operations work correctly")]
    public void ReflectionOperations_ShouldWorkCorrectly()
    {
        // Arrange
        var testObject = new { Name = "Test", Value = 42 };
        var type = testObject.GetType();

        // Act
        var properties = type.GetProperties();
        var nameProperty = type.GetProperty("Name");
        var valueProperty = type.GetProperty("Value");
        var nameValue = nameProperty?.GetValue(testObject);
        var valueValue = valueProperty?.GetValue(testObject);
        var typeName = type.Name;
        var isAnonymous = type.Name.Contains("AnonymousType");

        // Assert
        Assert.AreEqual(2, properties.Length, "Anonymous type should have 2 properties");
        Assert.IsNotNull(nameProperty, "Name property should exist");
        Assert.IsNotNull(valueProperty, "Value property should exist");
        Assert.AreEqual("Test", nameValue, "Name property value should be 'Test'");
        Assert.AreEqual(42, valueValue, "Value property value should be 42");
        Assert.IsTrue(isAnonymous, "Type should be recognized as anonymous");
    }
}
