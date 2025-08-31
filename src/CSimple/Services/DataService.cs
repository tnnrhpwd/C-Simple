using Microsoft.Maui.Storage;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;

public class DataService
{
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "https://www.sthopwood.com/api/data/";
    // private const string BaseUrl = "https://localhost:5000/api/data/";
    // private const string BaseUrl = "https://mern-plan-web-service.onrender.com/api/data/";
    private readonly UpdateDataService _updateDataService;

    public DataService()
    {
        _httpClient = new HttpClient();
        _updateDataService = new UpdateDataService();
    }

    private void SetAuthorizationHeader(string token)
    {
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    // Create new data
    public async Task<DataModel> CreateDataAsync(object data, string token)
    {
        SetAuthorizationHeader(token);

        // Wrap data under "data" property
        var wrappedData = new { data = data };
        var jsonData = JsonSerializer.Serialize(wrappedData);

        var jsonContent = new StringContent(jsonData, System.Text.Encoding.UTF8, "application/json");

        // Calculate the size of the JSON content
        var dataSize = System.Text.Encoding.UTF8.GetByteCount(jsonData);
        Debug.WriteLine($"----Size of the data being sent: {dataSize} bytes----");

        try
        {
            // Send the POST request
            var response = await _httpClient.PostAsync(BaseUrl, jsonContent);
            Debug.WriteLine($"Request URL: {BaseUrl} (POST) with data: {(jsonData.Length > 50 ? jsonData[..50] : jsonData)}");  // Log the request URL for debugging

            // Handle the response
            return await HandleResponse<DataModel>(response);
        }
        catch (HttpRequestException ex)
        {
            if (IsTokenExpiredError(ex))
            {
                await HandleTokenExpiration();
                throw new UnauthorizedAccessException("Your session has expired. Please log in again.");
            }
            throw;
        }
    }

    // Get data with a single 'data' query parameter
    public async Task<DataModel> GetDataAsync(string data, string token, CancellationToken cancellationToken = default)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        // Construct the URL with the query parameter
        var url = $"{BaseUrl}?data={{\"text\":\"{data}\"}}";
        Debug.WriteLine($"Request URL: {url}");  // Log the request URL for debugging

        const int maxRetries = 5;
        const int delayMilliseconds = 10000; // 10 seconds
        int retryCount = 0;

        while (retryCount < maxRetries)
        {
            try
            {
                using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                // Log the raw response content for debugging
                var responseContent = await response.Content.ReadAsStringAsync();
                Debug.WriteLine("Length of responseContent:" + JsonSerializer.Serialize(responseContent).Length.ToString());
                Debug.WriteLine($"1. (DataService.GetDataAsync) Raw response data: {responseContent.Length}");

                // Check for token expiration
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    Debug.WriteLine("Token expired detected in GetDataAsync");
                    await HandleTokenExpiration();
                    throw new UnauthorizedAccessException("Your session has expired. Please log in again.");
                }

                // Handle the response
                return await HandleResponse<DataModel>(response);
            }
            catch (HttpRequestException ex)
            {
                if (IsTokenExpiredError(ex))
                {
                    Debug.WriteLine("Token expired detected from exception in GetDataAsync");
                    await HandleTokenExpiration();
                    throw new UnauthorizedAccessException("Your session has expired. Please log in again.");
                }

                retryCount++;
                Debug.WriteLine($"Attempt {retryCount} failed: {ex.Message}");
                if (retryCount >= maxRetries)
                {
                    throw;
                }
                Debug.WriteLine($"Retrying in {delayMilliseconds / 1000} seconds...");
                await Task.Delay(delayMilliseconds);
            }
        }

