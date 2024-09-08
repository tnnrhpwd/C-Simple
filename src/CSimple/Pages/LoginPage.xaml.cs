using CSimple.ViewModels;

namespace CSimple.Pages;

public partial class LoginPage : ContentPage
{
    public LoginPage(DataService dataService)
    {
        InitializeComponent();
        BindingContext = new LoginViewModel(dataService);
    }
}
