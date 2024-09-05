using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using System.Diagnostics;

namespace CSimple.ViewModels;

public class LoginViewModel : BaseViewModel
{
    private string username;
    private string password;
    public string Username
    {
        get => username;
        set
        {
            username = value;
            OnPropertyChanged();
        }
    }
    public string Password
    {
        get => password;
        set
        {
            password = value;
            OnPropertyChanged();
        }
    }
    public ICommand LoginCommand { get; }
    public LoginViewModel()
    {
        LoginCommand = new Command(OnLogin);
    }
    private void OnLogin()
    {
        Debug.WriteLine($"Login attempt: Username = {Username}, Password = {Password}");
        // Here you could add real authentication logic or navigate to another page
    }
}
