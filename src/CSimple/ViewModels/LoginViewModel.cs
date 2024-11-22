using System.Diagnostics;
using System.Windows.Input;
namespace CSimple.ViewModels
{
    public class LoginViewModel : BaseViewModel
    {
        private readonly DataService _dataService; // AuthService instance for login/logout
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
        public LoginViewModel(DataService dataService)
        {
            _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
            LoginCommand = new Command(async () => await ExecuteLogin());
        }

        // Login logic
        private async Task ExecuteLogin()
        {
            if (IsBusy)
                return;

            IsBusy = true;

            try
            {
                Debug.WriteLine("Calling login...");
                var user = await _dataService.LoginAsync(Email, Password);
                Debug.WriteLine("User: " + user);

                if (user != null)
                {
                    IsLoggedIn = true;
                    await Shell.Current.GoToAsync($"///home");
                }
                else
                {
                    IsLoggedIn = false;
                    await Application.Current.MainPage.DisplayAlert("Error", "Invalid email or password.", "OK");
                }
            }
            catch (Exception ex)
            {
                // Handle any unexpected errors
                Debug.WriteLine($"Login error: {ex.Message}");
                await Application.Current.MainPage.DisplayAlert("Error", "Something went wrong. Try again later.", "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }

        // Logout logic
        public void Logout()
        {
            _dataService.Logout();
            IsLoggedIn = false;
            Shell.Current.GoToAsync($"///login");
        }
    }
}