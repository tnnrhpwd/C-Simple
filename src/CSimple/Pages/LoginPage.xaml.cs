using CSimple.ViewModels;

namespace CSimple.Pages;

public partial class LoginPage : ContentPage
{
    public LoginPage(DataService dataService)
    {
        InitializeComponent();
        BindingContext = new LoginViewModel(dataService);
    }

    private void OnEntryCompleted(object sender, EventArgs e)
    {
        // Validate input fields before attempting login
        if (!ValidateInputFields())
        {
            return; // Don't proceed with login if validation fails
        }

        // Execute the login command when Enter is pressed in any entry field
        if (BindingContext is LoginViewModel loginViewModel && loginViewModel.LoginCommand.CanExecute(null))
        {
            loginViewModel.LoginCommand.Execute(null);

            // Clear the password field after login attempt for security
            ClearPasswordField();
        }
    }

    private bool ValidateInputFields()
    {
        if (BindingContext is LoginViewModel loginViewModel)
        {
            // Check if username/email field is empty
            if (string.IsNullOrWhiteSpace(loginViewModel.Email))
            {
                DisplayAlert("Validation Error", "Please fill in the Username or Email field", "OK");
                return false;
            }

            // Check if password field is empty
            if (string.IsNullOrWhiteSpace(loginViewModel.Password))
            {
                DisplayAlert("Validation Error", "Please fill in the Password field", "OK");
                return false;
            }
        }

        return true;
    }

    private void ClearPasswordField()
    {
        try
        {
            // Clear the password entry field
            PasswordEntry.Text = string.Empty;

            // Also clear the ViewModel's Password property if accessible
            if (BindingContext is LoginViewModel loginViewModel)
            {
                loginViewModel.Password = string.Empty;
            }
        }
        catch (Exception ex)
        {
            // Log error but don't crash the app
            System.Diagnostics.Debug.WriteLine($"Error clearing password field: {ex.Message}");
        }
    }
}
