using Microsoft.Maui.Storage;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;

namespace CSimple.Pages;

public partial class GoalPage : ContentPage
{
    public bool ShowNewGoal { get; set; } = false;
    public bool ShowMyGoals { get; set; } = false;
    public string NewGoalText { get; set; } = string.Empty;
    public ObservableCollection<string> MyGoals { get; set; } = new ObservableCollection<string>();
    public string CreateGoalButtonText => ShowNewGoal ? "Cancel Goal" : "Create Goal";
    public string MyGoalsButtonText => ShowMyGoals ? "Hide Goals" : "My Goals";
    public ICommand ToggleCreateGoalCommand { get; }
    public ICommand ToggleMyGoalsCommand { get; }
    public ICommand SubmitGoalCommand { get; }
    public GoalPage()
    {
        InitializeComponent();
        // Initialize Commands
        ToggleCreateGoalCommand = new Command(OnToggleCreateGoal);
        ToggleMyGoalsCommand = new Command(OnToggleMyGoals);
        SubmitGoalCommand = new Command(OnSubmitGoal);
        // Bind the context
        BindingContext = this;
        CheckUserLoggedIn();
    }

    private async void CheckUserLoggedIn()
    {
        if (!await IsUserLoggedInAsync())
        {
            Debug.WriteLine("User is not logged in, navigating to login...");
            NavigateLogin();
        }
        else
        {
            Debug.WriteLine("User is logged in.");
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
            Debug.WriteLine($"Error navigating to login: {ex.Message}");
        }
    }

    private async Task<bool> IsUserLoggedInAsync()
    {
        try
        {
            // Retrieve stored token
            var userToken = await SecureStorage.GetAsync("userToken");

            // Check if token exists and is not empty
            if (!string.IsNullOrEmpty(userToken))
            {
                Debug.WriteLine("User token found: " + userToken);
                return true; // User is logged in
            }
            else
            {
                Debug.WriteLine("No user token found.");
                return false; // User is not logged in
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error retrieving user token: {ex.Message}");
            return false;
        }
    }
    private void OnToggleCreateGoal()
    {
        ShowNewGoal = !ShowNewGoal;
        OnPropertyChanged(nameof(ShowNewGoal));
        OnPropertyChanged(nameof(CreateGoalButtonText));
    }
    private void OnToggleMyGoals()
    {
        ShowMyGoals = !ShowMyGoals;
        OnPropertyChanged(nameof(ShowMyGoals));
        OnPropertyChanged(nameof(MyGoalsButtonText));
    }
    private void OnSubmitGoal()
    {
        if (!string.IsNullOrWhiteSpace(NewGoalText))
        {
            MyGoals.Add(NewGoalText);
            NewGoalText = string.Empty;
            OnPropertyChanged(nameof(NewGoalText));
        }
    }
    protected override void OnAppearing()
    {
        base.OnAppearing();

        // Fetch data (like goals) here if needed
        // For example:
        // LoadMyGoals();
    }
    // Placeholder for fetching data
    private void LoadMyGoals()
    {
        MyGoals.Add("Sample Goal 1");
        MyGoals.Add("Sample Goal 2");
        OnPropertyChanged(nameof(MyGoals));
    }
}
