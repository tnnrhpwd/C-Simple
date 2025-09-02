using CSimple.ViewModels;
using System.Diagnostics;
using System.Windows.Input;
using Microsoft.Maui.Graphics;
using CSimple.Services;
using System.ComponentModel;
using Microsoft.Maui.Storage;

namespace CSimple.Pages;

public partial class HomePage : ContentPage, INotifyPropertyChanged
{
    private readonly DataService _dataService; // AuthService instance for login/logout
    private readonly AppModeService _appModeService;
    private readonly VoiceAssistantService _voiceAssistantService;
    private bool _isVoiceAssistantActive = false;
    private float _voiceLevel = 0;
    private DateTime _sessionStartTime = DateTime.Now;
    private DateTime _lastAIStateChangeTime = DateTime.Now;
    private TimeSpan _totalActiveTime = TimeSpan.Zero;
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
    private bool _isAIEnabled = false; // Default to false
    public bool IsAIEnabled
    {
        get => _isAIEnabled;
        set
        {
            if (_isAIEnabled != value)
            {
                // Track utilization when AI state changes
                UpdateUtilizationTracking(value);

                _isAIEnabled = value;
                OnPropertyChanged(nameof(IsAIEnabled));

                // Save state to persistent storage
                Task.Run(async () => await SaveAIEnabledStateAsync(value));

                // Synchronize with NetPageViewModel's IsIntelligenceActive
                SynchronizeIntelligenceState(value);

                // Update UI status
                ActiveAIStatus = value ? "AI Assistant Active" : "AI Assistant Inactive";
                OnPropertyChanged(nameof(ActiveAIStatus));
            }
        }
    }
    public string ActiveAIStatus { get; set; } = "AI Assistant Inactive";
    public string AIStatusDetail { get; set; } = "AI Assistant ready to start";
    public int ActiveModelsCount { get; set; } = 2;
    public int TodayActionsCount { get; set; } = 15;
    public double Utilization { get; set; } = 0.0; // Represents percentage of time AI assistant is active
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

