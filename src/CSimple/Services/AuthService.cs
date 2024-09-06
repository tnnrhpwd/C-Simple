using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Microsoft.Maui.Storage;  // For storing token

public class AuthService
{
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "https://mern-plan-web-service.onrender.com/api/data/";

    public AuthService()
    {
        _httpClient = new HttpClient();
    }

    // Login user and store token locally
    public async Task<bool> LoginAsync(string email, string password)
    {
        var userData = new { email, password };

        // Send POST request to login
        var response = await _httpClient.PostAsJsonAsync(BaseUrl + "login", userData);

        if (response.IsSuccessStatusCode)
        {
            var responseData = await response.Content.ReadAsStringAsync();
            var user = JsonConvert.DeserializeObject<UserResponse>(responseData);

            // Store the token securely
            await SecureStorage.SetAsync("userToken", user.Token);
            Debug.WriteLine("Login successful: userToken = ", user.Token);
            return true;
        }

        return false;
    }

    // Logout user by clearing token
    public void Logout()
    {
        SecureStorage.Remove("userToken");
    }

    // Check if the user is logged in by verifying if the token exists
    public async Task<bool> IsLoggedInAsync()
    {
        try
        {
            // Attempt to retrieve the token from secure storage
            var token = await SecureStorage.GetAsync("userToken");

            // If a token exists, return true (user is logged in)
            return !string.IsNullOrEmpty(token);
        }
        catch (Exception ex)
        {
            // Handle any exceptions (for example, SecureStorage may throw if not supported on the platform)
            Debug.WriteLine($"Error checking login status: {ex.Message}");
            return false;
        }
    }
}

// Define a model class to deserialize the user response (matching your API response)
public class UserResponse
{
    public string Token { get; set; }
    public string Email { get; set; }
}
