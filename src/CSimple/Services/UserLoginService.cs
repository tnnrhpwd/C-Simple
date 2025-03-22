using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;

namespace CSimple.Services
{
    public class UserLoginService
    {
        private readonly UserService _userService;

        public UserLoginService()
        {
            _userService = new UserService();
        }

        public async Task CheckUserLoggedInAsync()
        {
            if (!await _userService.IsUserLoggedInAsync())
            {
                Debug.WriteLine("User is not logged in, navigating to login...");
                await _userService.NavigateLoginAsync();
            }
            if (await _userService.IsUserLoggedInAsync())
            {
                Debug.WriteLine("User is logged in.");
            }
            else
            {
                Debug.WriteLine("User is not logged in, navigating to login...");
                await _userService.NavigateLoginAsync();
            }
        }
    }
}