            // Start NetPage preload in the background immediately after construction
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(1000); // Small delay to let the UI finish initializing
                    await PreloadNetPageAsync();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Background NetPage preload error: {ex.Message}");
                }
            });
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

        // Load saved AI enabled state first, before other initialization
        var savedIsAIEnabled = await LoadAIEnabledStateAsync();
        _isAIEnabled = savedIsAIEnabled; // Set backing field directly to avoid triggering save
        OnPropertyChanged(nameof(IsAIEnabled));

        // Initialize utilization tracking based on loaded state
        _sessionStartTime = DateTime.Now;
        _lastAIStateChangeTime = DateTime.Now;
        _totalActiveTime = TimeSpan.Zero;
        CalculateUtilization(); // Initialize utilization display

        // Update initial status based on loaded state
        ActiveAIStatus = _isAIEnabled ? "AI Assistant Active" : "AI Assistant Inactive";
        AIStatusDetail = _isAIEnabled ? "Monitoring inputs and providing assistance" : "AI Assistant ready to start";
        OnPropertyChanged(nameof(ActiveAIStatus));
        OnPropertyChanged(nameof(AIStatusDetail));

        Debug.WriteLine($"HomePage: Loaded AI enabled state: {_isAIEnabled}");

        // Ensure app mode UI reflects the current persisted state
        OnPropertyChanged(nameof(IsOnlineMode));
        OnPropertyChanged(nameof(AppModeLabel));
        Debug.WriteLine($"HomePage: App mode loaded: {IsOnlineMode}");

        await Initialize();

        // Preload NetPage models and initialize for faster navigation
        await PreloadNetPageAsync();

        // Refresh dashboard stats on appearing
        RefreshDashboard();

        // Synchronize the NetPageViewModel after loading and set up bidirectional sync
        SynchronizeIntelligenceState(_isAIEnabled);
        SetupNetPageViewModelSync();
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
            }
        }
    }

    private void RefreshDashboard()
    {
        // Simulate refreshing system stats
        SystemHealthPercentage = new Random().NextDouble() * 0.3 + 0.7; // Between 0.7 and 1.0
        ActiveModelsCount = new Random().Next(1, 5);
        TodayActionsCount = new Random().Next(10, 30);
        // Calculate actual utilization based on tracking
        CalculateUtilization();
        AverageAIAccuracy = new Random().NextDouble() * 0.15 + 0.85; // Between 0.85 and 1.0

        // Update bindings
        OnPropertyChanged(nameof(SystemHealthPercentage));
        OnPropertyChanged(nameof(ActiveModelsCount));
        OnPropertyChanged(nameof(TodayActionsCount));
        // Utilization is updated in CalculateUtilization()
        OnPropertyChanged(nameof(AverageAIAccuracy));
        OnPropertyChanged(nameof(SystemHealthColor));
    }

    private void UpdateUtilizationTracking(bool isEnabled)
    {
        var now = DateTime.Now;

        // If AI was previously enabled, add to the total active time
        if (_isAIEnabled)
        {
            _totalActiveTime += now - _lastAIStateChangeTime;
        }

        _lastAIStateChangeTime = now;

        // Recalculate and update utilization
        CalculateUtilization();
    }

    private void CalculateUtilization()
    {
        var now = DateTime.Now;
        var totalSessionTime = now - _sessionStartTime;

        // Add current active time if AI is currently enabled
        var currentActiveTime = _totalActiveTime;
        if (_isAIEnabled)
        {
            currentActiveTime += now - _lastAIStateChangeTime;
        }

        // Calculate utilization as percentage
        if (totalSessionTime.TotalSeconds > 0)
        {
            Utilization = currentActiveTime.TotalSeconds / totalSessionTime.TotalSeconds;
        }
        else
        {
            Utilization = 0.0;
        }

        OnPropertyChanged(nameof(Utilization));
        Debug.WriteLine($"Utilization updated: {Utilization:P2} (Active: {currentActiveTime.TotalMinutes:F1}min / Total: {totalSessionTime.TotalMinutes:F1}min)");
    }

    private async Task PreloadNetPageAsync()
    {
        try
        {
            Debug.WriteLine("HomePage: Starting NetPage preload...");

            // Update UI to show we're starting initialization
            Dispatcher.Dispatch(() =>
            {
                AIStatusDetail = "Initializing AI models...";
            });

            // Get the NetPageViewModel from the app
            var app = Application.Current as App;
            var netPageViewModel = app?.NetPageViewModel;

            if (netPageViewModel != null)
            {
                // Check if models are already loaded to avoid redundant work
                if (netPageViewModel.AvailableModels?.Count > 0)
                {
                    Debug.WriteLine($"HomePage: NetPage already initialized with {netPageViewModel.AvailableModels.Count} models");

                    Dispatcher.Dispatch(() =>
                    {
                        ActiveModelsCount = netPageViewModel.ActiveModels?.Count ?? 0;
                        AIStatusDetail = $"AI models ready ({netPageViewModel.AvailableModels.Count} available)";
                        OnPropertyChanged(nameof(ActiveModelsCount));
                    });
                    return;
                }

                // Update status to show we're loading
                Dispatcher.Dispatch(() =>
                {
                    AIStatusDetail = "Loading neural network models...";
                });

                // Load the NetPage data
                await netPageViewModel.LoadDataAsync();

                // Update UI on main thread
                Dispatcher.Dispatch(() =>
                {
                    // Update the active models count from the actual loaded models
                    ActiveModelsCount = netPageViewModel.ActiveModels?.Count ?? 0;

                    Debug.WriteLine($"HomePage: NetPage preload completed. Loaded {netPageViewModel.AvailableModels?.Count ?? 0} models, {ActiveModelsCount} active.");

                    // Update status to show completion
                    AIStatusDetail = netPageViewModel.AvailableModels?.Count > 0
                        ? $"AI models ready ({netPageViewModel.AvailableModels.Count} available)"
                        : "AI models initialized";

                    // Update properties that depend on the loaded models
                    OnPropertyChanged(nameof(ActiveModelsCount));
                });
            }
            else
            {
                Debug.WriteLine("HomePage: NetPageViewModel not found in app instance");
                Dispatcher.Dispatch(() =>
                {
                    AIStatusDetail = "AI initialization failed - NetPage not available";
                });
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"HomePage: Error preloading NetPage: {ex.Message}");
            Dispatcher.Dispatch(() =>
            {
                AIStatusDetail = "AI initialization completed with warnings";
            });
        }
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

            // Set up context menu handlers
            trayService.StartListenHandler = () =>
            {
                Debug.WriteLine("Simply start requested from tray menu");
                try
                {
                    // Use the unified IsAIEnabled property instead of directly setting NetPageViewModel
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        IsAIEnabled = true;
                        Debug.WriteLine("Simply assistant started from tray menu");
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error starting Simply assistant from tray: {ex.Message}");
                }
            };

            trayService.StopListenHandler = () =>
            {
                Debug.WriteLine("Simply stop requested from tray menu");
                try
                {
                    // Use the unified IsAIEnabled property instead of directly setting NetPageViewModel
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        IsAIEnabled = false;
                        Debug.WriteLine("Simply assistant stopped from tray menu");
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error stopping Simply assistant from tray: {ex.Message}");
                }
            };

            trayService.ShowSettingsHandler = () =>
            {
                Debug.WriteLine("Settings requested from tray menu");
                try
                {
                    // Bring window to front and navigate to settings page
                    WindowExtensions.BringToFront();

                    // Navigate to settings page
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        try
                        {
                            Debug.WriteLine("Navigating to settings page from tray menu");
                            await Shell.Current.GoToAsync("///settings");
                            Debug.WriteLine("Successfully navigated to settings page");
                        }
                        catch (Exception navEx)
                        {
                            Debug.WriteLine($"Error navigating to settings page: {navEx.Message}");
                        }
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error showing settings from tray: {ex.Message}");
                }
            };

            trayService.QuitApplicationHandler = () =>
            {
                Debug.WriteLine("Quit requested from tray menu");
                try
                {
                    Application.Current?.Quit();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error quitting application from tray: {ex.Message}");
                }
            };

            trayService.IsListeningCallback = () =>
            {
                try
                {
                    // Use the unified IsAIEnabled property instead of checking NetPageViewModel directly
                    return IsAIEnabled;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error checking intelligence status for tray: {ex.Message}");
                    return false;
                }
            };

            Debug.WriteLine("Tray context menu handlers configured");
        }
    }

    // Methods for AI state management and persistence
    private async Task SaveAIEnabledStateAsync(bool isEnabled)
    {
        try
        {
            await SecureStorage.SetAsync("IsAIEnabled", isEnabled.ToString());
            Debug.WriteLine($"AI enabled state saved: {isEnabled}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error saving AI enabled state: {ex.Message}");
        }
    }

    private async Task<bool> LoadAIEnabledStateAsync()
    {
        try
        {
            var savedState = await SecureStorage.GetAsync("IsAIEnabled");
            if (savedState != null && bool.TryParse(savedState, out bool isEnabled))
            {
                Debug.WriteLine($"AI enabled state loaded: {isEnabled}");
                return isEnabled;
            }
            else
            {
                Debug.WriteLine("No saved AI enabled state found, defaulting to false");
                return false; // Default to false as requested
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading AI enabled state: {ex.Message}");
            return false; // Default to false on error
        }
    }

    private void SynchronizeIntelligenceState(bool isEnabled)
    {
        try
        {
            var app = Application.Current as App;
            var netPageViewModel = app?.NetPageViewModel;
            if (netPageViewModel != null)
            {
                // Synchronize NetPageViewModel's IsIntelligenceActive with IsAIEnabled
                netPageViewModel.IsIntelligenceActive = isEnabled;
                Debug.WriteLine($"Synchronized NetPageViewModel.IsIntelligenceActive to: {isEnabled}");
            }
            else
            {
                Debug.WriteLine("NetPageViewModel not available for state synchronization");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error synchronizing intelligence state: {ex.Message}");
        }
    }

    private void SetupNetPageViewModelSync()
    {
        try
        {
            var app = Application.Current as App;
            var netPageViewModel = app?.NetPageViewModel;
            if (netPageViewModel != null)
            {
                // Subscribe to NetPageViewModel property changes to sync back to IsAIEnabled
                netPageViewModel.PropertyChanged += (sender, e) =>
                {
                    if (e.PropertyName == nameof(netPageViewModel.IsIntelligenceActive))
                    {
                        // Sync back to IsAIEnabled if NetPageViewModel changes from other sources
                        if (netPageViewModel.IsIntelligenceActive != _isAIEnabled)
                        {
                            MainThread.BeginInvokeOnMainThread(() =>
                            {
                                _isAIEnabled = netPageViewModel.IsIntelligenceActive;
                                OnPropertyChanged(nameof(IsAIEnabled));

                                // Update UI status
                                ActiveAIStatus = _isAIEnabled ? "AI Assistant Active" : "AI Assistant Inactive";
                                OnPropertyChanged(nameof(ActiveAIStatus));

                                // Save the new state
                                Task.Run(async () => await SaveAIEnabledStateAsync(_isAIEnabled));

                                Debug.WriteLine($"Reverse-synchronized IsAIEnabled to: {_isAIEnabled}");
                            });
                        }
                    }
                };
                Debug.WriteLine("NetPageViewModel synchronization established");
            }
            else
            {
                Debug.WriteLine("NetPageViewModel not available for reverse synchronization setup");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error setting up NetPageViewModel sync: {ex.Message}");
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
