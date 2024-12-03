using Microsoft.Maui.Storage;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;
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
    private readonly DataService _dataService;
    private readonly FileService _fileService;

    public GoalPage()
    {
        InitializeComponent();
        // Initialize Commands
        ToggleCreateGoalCommand = new Command(OnToggleCreateGoal);
        ToggleMyGoalsCommand = new Command(OnToggleMyGoals);
        SubmitGoalCommand = new Command(OnSubmitGoal);
        // Initialize services
        _dataService = new DataService();
        _fileService = new FileService();
        // Bind the context
        BindingContext = this;
        CheckUserLoggedIn();
        // Load goals from file
        _ = LoadGoalsFromFile();
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
            await LoadGoalsFromBackend();
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
    private async void OnSubmitGoal()
    {
        if (!string.IsNullOrWhiteSpace(NewGoalText))
        {
            MyGoals.Add(NewGoalText);
            await SaveGoalsToFile();
            await SaveGoalToBackend(NewGoalText);
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
    private async Task LoadGoalsFromBackend()
    {
        try
        {
            var token = await SecureStorage.GetAsync("userToken");
            if (string.IsNullOrEmpty(token))
            {
                Debug.WriteLine("User is not logged in.");
                return;
            }

            var data = "Goal";
            var goals = await _dataService.GetDataAsync(data, token);
            var formattedGoals = FormatGoalsFromBackend(goals.Data);

            MyGoals.Clear();
            foreach (var goal in formattedGoals)
            {
                MyGoals.Add(goal);
            }

            await SaveGoalsToFile();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading goals from backend: {ex.Message}");
        }
    }

    private ObservableCollection<string> FormatGoalsFromBackend(IEnumerable<string> goalStrings)
    {
        var formattedGoals = new ObservableCollection<string>();

        foreach (var goalString in goalStrings)
        {
            if (goalString.Contains("|Goal"))
            {
                formattedGoals.Add(goalString);
            }
        }

        return formattedGoals;
    }

    private async Task SaveGoalToBackend(string goal)
    {
        try
        {
            var token = await SecureStorage.GetAsync("userToken");
            if (string.IsNullOrEmpty(token))
            {
                Debug.WriteLine("User is not logged in.");
                return;
            }

            var data = $"Goal:{goal}";
            var response = await _dataService.CreateDataAsync(data, token);
            if (response.DataIsSuccess)
            {
                Debug.WriteLine("Goal saved to backend");
            }
            else
            {
                Debug.WriteLine("Failed to save goal to backend");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error saving goal to backend: {ex.Message}");
        }
    }

    private async Task SaveGoalsToFile()
    {
        try
        {
            await _fileService.SaveGoalsAsync(MyGoals);
            Debug.WriteLine("Goals saved to file");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error saving goals to file: {ex.Message}");
        }
    }

    private async Task LoadGoalsFromFile()
    {
        try
        {
            var loadedGoals = await _fileService.LoadGoalsAsync();
            MyGoals.Clear();
            foreach (var goal in loadedGoals)
            {
                MyGoals.Add(goal);
            }
            Debug.WriteLine("Goals loaded from file");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading goals from file: {ex.Message}");
        }
    }
}
