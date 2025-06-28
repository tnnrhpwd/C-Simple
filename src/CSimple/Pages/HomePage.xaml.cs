using CSimple.ViewModels;
using System.Diagnostics;
using System.Windows.Input;
using Microsoft.Maui.Graphics;
using CSimple.Services;
using System.ComponentModel;

namespace CSimple.Pages;

public partial class HomePage : ContentPage, INotifyPropertyChanged
{
    private readonly DataService _dataService; // AuthService instance for login/logout
    private readonly AppModeService _appModeService;
    private readonly VoiceAssistantService _voiceAssistantService;
    private bool _isVoiceAssistantActive = false;
    private float _voiceLevel = 0;
    static bool isSetup = false;
    public ICommand NavigateCommand { get; set; }
    public ICommand LogoutCommand { get; }
    public ICommand ToggleVoiceAssistantCommand { get; }

    // Voice assistant properties
    public bool IsVoiceAssistantActive
    {
        get => _isVoiceAssistantActive;
        set
        {
            if (_isVoiceAssistantActive != value)
            {
                _isVoiceAssistantActive = value;
                OnPropertyChanged(nameof(IsVoiceAssistantActive));
                OnPropertyChanged(nameof(VoiceAssistantStatus));
                OnPropertyChanged(nameof(VoiceAssistantIcon));
            }
        }
    }

    public float VoiceLevel
    {
        get => _voiceLevel;
        set
        {
            if (_voiceLevel != value)
            {
                _voiceLevel = value;
                OnPropertyChanged(nameof(VoiceLevel));
            }
        }
    }

    public string VoiceAssistantStatus => IsVoiceAssistantActive ? "Voice Assistant Active" : "Voice Assistant Inactive";
    public string VoiceAssistantIcon => IsVoiceAssistantActive ? "mic_active.png" : "mic_inactive.png";

    // New properties to showcase AI capabilities
    public bool IsAIEnabled { get; set; } = true;
    public string ActiveAIStatus { get; set; } = "AI Assistant Active";
    public string AIStatusDetail { get; set; } = "Monitoring inputs and providing assistance";
    public int ActiveModelsCount { get; set; } = 2;
    public int TodayActionsCount { get; set; } = 15;
    public double SuccessRate { get; set; } = 0.92;
    public double SystemHealthPercentage { get; set; } = 0.87;
    public string SystemHealthStatus { get; set; } = "Systems nominal, resources optimized";
    public Color SystemHealthColor => SystemHealthPercentage > 0.7 ? Colors.Green : SystemHealthPercentage > 0.4 ? Colors.Orange : Colors.Red;
    public double AverageAIAccuracy { get; set; } = 0.89;

    public bool IsOnlineMode
    {
        get => _appModeService.CurrentMode == AppMode.Online;
        set
        {
            _appModeService.CurrentMode = value ? AppMode.Online : AppMode.Offline;
            OnPropertyChanged(nameof(AppModeLabel));
        }
    }

    public string AppModeLabel => IsOnlineMode ? "Online Mode" : "Offline Mode";

    // Commands for AI features
    public ICommand RefreshDashboardCommand { get; }
    public ICommand CreateNewGoalCommand { get; }
    public ICommand NavigateToObserveCommand { get; }
    public ICommand TrainModelCommand { get; }
    public ICommand DiscoverSharedGoalsCommand { get; }

    // Constructor with service injection
    public HomePage(HomeViewModel vm, DataService dataService, AppModeService appModeService, VoiceAssistantService voiceAssistantService)
    {
        try
        {
            InitializeComponent();
            _dataService = dataService;
            _appModeService = appModeService;
            _voiceAssistantService = voiceAssistantService;
            LogoutCommand = new Command(ExecuteLogout);
            ToggleVoiceAssistantCommand = new Command(ExecuteToggleVoiceAssistant);

            // Bind to AppModeService changes
            _appModeService.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(AppModeService.CurrentMode))
                {
                    OnPropertyChanged(nameof(IsOnlineMode));
                    OnPropertyChanged(nameof(AppModeLabel));
                }
            };

            // Subscribe to voice assistant events
            _voiceAssistantService.AudioLevelChanged += OnVoiceAssistantAudioLevelChanged;
            _voiceAssistantService.ListeningStateChanged += OnVoiceAssistantListeningStateChanged;
            _voiceAssistantService.DebugMessageLogged += OnVoiceAssistantDebugMessage;
            _voiceAssistantService.TranscriptionCompleted += OnVoiceAssistantTranscriptionCompleted;
            _voiceAssistantService.ActionExecuted += OnVoiceAssistantActionExecuted;

            // Initialize commands
            RefreshDashboardCommand = new Command(RefreshDashboard);
            CreateNewGoalCommand = new Command(async () => await Shell.Current.GoToAsync("///goal"));
            NavigateToObserveCommand = new Command(async () => await Shell.Current.GoToAsync("///observe"));
            TrainModelCommand = new Command(async () => await Shell.Current.GoToAsync("///orient"));
            DiscoverSharedGoalsCommand = new Command(ShowSharedGoalsPopup);

