using CSimple.ViewModels;
using System.Diagnostics;

namespace CSimple.Pages;

public partial class GoalPage : ContentPage
{
    public GoalPage()
    {
        InitializeComponent();
        if (!IsUserLoggedIn())
        {
            Debug.WriteLine("Navigating...");
            NavigateLogin();
        }
    }
    async void NavigateLogin()
    {
        try
        {
            await Shell.Current.GoToAsync($"///login");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"err: {ex.Message}");
        }
    }
    private bool IsUserLoggedIn()
    {
        // Logic to check if the user is logged in
        return false;
    }
}
