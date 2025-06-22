using System.Diagnostics;
using System.Windows.Input;
using CSimple.Pages;
using System;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Xaml;
using CSimple.Converters;
using CSimple.Views;
using Microsoft.Maui.Controls.PlatformConfiguration;
using CSimple.Services;
using CSimple.Services.AppModeService;
using Microsoft.Maui.Platform;
// Add this using statement
using CSimple.ViewModels;
using System.IO; // Add this using statement

[assembly: XamlCompilation(XamlCompilationOptions.Compile)]

namespace CSimple;

public partial class App : Application
{
    // Define user preferences
    public enum NavMode
    {
        Tabs,
        Flyout
    }

    private NavMode _navigationMode;
    public NavMode NavigationMode
    {
        get => _navigationMode;
        set
        {
            _navigationMode = value;
            UpdateNavigationMode();
        }
    }

    public ICommand ToggleFlyoutCommand { get; }    // Add this property
    public NetPageViewModel NetPageViewModel { get; private set; }

    // Inject services via constructor - Change PythonDependencyManager to PythonBootstrapper
    public App(FileService fileService, HuggingFaceService huggingFaceService, PythonBootstrapper pythonBootstrapper, AppModeService appModeService, PythonEnvironmentService pythonEnvironmentService, ModelCommunicationService modelCommunicationService, ModelExecutionService modelExecutionService, ModelImportExportService modelImportExportService, ITrayService trayService)
    {
        try
        {
            // Create converters before initialization
            var boolToColorConverter = new BoolToColorConverter();
            var intToColorConverter = new IntToColorConverter();
            var intToBoolConverter = new IntToBoolConverter();
            var inverseBoolConverter = new InverseBoolConverter();

            Debug.WriteLine("App constructor: Converters created");

            InitializeComponent(); Debug.WriteLine("App constructor: InitializeComponent completed");

            // Instantiate NetPageViewModel using injected services - Pass pythonBootstrapper
            NetPageViewModel = new NetPageViewModel(fileService, huggingFaceService, pythonBootstrapper, appModeService, pythonEnvironmentService, modelCommunicationService, modelExecutionService, modelImportExportService, trayService);
            Debug.WriteLine("App constructor: NetPageViewModel instantiated");

            // Extract bundled scripts to app data directory
            Task.Run(ExtractBundledScriptsAsync);

            // Register converters directly after initialization to ensure they're available
            try
            {
                Resources["BoolToColorConverter"] = boolToColorConverter;
                Resources["IntToColorConverter"] = intToColorConverter;
                Resources["IntToBoolConverter"] = intToBoolConverter;
                Resources["InverseBoolConverter"] = inverseBoolConverter;

                Debug.WriteLine("App constructor: Converters registered successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error registering converters: {ex.Message}");
            }

            // Register routes
            Routing.RegisterRoute(nameof(SettingsPage), typeof(SettingsPage));
            Routing.RegisterRoute(nameof(LoginPage), typeof(LoginPage));
            Routing.RegisterRoute(nameof(HomePage), typeof(HomePage));
            Routing.RegisterRoute(nameof(NetPage), typeof(NetPage));

            //App.Current.UserAppTheme = AppTheme.Dark;

            if (DeviceInfo.Idiom == DeviceIdiom.Phone)
                Shell.Current.CurrentItem = PhoneTabs;

        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Critical error in App constructor: {ex.Message}");
        }

        ToggleFlyoutCommand = new Command(() =>
        {
            Shell.Current.FlyoutIsPresented = !Shell.Current.FlyoutIsPresented;
        });

        // Initialize navigation mode
        _navigationMode = NavMode.Flyout;

#if MACCATALYST
        // Use flyout navigation on macOS
        _navigationMode = NavMode.Flyout;
#endif

        UpdateNavigationMode();

        // Register services
        DependencyService.Register<DataService>();
    }

    private async Task ExtractBundledScriptsAsync()
    {
        try
        {
            string scriptsSourceFolder = "Scripts";
            string appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CSimple"
            );
            string scriptsPath = Path.Combine(appDataPath, "scripts");

            // Create scripts directory if it doesn't exist
            Directory.CreateDirectory(scriptsPath);

            // Check if we have embedded scripts and copy them
            string embeddedScriptsPath = Path.Combine(AppContext.BaseDirectory, scriptsSourceFolder);
            if (Directory.Exists(embeddedScriptsPath))
            {
                foreach (var file in Directory.GetFiles(embeddedScriptsPath, "*.py"))
                {
                    string destPath = Path.Combine(scriptsPath, Path.GetFileName(file));
                    File.Copy(file, destPath, overwrite: true);
                    Debug.WriteLine($"Copied script: {Path.GetFileName(file)} to {destPath}");
                }
            }
            else
            {
                Debug.WriteLine($"No embedded scripts found at {embeddedScriptsPath}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error extracting bundled scripts: {ex.Message}");
        }
    }

    private void UpdateNavigationMode()
    {
        if (AppShell != null && PhoneTabs != null)
        {
            switch (_navigationMode)
            {
                case NavMode.Tabs:
                    Debug.WriteLine("Setting navigation mode to Tabs");
                    PhoneTabs.IsVisible = true;
                    Shell.SetFlyoutBehavior(AppShell, FlyoutBehavior.Disabled);
                    break;
                case NavMode.Flyout:
                default:
                    Debug.WriteLine("Setting navigation mode to Flyout");
                    PhoneTabs.IsVisible = false;
                    Shell.SetFlyoutBehavior(AppShell, FlyoutBehavior.Flyout);
                    break;
            }
        }
    }

    async void TapGestureRecognizer_Tapped(System.Object sender, System.EventArgs e)
    {
        try
        {
            await Shell.Current.GoToAsync($"///settings");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"err: {ex.Message}");
        }
    }
}