        throw new Exception("Failed to get data after multiple retries.");
    }

    // Update existing data using the backend's "compress" or "update" method
    public async Task<DataModel> UpdateDataAsync(string id, object data, string token)
    {
        try
        {
            return await _updateDataService.UpdateDataAsync(id, data, token);
        }
        catch (HttpRequestException ex)
        {
            if (IsTokenExpiredError(ex))
            {
                await HandleTokenExpiration();
                throw new UnauthorizedAccessException("Your session has expired. Please log in again.");
            }
            throw;
        }
    }

    // Delete user data
    public async Task<DataModel> DeleteDataAsync(string id, string token)
    {
        SetAuthorizationHeader(token);
        try
        {
            var response = await _httpClient.DeleteAsync($"{BaseUrl}{id}");
            return await HandleResponse<DataModel>(response);
        }
        catch (HttpRequestException ex)
        {
            if (IsTokenExpiredError(ex))
            {
                await HandleTokenExpiration();
                throw new UnauthorizedAccessException("Your session has expired. Please log in again.");
            }
            throw;
        }
    }

    // Login user and store token and nickname locally
    public async Task<DataModel.User> LoginAsync(string email, string password)
    {
        var userData = new { email, password };
        var jsonContent = new StringContent(
            JsonSerializer.Serialize(userData),
            System.Text.Encoding.UTF8,
            "application/json"
        );

        var response = await _httpClient.PostAsync(BaseUrl + "login", jsonContent);
        Debug.WriteLine($"Request URL: {BaseUrl}login");
        Debug.WriteLine($"Request content: {JsonSerializer.Serialize(userData)}");
        Debug.WriteLine($"Response status: {response.StatusCode}");
        var responseContent = await response.Content.ReadAsStringAsync();
        Debug.WriteLine($"Response content: {responseContent}");

        if (response.IsSuccessStatusCode)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var user = JsonSerializer.Deserialize<DataModel.User>(responseContent, options);
            if (user == null || string.IsNullOrEmpty(user.Token))
            {
                Debug.WriteLine("User deserialization failed or token is null.");
            }
            else
            {
                Debug.WriteLine($"Setting secure storage... Token: {user.Token}, Nickname: {user.Nickname}, Email: {user.Email}, ID: {user._id}");
                await SecureStorage.SetAsync("userToken", user.Token);
                await SecureStorage.SetAsync("userNickname", user.Nickname);
                await SecureStorage.SetAsync("userEmail", user.Email);
                await SecureStorage.SetAsync("userID", user._id);
                Debug.WriteLine("Login successful. Token:" + user.Token + ", Name:" + user.Nickname + ", Email:" + user.Email + ", ID:" + user._id);
                return user;
            }
        }
        // If login fails, return null
        Debug.WriteLine("Login failed.");
        return null;
    }

    // Logout user
    public void Logout()
    {
        SecureStorage.Remove("userToken");
        SecureStorage.Remove("userNickname");
        SecureStorage.Remove("userEmail");
        SecureStorage.Remove("userID");
        Debug.WriteLine("User logged out.");
    }

    // Check if the user is logged in by verifying if the token exists
    public async Task<bool> IsLoggedInAsync()
    {
        try
        {
            var token = await SecureStorage.GetAsync("userToken");
            return !string.IsNullOrEmpty(token);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error checking login status: {ex.Message}");
            return false;
        }
    }

    // Get user details from secure storage
    public async Task<DataModel.User> GetStoredUserAsync()
    {
        try
        {
            var token = await SecureStorage.GetAsync("userToken");
            var nickname = await SecureStorage.GetAsync("userNickname");
            var email = await SecureStorage.GetAsync("userEmail");
            var id = await SecureStorage.GetAsync("userID");

            if (!string.IsNullOrEmpty(token) && !string.IsNullOrEmpty(nickname) && !string.IsNullOrEmpty(email) && !string.IsNullOrEmpty(id))
            {
                return new DataModel.User
                {
                    Token = token,
                    Nickname = nickname,
                    Email = email,
                    _id = id
                };
            }
            return null; // User is not logged in or missing data
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error retrieving stored user: {ex.Message}");
            return null;
        }
    }

    // Get user subscription data from backend
    public async Task<DataModel.UserSubscription> GetUserSubscriptionAsync()
    {
        try
        {
            var token = await SecureStorage.GetAsync("userToken");
            if (string.IsNullOrEmpty(token))
            {
                Debug.WriteLine("No token found for subscription request");
                return null;
            }

            var request = new HttpRequestMessage(HttpMethod.Get, BaseUrl + "subscription");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.SendAsync(request);
            Debug.WriteLine($"Subscription request URL: {BaseUrl}subscription");
            Debug.WriteLine($"Subscription response status: {response.StatusCode}");

            var responseContent = await response.Content.ReadAsStringAsync();
            Debug.WriteLine($"Subscription response content: {responseContent}");

            if (response.IsSuccessStatusCode)
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var subscription = JsonSerializer.Deserialize<DataModel.UserSubscription>(responseContent, options);
                Debug.WriteLine($"Subscription parsed - Plan: {subscription?.SubscriptionPlan}");
                return subscription;
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                await HandleTokenExpiration();
                throw new UnauthorizedAccessException("Your session has expired. Please log in again.");
            }

            Debug.WriteLine($"Subscription request failed: {response.StatusCode}");
            return null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error fetching subscription: {ex.Message}");
            return null;
        }
    }

    // Get user usage data from backend
    public async Task<DataModel.UserUsage> GetUserUsageAsync()
    {
        try
        {
            var token = await SecureStorage.GetAsync("userToken");
            if (string.IsNullOrEmpty(token))
            {
                Debug.WriteLine("No token found for usage request");
                return null;
            }

            var request = new HttpRequestMessage(HttpMethod.Get, BaseUrl + "usage");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.SendAsync(request);
            Debug.WriteLine($"Usage request URL: {BaseUrl}usage");
            Debug.WriteLine($"Usage response status: {response.StatusCode}");

            var responseContent = await response.Content.ReadAsStringAsync();
            Debug.WriteLine($"Usage response content: {responseContent}");

            if (response.IsSuccessStatusCode)
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var usage = JsonSerializer.Deserialize<DataModel.UserUsage>(responseContent, options);
                Debug.WriteLine($"Usage parsed - Available Credits: ${usage?.AvailableCredits:F4}");
                return usage;
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                await HandleTokenExpiration();
                throw new UnauthorizedAccessException("Your session has expired. Please log in again.");
            }

            Debug.WriteLine($"Usage request failed: {response.StatusCode}");
            return null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error fetching usage: {ex.Message}");
            return null;
        }
    }

    // Get user storage data from backend
    public async Task<DataModel.UserStorage> GetUserStorageAsync()
    {
        try
        {
            var token = await SecureStorage.GetAsync("userToken");
            if (string.IsNullOrEmpty(token))
            {
                Debug.WriteLine("No token found for storage request");
                return null;
            }

            var request = new HttpRequestMessage(HttpMethod.Get, BaseUrl + "storage");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.SendAsync(request);
            Debug.WriteLine($"Storage request URL: {BaseUrl}storage");
            Debug.WriteLine($"Storage response status: {response.StatusCode}");

            var responseContent = await response.Content.ReadAsStringAsync();
            Debug.WriteLine($"Storage response content: {responseContent}");

            if (response.IsSuccessStatusCode)
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var storage = JsonSerializer.Deserialize<DataModel.UserStorage>(responseContent, options);
                Debug.WriteLine($"Storage parsed - Total Used: {storage?.TotalStorageFormatted}");
                return storage;
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                await HandleTokenExpiration();
                throw new UnauthorizedAccessException("Your session has expired. Please log in again.");
            }

            Debug.WriteLine($"Storage request failed: {response.StatusCode}");
            return null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error fetching storage: {ex.Message}");
            return null;
        }
    }

    // Send password reset email for authenticated user
    public async Task<bool> SendPasswordResetAsync()
    {
        try
        {
            var token = await SecureStorage.GetAsync("userToken");
            if (string.IsNullOrEmpty(token))
            {
                Debug.WriteLine("No token found for password reset request");
                return false;
            }

            var request = new HttpRequestMessage(HttpMethod.Post, BaseUrl + "forgot-password-authenticated");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            request.Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);
            Debug.WriteLine($"Password reset request URL: {BaseUrl}forgot-password-authenticated");
            Debug.WriteLine($"Password reset response status: {response.StatusCode}");

            var responseContent = await response.Content.ReadAsStringAsync();
            Debug.WriteLine($"Password reset response content: {responseContent}");

            if (response.IsSuccessStatusCode)
            {
                Debug.WriteLine("Password reset email sent successfully");
                return true;
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                await HandleTokenExpiration();
                throw new UnauthorizedAccessException("Your session has expired. Please log in again.");
            }

            Debug.WriteLine($"Password reset request failed: {response.StatusCode}");
            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error sending password reset: {ex.Message}");
            return false;
        }
    }    // Handle responses, ensure success or handle error
    private async Task<T> HandleResponse<T>(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            var responseContent = await response.Content.ReadAsStringAsync();
            Debug.WriteLine("Length of responseContent:" + responseContent.Length.ToString());
            // Debug.WriteLine($"1 (DataService.HandleResponse) Raw response data: {responseContent}");
            return JsonSerializer.Deserialize<T>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        else
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            Debug.WriteLine($"Error: {errorContent}");

            // Check for unauthorized/token expired
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                Debug.WriteLine("Token expired detected in HandleResponse");
                await HandleTokenExpiration();
                throw new UnauthorizedAccessException("Your session has expired. Please log in again.");
            }

            throw new HttpRequestException($"Request failed: {response.StatusCode}");
        }
    }

    // Helper method to check if an error is related to token expiration
    private bool IsTokenExpiredError(HttpRequestException ex)
    {
        // Check if the exception message indicates token expiration
        return ex.Message.Contains("Unauthorized") ||
               ex.Message.Contains("401") ||
               (ex.StatusCode.HasValue && ex.StatusCode.Value == System.Net.HttpStatusCode.Unauthorized);
    }

    // Handle token expiration by logging out and redirecting to login page
    private async Task HandleTokenExpiration()
    {
        Debug.WriteLine("Handling token expiration - logging out user");

        // Log the user out
        Logout();

        // Navigate to login page on the main thread
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try
            {
                // Display message to user
                await Application.Current.MainPage.DisplayAlert(
                    "Session Expired",
                    "Your session has expired. Please log in again.",
                    "OK");

                // Navigate to login page
                await Shell.Current.GoToAsync("///login");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during navigation after token expiration: {ex.Message}");
            }
        });
    }
}