            BindingContext = this;
            if (!isSetup)
            {
                isSetup = true;
                SetupAppActions();
                SetupTrayIcon();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in HomePage constructor: {ex.Message}");
        }
    }

    private void ExecuteToggleVoiceAssistant()
    {
        if (_voiceAssistantService != null)
        {
            // When AI is toggled, enable/disable voice assistant
            _voiceAssistantService.ToggleEnabled(IsAIEnabled);

            if (IsAIEnabled && !IsVoiceAssistantActive)
            {
                // If AI is enabled and voice assistant is not active, start listening
                _voiceAssistantService.StartListening();
            }
            else if (IsVoiceAssistantActive)
            {
                // If voice assistant is active, stop listening
                _voiceAssistantService.StopListening();
            }
        }
    }

    private void OnVoiceAssistantAudioLevelChanged(float level)
    {
        // Update UI on main thread
        Dispatcher.Dispatch(() =>
        {
            VoiceLevel = level;
        });
    }

    private void OnVoiceAssistantListeningStateChanged(bool isListening)
    {
        // Update UI on main thread
        Dispatcher.Dispatch(() =>
        {
            IsVoiceAssistantActive = isListening;
        });
    }

    private void OnVoiceAssistantDebugMessage(string message)
    {
        Debug.WriteLine($"[VoiceAssistant] {message}");

        // Update status detail
        Dispatcher.Dispatch(() =>
        {
            AIStatusDetail = message;
        });
    }

    private void OnVoiceAssistantTranscriptionCompleted(string text)
    {
        Dispatcher.Dispatch(() =>
        {
            // Update UI to show transcription
            AIStatusDetail = $"Transcription: {text}";
        });
    }

    private void OnVoiceAssistantActionExecuted(string action, bool success)
    {
        Dispatcher.Dispatch(() =>
        {
            // Update UI to show action result
            AIStatusDetail = success ? $"Action executed: {action}" : $"Action failed: {action}";
        });
    }

    // Existing methods...

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await Initialize();

        // Refresh dashboard stats on appearing
        RefreshDashboard();

        // Watch for AI toggle changes
        OnPropertyChanged(nameof(IsAIEnabled));
    }

    // Watch for AI toggle changes
    protected override void OnPropertyChanged(string propertyName)
    {
        base.OnPropertyChanged(propertyName);

        if (propertyName == nameof(IsAIEnabled))
        {
            // When AI is toggled, enable/disable voice assistant
            if (_voiceAssistantService != null)
            {
                _voiceAssistantService.ToggleEnabled(IsAIEnabled);

                // Update UI
                ActiveAIStatus = IsAIEnabled ? "AI Assistant Active" : "AI Assistant Inactive";
                OnPropertyChanged(nameof(ActiveAIStatus));
            }
        }
    }

    private void RefreshDashboard()
    {
        // Simulate refreshing system stats
        SystemHealthPercentage = new Random().NextDouble() * 0.3 + 0.7; // Between 0.7 and 1.0
        ActiveModelsCount = new Random().Next(1, 5);
        TodayActionsCount = new Random().Next(10, 30);
        SuccessRate = new Random().NextDouble() * 0.2 + 0.8; // Between 0.8 and 1.0
        AverageAIAccuracy = new Random().NextDouble() * 0.15 + 0.85; // Between 0.85 and 1.0

        // Update bindings
        OnPropertyChanged(nameof(SystemHealthPercentage));
        OnPropertyChanged(nameof(ActiveModelsCount));
        OnPropertyChanged(nameof(TodayActionsCount));
        OnPropertyChanged(nameof(SuccessRate));
        OnPropertyChanged(nameof(AverageAIAccuracy));
        OnPropertyChanged(nameof(SystemHealthColor));
    }

    private async void ShowSharedGoalsPopup()
    {
        await DisplayAlert("Shared Goals", "Discovering goals shared by other users. This feature allows you to download pre-trained models for specific tasks.", "OK");
    }

    private async Task Initialize()
    {
        await Task.Run(() =>
        {
            // if (!await IsLoggedInAsync())
            // if (true)
            // {
            //     Debug.WriteLine("Navigating...");
            //     NavigateLogin();
            // }
        });
    }
    async void NavigateLogin()
    {
        try
        {
            await Shell.Current.GoToAsync($"///login");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Navigation error: {ex.Message}");
        }
    }
    private void ExecuteLogout()
    {
        try
        {
            _dataService.Logout();
            Shell.Current.GoToAsync($"///login");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Logout error: {ex.Message}");
        }
    }
    private async Task<bool> IsUserLoggedIn()
    {
        return await _dataService.IsLoggedInAsync();
    }
    private void SetupAppActions()
    {
        try
        {
#if WINDOWS
#endif
            AppActions.SetAsync(
                new AppAction("current_info", "Check Current Weather", icon: "current_info"),
                new AppAction("add_location", "Add a Location", icon: "add_location")
            );
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"App Actions not supported: {ex.Message}");
        }
    }
    private void SetupTrayIcon()
    {
        var trayService = ServiceProvider.GetService<ITrayService>();

        if (trayService != null)
        {
            trayService.Initialize();
            trayService.ClickHandler = () =>
            {
                Debug.WriteLine("Tray icon clicked - bringing window to front");
                WindowExtensions.BringToFront();
            };
        }
    }
    private async void OnGetStartedClicked(object sender, EventArgs e)
    {
        try
        {
            Debug.WriteLine("Get Started button clicked, attempting navigation to action page");
            await Shell.Current.GoToAsync("///action");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Navigation error in Get Started button click: {ex.Message}");
            // Fallback approach if the first one fails
            try
            {
                await Shell.Current.GoToAsync($"///{nameof(ActionPage)}");
            }
            catch (Exception innerEx)
            {
                Debug.WriteLine($"Fallback navigation also failed: {innerEx.Message}");
            }
        }
    }
}
