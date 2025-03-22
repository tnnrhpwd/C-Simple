using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;

namespace CSimple.Services
{
    public class UserService
    {
        public async Task<bool> IsUserLoggedInAsync()
        {
            try
            {
                var userToken = await SecureStorage.GetAsync("userToken");

                if (!string.IsNullOrEmpty(userToken))
                {
                    Debug.WriteLine("User token found: " + userToken);
                    return true;
                }
                else
                {
                    Debug.WriteLine("No user token found.");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error retrieving user token: {ex.Message}");
                return false;
            }
        }

        public async Task NavigateLoginAsync()
        {
            try
            {
                await Shell.Current.GoToAsync($"///login");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error navigating to login: {ex.Message}");
            }
        }
    }
}
