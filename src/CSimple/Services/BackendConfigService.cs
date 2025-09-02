using System.Diagnostics;

namespace CSimple.Services
{
    public class BackendConfigService
    {
        public enum Environment
        {
            Development,
            Production
        }

        public static Environment CurrentEnvironment =>
#if DEBUG
            Environment.Development;
#else
            Environment.Production;
#endif

        public static class ApiEndpoints
        {
            // Development - Local backend
            public const string DevelopmentBaseUrl = "http://localhost:5000/api/data/";

            // Production - Render backend 
            // TODO: Update this with your actual Render deployment URL
            public const string ProductionBaseUrl = "https://mern-plan-web-service.onrender.com/api/data/";

            // Alternative production URLs (uncomment the correct one):
            // public const string ProductionBaseUrl = "https://your-app-name.onrender.com/api/data/";
            // public const string ProductionBaseUrl = "https://sthopwood-backend.onrender.com/api/data/";

            public static string GetBaseUrl()
            {
                var baseUrl = CurrentEnvironment == Environment.Development
                    ? DevelopmentBaseUrl
                    : ProductionBaseUrl;

                Debug.WriteLine($"[BackendConfig] Environment: {CurrentEnvironment}");
                Debug.WriteLine($"[BackendConfig] Base URL: {baseUrl}");

                return baseUrl;
            }

            public static string GetHealthUrl()
            {
                var baseUrl = GetBaseUrl();
                return baseUrl.Replace("/api/data/", "/health");
            }

            public static string GetRootUrl()
            {
                var baseUrl = GetBaseUrl();
                return baseUrl.Replace("/api/data/", "/");
            }

            public static string GetPublicApiUrl()
            {
                var baseUrl = GetBaseUrl();
                return baseUrl + "public";
            }
        }

        /// <summary>
        /// Test if the backend is reachable and properly configured
        /// </summary>
        public static async Task<(bool IsReachable, string Details)> TestBackendAsync()
        {
            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(10);

                var details = new List<string>();
                var baseUrl = ApiEndpoints.GetBaseUrl();

                details.Add($"🔧 Environment: {CurrentEnvironment}");
                details.Add($"🌐 Backend URL: {baseUrl}");
                details.Add("");

                // Test health endpoint
                try
                {
                    var healthUrl = ApiEndpoints.GetHealthUrl();
                    var healthResponse = await client.GetAsync(healthUrl);
                    // details.Add($"✅ Health check (Status: {healthResponse.StatusCode})");

                    var healthContent = await healthResponse.Content.ReadAsStringAsync();
                    if (healthContent.TrimStart().StartsWith("{"))
                    {
                        details.Add("   ✅ Backend is running and returning JSON");
                    }
                    else
                    {
                        details.Add("   ❌ Backend returning HTML - routing/deployment issue");
                    }
                }
                catch (Exception ex)
                {
                    details.Add($"❌ Health check failed: {ex.Message}");
                    if (CurrentEnvironment == Environment.Development)
                    {
                        details.Add("   💡 Is your local backend running? (npm start)");
                    }
                    else
                    {
                        details.Add("   💡 Check Render deployment status");
                    }
                }

                return (true, string.Join("\n", details));
            }
            catch (Exception ex)
            {
                return (false, $"❌ Backend test failed: {ex.Message}");
            }
        }
    }
}
