using System.Windows.Input;
using Microsoft.Maui.Controls;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using CSimple.ViewModels;
namespace CSimple.ViewModels
{
    public class LoginViewModel : BaseViewModel
    {
        private readonly AuthService _authService; // AuthService instance for login/logout
        private string _email;
        private string _password;
        private bool _isLoggedIn;
        private bool _isBusy;  // IsBusy property to track activity state

        // Email property
        public string Email
        {
            get => _email;
            set
            {
                _email = value;
                OnPropertyChanged();
            }
        }

        // Password property
        public string Password
        {
            get => _password;
            set
            {
                _password = value;
                OnPropertyChanged();
            }
        }

        // IsLoggedIn property
        public bool IsLoggedIn
        {
            get => _isLoggedIn;
            set
            {
                _isLoggedIn = value;
                OnPropertyChanged();
            }
        }

        // IsBusy property to indicate when an operation is running
        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                _isBusy = value;
                OnPropertyChanged();
            }
        }

        // Command for Login action
        public ICommand LoginCommand { get; }

        // Constructor to initialize AuthService and setup commands
        public LoginViewModel()
        {
            _authService = new AuthService();
            LoginCommand = new Command(async () => await ExecuteLogin());
        }

        // Login logic
        private async Task ExecuteLogin()
        {
            if (IsBusy)
                return;

            IsBusy = true;  // Set IsBusy to true during login operation

            var success = await _authService.LoginAsync(Email, Password);

            if (success)
            {
                IsLoggedIn = true;
                await Shell.Current.GoToAsync($"///home");  // Navigate to homepage on successful login
            }
            else
            {
                IsLoggedIn = false;
                await Application.Current.MainPage.DisplayAlert("Error", "Login failed. Try again.", "OK");
            }

            IsBusy = false;  // Reset IsBusy after operation
        }
        
        // Logout logic
        public void Logout()
        {
            _authService.Logout();
            IsLoggedIn = false;
            Shell.Current.GoToAsync($"///login");
        }
    }
}