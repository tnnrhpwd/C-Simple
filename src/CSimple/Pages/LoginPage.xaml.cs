using CSimple.ViewModels;

namespace CSimple.Pages;

public partial class LoginPage : ContentPage
{
    public LoginPage()
    {
        InitializeComponent();
        BindingContext = new LoginViewModel(); // Set the BindingContext to LoginViewModel
    }
}
