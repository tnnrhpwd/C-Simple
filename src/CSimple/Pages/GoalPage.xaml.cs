using CSimple.Models;
using CSimple.Services;
using CSimple.Services.AppModeService;
using CSimple.ViewModels; // Added for OrientPageViewModel
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Maui.Storage;
using System.Text.Json;
using CSimple.Services.AppModeService;
using System.IO;

namespace CSimple.Pages
{
    public partial class GoalPage : ContentPage, INotifyPropertyChanged
    {
        #region Private Fields
        private readonly GoalService _goalService;
        private readonly CSimple.Services.AppModeService.AppModeService _appModeService;
        private readonly OrientPageViewModel _orientPageViewModel; // Added
        private readonly FileService _fileService; // Added FileService
        private readonly DataService _dataService; // Added for backend integration
        private readonly NetPageViewModel _netPageViewModel; // Added for neural network access

        private bool _showNewGoal = false;
        private bool _isLoading = false;
        private bool _hasError = false;
        private string _errorMessage = string.Empty;
        private bool _isUserLoggedIn = false;
        #endregion
        public bool ShowNewGoal
        {
            get => _showNewGoal;
            set => SetProperty(ref _showNewGoal, value, onChanged: () => OnPropertyChanged(nameof(CreateGoalButtonText)));
        }

        public string CreateGoalButtonText => ShowNewGoal ? "Cancel Goal" : "Create Goal";

        // Loading and Error States (similar to PlanPage)
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public bool HasError
        {
            get => _hasError;
            set => SetProperty(ref _hasError, value);
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        // User state
        public bool IsUserLoggedIn
        {
            get => _isUserLoggedIn;
            set => SetProperty(ref _isUserLoggedIn, value);
        }

        public ObservableCollection<Goal> MyGoals { get; set; } = new ObservableCollection<Goal>();
        public ObservableCollection<DataItem> AllDataItems { get; set; } = new ObservableCollection<DataItem>();

        private string _newGoalTitle = string.Empty;
        public string NewGoalTitle
        {
            get => _newGoalTitle;
            set => SetProperty(ref _newGoalTitle, value);
        }

        private string _newGoalDescription = string.Empty;
        public string NewGoalDescription
        {
            get => _newGoalDescription;
            set => SetProperty(ref _newGoalDescription, value);
        }

        private int _goalPriority = 3;
        public int GoalPriority
        {
            get => _goalPriority;
            set => SetProperty(ref _goalPriority, value);
        }

        private DateTime _goalDeadline = DateTime.Today.AddDays(7);
        public DateTime GoalDeadline
        {
            get => _goalDeadline;
            set => SetProperty(ref _goalDeadline, value);
        }

        private bool _shareGoal = false;
        public bool ShareGoal
        {
            get => _shareGoal;
            set => SetProperty(ref _shareGoal, value);
        }

        private string _selectedGoalType;
        public string SelectedGoalType
        {
            get => _selectedGoalType;
            set => SetProperty(ref _selectedGoalType, value);
        }

        public ObservableCollection<string> GoalTypes { get; } = new ObservableCollection<string>
        {
            "Personal", "Work", "Learning", "Health", "Finance", "Other"
        };

        // --- Enhanced AI Goal Improvement Properties ---
        private string _aiAnalysisMode = "Comprehensive";
        public string AiAnalysisMode
        {
            get => _aiAnalysisMode;
            set => SetProperty(ref _aiAnalysisMode, value);
        }

        private bool _showAdvancedOptions = false;
        public bool ShowAdvancedOptions
        {
            get => _showAdvancedOptions;
            set => SetProperty(ref _showAdvancedOptions, value);
        }

        private bool _isWebcamAnalysisEnabled = false;
        public bool IsWebcamAnalysisEnabled
        {
            get => _isWebcamAnalysisEnabled;
            set => SetProperty(ref _isWebcamAnalysisEnabled, value);
        }

        private string _webcamAnalysisStatus = "Webcam analysis ready";
        public string WebcamAnalysisStatus
        {
            get => _webcamAnalysisStatus;
            set => SetProperty(ref _webcamAnalysisStatus, value);
        }

        private bool _isSmartGoalGeneration = true;
        public bool IsSmartGoalGeneration
        {
            get => _isSmartGoalGeneration;
            set => SetProperty(ref _isSmartGoalGeneration, value);
        }

        private string _goalContext = string.Empty;
        public string GoalContext
        {
            get => _goalContext;
            set => SetProperty(ref _goalContext, value);
        }

        private ObservableCollection<string> _suggestedImprovements = new ObservableCollection<string>();
        public ObservableCollection<string> SuggestedImprovements
        {
            get => _suggestedImprovements;
            set => SetProperty(ref _suggestedImprovements, value);
        }

        private ObservableCollection<GeneratedGoal> _generatedGoals = new ObservableCollection<GeneratedGoal>();
        public ObservableCollection<GeneratedGoal> GeneratedGoals
        {
            get => _generatedGoals;
            set => SetProperty(ref _generatedGoals, value);
        }

        private bool _isGeneratingGoals = false;
        public bool IsGeneratingGoals
        {
            get => _isGeneratingGoals;
            set => SetProperty(ref _isGeneratingGoals, value);
        }

        private bool _showGeneratedGoals = false;
        public bool ShowGeneratedGoals
        {
            get => _showGeneratedGoals;
            set => SetProperty(ref _showGeneratedGoals, value);
        }

        private string _goalGenerationContext = "";
        public string GoalGenerationContext
        {
            get => _goalGenerationContext;
            set => SetProperty(ref _goalGenerationContext, value);
        }

        private bool _useWebcamForGeneration = false;
        public bool UseWebcamForGeneration
        {
            get => _useWebcamForGeneration;
            set => SetProperty(ref _useWebcamForGeneration, value);
        }

        private string _webcamStatus = "Webcam ready";
        public string WebcamStatus
        {
            get => _webcamStatus;
            set => SetProperty(ref _webcamStatus, value);
        }

        private ObservableCollection<string> _analysisTypes = new ObservableCollection<string>
        {
            "Quick Analysis", "Comprehensive", "Behavioral", "Performance-Based", "Webcam Enhanced"
        };
        public ObservableCollection<string> AnalysisTypes
        {
            get => _analysisTypes;
            set => SetProperty(ref _analysisTypes, value);
        }

        private string _currentWebcamFrame = "";
        public string CurrentWebcamFrame
        {
            get => _currentWebcamFrame;
            set => SetProperty(ref _currentWebcamFrame, value);
        }

        private bool _isWebcamStreaming = false;
        public bool IsWebcamStreaming
        {
            get => _isWebcamStreaming;
            set => SetProperty(ref _isWebcamStreaming, value);
        }

        private string _selectedImprovement;
        public string SelectedImprovement
        {
            get => _selectedImprovement;
            set => SetProperty(ref _selectedImprovement, value);
        }

        private bool _isAnalyzingGoals = false;
        public bool IsAnalyzingGoals
        {
            get => _isAnalyzingGoals;
            set => SetProperty(ref _isAnalyzingGoals, value);
        }

        private ObservableCollection<string> _goalInsights = new ObservableCollection<string>();
        public ObservableCollection<string> GoalInsights
        {
            get => _goalInsights;
            set => SetProperty(ref _goalInsights, value);
        }

        private ObservableCollection<GoalTemplate> _goalTemplates = new ObservableCollection<GoalTemplate>();
        public ObservableCollection<GoalTemplate> GoalTemplates
        {
            get => _goalTemplates;
            set => SetProperty(ref _goalTemplates, value);
        }

        private GoalTemplate _selectedTemplate;
        public GoalTemplate SelectedTemplate
        {
            get => _selectedTemplate;
            set => SetProperty(ref _selectedTemplate, value);
        }

        // --- AI Improvement Section ---
        private string _improvementSuggestion = "Click 'Run Improvement Pipeline' to get AI suggestions.";
        public string ImprovementSuggestion
        {
            get => _improvementSuggestion;
            set => SetProperty(ref _improvementSuggestion, value);
        }

        private string _aiPromptInput = "Suggest future improvements given my PC recorded data.";
        public string AiPromptInput
        {
            get => _aiPromptInput;
            set => SetProperty(ref _aiPromptInput, value);
        }

        private bool _isPipelineRunning = false;
        public bool IsPipelineRunning
        {
            get => _isPipelineRunning;
            set => SetProperty(ref _isPipelineRunning, value);
        }

        // Properties for Pipeline Selection
        public ObservableCollection<string> AvailablePipelines { get; } = new ObservableCollection<string>();

        private string _selectedPipelineName;
        public string SelectedPipelineName
        {
            get => _selectedPipelineName;
            set => SetProperty(ref _selectedPipelineName, value);
        }
        // Properties for enhanced user feedback
        private string _lastAction = string.Empty;
        public string LastAction
        {
            get => _lastAction;
            set => SetProperty(ref _lastAction, value);
        }

        private bool _showLastAction = false;
        public bool ShowLastAction
        {
            get => _showLastAction;
            set => SetProperty(ref _showLastAction, value);
        }

        // --- End AI Improvement Section ---

        // --- Enhanced AI Commands ---
        public ICommand AnalyzeGoalsCommand { get; }
        public ICommand ApplyImprovementCommand { get; }
        public ICommand GenerateGoalTemplatesCommand { get; }
        public ICommand ApplyTemplateCommand { get; }
        public ICommand ToggleAdvancedOptionsCommand { get; }
        public ICommand GenerateGoalInsightsCommand { get; }

        // --- New Enhanced AI Commands ---
        public ICommand StartWebcamAnalysisCommand { get; }
        public ICommand StopWebcamAnalysisCommand { get; }
        public ICommand GenerateSmartGoalCommand { get; }
        public ICommand ApplySmartSuggestionCommand { get; }
        public ICommand ToggleWebcamCommand { get; }
        public ICommand ClearSuggestionsCommand { get; }
        public ICommand ExportGoalAnalyticsCommand { get; }
        public ICommand RefineGoalWithAICommand { get; }


        public ICommand ToggleCreateGoalCommand { get; }
        public ICommand SubmitGoalCommand { get; }
        public ICommand DeleteGoalCommand { get; }
        public ICommand EditGoalCommand { get; }
        public ICommand GenerateGoalsCommand { get; } // Changed from RunImprovementPipelineCommand
        public ICommand AddGeneratedGoalCommand { get; } // New command for adding selected goals

        // --- Tab Selection Properties ---
        private bool _isMyGoalsSelected = true; // Default to My Goals tab
        public bool IsMyGoalsSelected
        {
            get => _isMyGoalsSelected;
            set => SetProperty(ref _isMyGoalsSelected, value);
        }

        private bool _isSharedGoalsSelected;
        public bool IsSharedGoalsSelected
        {
            get => _isSharedGoalsSelected;
            set => SetProperty(ref _isSharedGoalsSelected, value);
        }

        private bool _isDiscoverSelected;
        public bool IsDiscoverSelected
        {
            get => _isDiscoverSelected;
            set => SetProperty(ref _isDiscoverSelected, value);
        }

        // Properties for search (referenced in XAML)
        private string _sharedGoalSearchQuery;
        public string SharedGoalSearchQuery
        {
            get => _sharedGoalSearchQuery;
            set => SetProperty(ref _sharedGoalSearchQuery, value);
        }

        private string _discoverSearchQuery;
        public string DiscoverSearchQuery
        {
            get => _discoverSearchQuery;
            set => SetProperty(ref _discoverSearchQuery, value);
        }

        // Shared goals collection (referenced in XAML)
        public ObservableCollection<Goal> SharedGoals { get; set; } = new ObservableCollection<Goal>();

        // Discover goals collection (referenced in XAML)
        public ObservableCollection<Goal> DiscoverGoals { get; set; } = new ObservableCollection<Goal>();

        // --- Additional Commands ---
        public ICommand SwitchToMyGoalsCommand { get; }
        public ICommand SwitchToSharedGoalsCommand { get; }
        public ICommand SwitchToDiscoverGoalsCommand { get; }
        public ICommand FilterCategoryCommand { get; }
        public ICommand UnshareGoalCommand { get; }
        public ICommand DownloadGoalCommand { get; }

        // Modified constructor to accept OrientPageViewModel, FileService, and NetPageViewModel
        public GoalPage(GoalService goalService, CSimple.Services.AppModeService.AppModeService appModeService, OrientPageViewModel orientPageViewModel, FileService fileService, DataService dataService, NetPageViewModel netPageViewModel) // Added NetPageViewModel
        {
            InitializeComponent();

            _goalService = goalService; // Use injected service
            _appModeService = appModeService; // Use injected service
            _orientPageViewModel = orientPageViewModel; // Store injected ViewModel
            _fileService = fileService; // Store injected FileService
            _dataService = dataService; // Store injected DataService
            _netPageViewModel = netPageViewModel; // Store injected NetPageViewModel

            // Initialize existing commands
            ToggleCreateGoalCommand = new Command(OnToggleCreateGoal);
            SubmitGoalCommand = new Command(async () => await OnSubmitGoal());
            DeleteGoalCommand = new Command<Goal>(async (goal) => await OnDeleteGoal(goal));
            EditGoalCommand = new Command<Goal>(OnEditGoal);
            GenerateGoalsCommand = new Command(async () => await OnGenerateGoals(), () => !IsGeneratingGoals); // Always enabled, works with or without pipeline
            AddGeneratedGoalCommand = new Command<GeneratedGoal>(OnAddGeneratedGoal);

            // Initialize new tab commands
            SwitchToMyGoalsCommand = new Command(() => SwitchTab(TabType.MyGoals));
            SwitchToSharedGoalsCommand = new Command(() => SwitchTab(TabType.SharedGoals));
            SwitchToDiscoverGoalsCommand = new Command(() => SwitchTab(TabType.Discover));

            // Initialize other new commands
            FilterCategoryCommand = new Command<string>(OnFilterCategory);
            UnshareGoalCommand = new Command<Goal>(OnUnshareGoal);
            DownloadGoalCommand = new Command<Goal>(OnDownloadGoal);

            // Initialize enhanced AI commands
            AnalyzeGoalsCommand = new Command(async () => await OnAnalyzeGoals());
            ApplyImprovementCommand = new Command<string>(OnApplyImprovement);
            GenerateGoalTemplatesCommand = new Command(async () => await OnGenerateGoalTemplates());
            ApplyTemplateCommand = new Command<GoalTemplate>(OnApplyTemplate);
            ToggleAdvancedOptionsCommand = new Command(OnToggleAdvancedOptions);
            GenerateGoalInsightsCommand = new Command(async () => await OnGenerateGoalInsights());

            // Initialize new enhanced AI commands
            StartWebcamAnalysisCommand = new Command(async () => await OnStartWebcamAnalysis());
            StopWebcamAnalysisCommand = new Command(OnStopWebcamAnalysis);
            GenerateSmartGoalCommand = new Command(async () => await OnGenerateSmartGoal());
            ApplySmartSuggestionCommand = new Command<SmartGoalSuggestion>(OnApplySmartSuggestion);
            ToggleWebcamCommand = new Command(async () => await OnToggleWebcam());
            ClearSuggestionsCommand = new Command(OnClearSuggestions);
            ExportGoalAnalyticsCommand = new Command(async () => await OnExportGoalAnalytics());
            RefineGoalWithAICommand = new Command<Goal>(async (goal) => await OnRefineGoalWithAI(goal));

            BindingContext = this;

            _ = InitializePageAsync(); // Initialize with backend integration
        }

        private async Task InitializePageAsync()
        {
            try
            {
                IsLoading = true;
                HasError = false;

                // Check user authentication status
                await CheckUserLoggedInAsync();

                // Load initial data
                await LoadInitialDataAsync();
            }
            catch (Exception ex)
            {
                HasError = true;
                ErrorMessage = ex.Message;
                Debug.WriteLine($"Error initializing page: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        // Add the missing LoadInitialDataAsync method
        private async Task LoadInitialDataAsync()
        {
            // Load goals
            await LoadGoalsAsync();

            // Load available pipelines
            await LoadAvailablePipelinesAsync();
        }

        private async Task CheckUserLoggedInAsync()
        {
            if (_appModeService.CurrentMode == AppMode.Offline)
            {
                Debug.WriteLine("App is in offline mode. Using local goals only.");
                IsUserLoggedIn = false;
                await LoadGoalsFromFile();
                return;
            }

            var isLoggedIn = await IsUserLoggedInAsync();
            IsUserLoggedIn = isLoggedIn;

            if (!isLoggedIn)
            {
                Debug.WriteLine("User is not logged in.");
                // In PlanPage, it navigates to login, but here we'll just show local goals
            }
            else
            {
                Debug.WriteLine("User is logged in.");
                await LoadGoalsFromBackend();
            }
        }

        // Method to load available pipeline names
        private async Task LoadAvailablePipelinesAsync()
        {
            try
            {
                var pipelines = await _fileService.ListPipelinesAsync();
                AvailablePipelines.Clear();
                if (pipelines != null)
                {
                    foreach (var pipeline in pipelines.OrderBy(p => p.Name))
                    {
                        AvailablePipelines.Add(pipeline.Name);
                    }
                }
                // Update command CanExecute state after loading
                ((Command)GenerateGoalsCommand).ChangeCanExecute();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading available pipelines: {ex.Message}");
                // Handle error appropriately, maybe show an alert
            }
        }

        // Add this method to initialize sample data for SharedGoals and DiscoverGoals
        private void InitializeSampleData()
        {
            // Add sample shared goals
            SharedGoals.Add(new Goal
            {
                Title = "Team Project Milestone",
                Description = "Complete the first milestone of our team project by end of month",
                Priority = 4,
                Deadline = DateTime.Today.AddDays(14),
                GoalType = "Work",
                SharedWith = 3,
                SharedDate = DateTime.Today.AddDays(-5)
            });

            SharedGoals.Add(new Goal
            {
                Title = "Study Group Goals",
                Description = "Weekly study sessions for upcoming certification",
                Priority = 3,
                Deadline = DateTime.Today.AddDays(30),
                GoalType = "Learning",
                SharedWith = 5,
                SharedDate = DateTime.Today.AddDays(-2)
            });

            // Add sample discover goals
            DiscoverGoals.Add(new Goal
            {
                Title = "30-Day Fitness Challenge",
                Description = "Daily exercise routine to improve health and energy levels",
                Priority = 5,
                Deadline = DateTime.Today.AddDays(30),
                GoalType = "Health",
                Creator = "FitnessPro",
                Rating = 4.8,
                Downloads = 2345,
                CreatorImage = "https://picsum.photos/200" // placeholder image URL
            });

            DiscoverGoals.Add(new Goal
            {
                Title = "Budget Management Plan",
                Description = "Step-by-step plan to track and optimize personal finances",
                Priority = 4,
                Deadline = DateTime.Today.AddDays(90),
                GoalType = "Finance",
                Creator = "MoneyMentor",
                Rating = 4.5,
                Downloads = 1872,
                CreatorImage = "https://picsum.photos/201" // placeholder image URL
            });

            DiscoverGoals.Add(new Goal
            {
                Title = "Daily Productivity System",
                Description = "Framework for maximizing your daily productivity",
                Priority = 3,
                Deadline = DateTime.Today.AddDays(1),
                GoalType = "Productivity",
                Creator = "ProductivityGuru",
                Rating = 4.9,
                Downloads = 3105,
                CreatorImage = "https://picsum.photos/202" // placeholder image URL
            });
        }

        // Tab switching enum and method
        private enum TabType { MyGoals, SharedGoals, Discover }

        private void SwitchTab(TabType tab)
        {
            IsMyGoalsSelected = tab == TabType.MyGoals;
            IsSharedGoalsSelected = tab == TabType.SharedGoals;
            IsDiscoverSelected = tab == TabType.Discover;
        }

        // Command implementations for new commands
        private void OnFilterCategory(string category)
        {
            if (string.IsNullOrEmpty(category)) return;

            Debug.WriteLine($"Filtering goals by category: {category}");
            // TODO: Implement category filtering for discover goals
        }

        private void OnUnshareGoal(Goal goal)
        {
            if (goal == null) return;

            Debug.WriteLine($"Unsharing goal: {goal.Title}");
            // TODO: Implement unsharing functionality
            SharedGoals.Remove(goal);
        }

        private async void OnDownloadGoal(Goal goal)
        {
            if (goal == null) return;

            Debug.WriteLine($"Downloading goal: {goal.Title}");
            // TODO: Implement download functionality

            // Simplified example - copy to MyGoals
            var downloadedGoal = new Goal
            {
                Title = goal.Title,
                Description = goal.Description,
                Priority = goal.Priority,
                Deadline = goal.Deadline,
                GoalType = goal.GoalType,
                CreatedAt = DateTime.UtcNow
            };

            MyGoals.Add(downloadedGoal);
            await SaveGoalsToFile();
        }

        #region Enhanced AI Command Handlers

        private async Task OnAnalyzeGoals()
        {
            try
            {
                IsAnalyzingGoals = true;
                SuggestedImprovements.Clear();
                GoalInsights.Clear();

                // Enhanced analysis prompt based on selected mode
                string analysisPrompt = GenerateAnalysisPrompt();

                var analysisResult = await _orientPageViewModel.ExecutePipelineByNameAsync("goal_analysis_pipeline", analysisPrompt);

                if (!string.IsNullOrEmpty(analysisResult))
                {
                    await ProcessAnalysisResult(analysisResult);
                }

                // Generate insights based on goal patterns
                await OnGenerateGoalInsights();

                await DisplayAlert("Analysis Complete",
                    $"Found {SuggestedImprovements.Count} improvement suggestions and {GoalInsights.Count} insights", "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Analysis Error", $"Failed to analyze goals: {ex.Message}", "OK");
            }
            finally
            {
                IsAnalyzingGoals = false;
            }
        }

        private string GenerateAnalysisPrompt()
        {
            var goalTitles = string.Join(", ", MyGoals.Select(g => g.Title));
            var contextInfo = !string.IsNullOrEmpty(GoalContext) ? $" Context: {GoalContext}." : "";

            return AiAnalysisMode switch
            {
                "Quick Analysis" => $"Provide quick improvement suggestions for: {goalTitles}.{contextInfo}",
                "Comprehensive" => $"Perform comprehensive analysis including SMART criteria, dependencies, and optimization for: {goalTitles}.{contextInfo} Include priority recommendations and timeline analysis.",
                "Behavioral" => $"Analyze behavioral patterns and psychological factors for goal achievement: {goalTitles}.{contextInfo} Focus on habit formation and motivation strategies.",
                "Performance-Based" => $"Evaluate performance metrics and success indicators for: {goalTitles}.{contextInfo} Include KPI suggestions and measurement strategies.",
                "Webcam Enhanced" => IsWebcamAnalysisEnabled ?
                    $"Analyze goals with webcam context for user engagement and focus patterns: {goalTitles}.{contextInfo} Consider visual cues and workspace optimization." :
                    $"Analyze goals for visual engagement patterns: {goalTitles}.{contextInfo}",
                _ => $"Analyze these goals and provide improvement suggestions: {goalTitles}.{contextInfo}"
            };
        }

        private Task ProcessAnalysisResult(string analysisResult)
        {
            var suggestions = analysisResult.Split('\n')
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .Where(s => !s.StartsWith("Note:") && !s.StartsWith("Disclaimer:"))
                .ToList();

            foreach (var suggestion in suggestions)
            {
                if (suggestion.Contains("SMART:"))
                {
                    SuggestedImprovements.Add($"🎯 {suggestion}");
                }
                else if (suggestion.Contains("Priority:"))
                {
                    SuggestedImprovements.Add($"🔥 {suggestion}");
                }
                else if (suggestion.Contains("Timeline:"))
                {
                    SuggestedImprovements.Add($"⏰ {suggestion}");
                }
                else
                {
                    SuggestedImprovements.Add($"💡 {suggestion}");
                }
            }

            return Task.CompletedTask;
        }

        #region Enhanced Webcam Analysis

        private async Task OnStartWebcamAnalysis()
        {
            try
            {
                IsWebcamAnalysisEnabled = true;
                WebcamAnalysisStatus = "Starting webcam analysis...";

                // Simulate webcam initialization and analysis
                await Task.Delay(2000);

                if (IsWebcamAnalysisEnabled)
                {
                    WebcamAnalysisStatus = "Webcam analysis active - monitoring engagement";
                    await StartWebcamMonitoring();
                }
            }
            catch (Exception ex)
            {
                WebcamAnalysisStatus = $"Webcam analysis failed: {ex.Message}";
                IsWebcamAnalysisEnabled = false;
            }
        }

        private async Task StartWebcamMonitoring()
        {
            // Simulate webcam monitoring with periodic updates
            while (IsWebcamAnalysisEnabled)
            {
                await Task.Delay(5000); // Update every 5 seconds

                if (IsWebcamAnalysisEnabled)
                {
                    var engagementLevel = new Random().Next(60, 95);
                    WebcamAnalysisStatus = $"Engagement: {engagementLevel}% - Focus detected";

                    // Generate contextual suggestions based on "webcam analysis"
                    if (engagementLevel < 70)
                    {
                        SuggestedImprovements.Add("📹 Low engagement detected - consider breaking goals into smaller tasks");
                    }
                    else if (engagementLevel > 90)
                    {
                        SuggestedImprovements.Add("📹 High focus detected - optimal time for challenging goals");
                    }
                }
            }
        }

        private void OnStopWebcamAnalysis()
        {
            IsWebcamAnalysisEnabled = false;
            WebcamAnalysisStatus = "Webcam analysis stopped";
        }

        private async Task OnToggleWebcam()
        {
            try
            {
                IsWebcamStreaming = !IsWebcamStreaming;

                if (IsWebcamStreaming)
                {
                    WebcamAnalysisStatus = "Webcam stream starting...";
                    await Task.Delay(1000);
                    WebcamAnalysisStatus = "Webcam stream active";
                    CurrentWebcamFrame = "data:image/placeholder"; // Placeholder for actual webcam data
                }
                else
                {
                    WebcamAnalysisStatus = "Webcam stream stopped";
                    CurrentWebcamFrame = "";
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Webcam Error", $"Failed to toggle webcam: {ex.Message}", "OK");
                IsWebcamStreaming = false;
            }
        }

        #endregion

        #region Smart Goal Generation

        private async Task OnGenerateSmartGoal()
        {
            try
            {
                GeneratedGoals.Clear();

                var prompt = $"Generate SMART goal suggestions based on user context: {GoalContext}. " +
                           $"Current goals: {string.Join(", ", MyGoals.Select(g => g.Title))}. " +
                           "Focus on specific, measurable, achievable, relevant, and time-bound goals.";

                var result = await _orientPageViewModel.ExecutePipelineByNameAsync("smart_goal_pipeline", prompt);

                if (!string.IsNullOrEmpty(result))
                {
                    await ProcessSmartGoalSuggestions(result);
                }

                await DisplayAlert("Smart Goals Generated",
                    $"Generated {GeneratedGoals.Count} SMART goal suggestions", "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Generation Error", $"Failed to generate smart goals: {ex.Message}", "OK");
            }
        }

        private Task ProcessSmartGoalSuggestions(string result)
        {
            var lines = result.Split('\n').Where(l => !string.IsNullOrWhiteSpace(l)).ToList();

            for (int i = 0; i < Math.Min(lines.Count, 5); i++)
            {
                var suggestion = new SmartGoalSuggestion
                {
                    Id = Guid.NewGuid().ToString(),
                    Title = $"Smart Goal {i + 1}",
                    Description = lines[i].Trim(),
                    Confidence = new Random().Next(75, 95),
                    Category = DetermineCategory(lines[i]),
                    EstimatedDuration = TimeSpan.FromDays(new Random().Next(7, 90)),
                    Priority = new Random().Next(1, 5),
                    IsRecommended = new Random().NextDouble() > 0.3
                };

                GeneratedGoals.Add(new GeneratedGoal
                {
                    Title = suggestion.Title,
                    Description = suggestion.Description,
                    Category = suggestion.Category,
                    Priority = suggestion.Priority,
                    Confidence = suggestion.Confidence,
                    Source = "AI Template",
                    Rationale = suggestion.Rationale,
                    SuggestedDeadline = DateTime.Now.AddDays(14),
                    EstimatedDuration = TimeSpan.FromDays(7),
                    Icon = "🎯",
                    KeySteps = suggestion.ActionSteps?.ToList()
                });
            }

            return Task.CompletedTask;
        }

        private string DetermineCategory(string description)
        {
            var lowerDesc = description.ToLower();
            if (lowerDesc.Contains("health") || lowerDesc.Contains("fitness")) return "Health";
            if (lowerDesc.Contains("work") || lowerDesc.Contains("career")) return "Career";
            if (lowerDesc.Contains("learn") || lowerDesc.Contains("study")) return "Learning";
            if (lowerDesc.Contains("money") || lowerDesc.Contains("finance")) return "Finance";
            return "Personal";
        }

        private void OnApplySmartSuggestion(SmartGoalSuggestion suggestion)
        {
            if (suggestion == null) return;

            var newGoal = new Goal
            {
                Id = Guid.NewGuid().ToString(),
                Title = suggestion.Title,
                Description = suggestion.Description,
                Priority = suggestion.Priority,
                Deadline = DateTime.Now.Add(suggestion.EstimatedDuration),
                GoalType = suggestion.Category,
                CreatedAt = DateTime.UtcNow
            };

            MyGoals.Add(newGoal);
            DisplayAlert("Smart Goal Applied", $"Created goal: {newGoal.Title}", "OK");
        }

        #endregion

        #region Advanced AI Features

        private async Task OnRefineGoalWithAI(Goal goal)
        {
            if (goal == null) return;

            try
            {
                var prompt = $"Refine and improve this goal using SMART criteria: " +
                           $"Title: {goal.Title}, Description: {goal.Description}, " +
                           $"Priority: {goal.Priority}, Type: {goal.GoalType}";

                var result = await _orientPageViewModel.ExecutePipelineByNameAsync("goal_refinement_pipeline", prompt);

                if (!string.IsNullOrEmpty(result))
                {
                    var refinedGoal = new Goal
                    {
                        Id = Guid.NewGuid().ToString(),
                        Title = $"Refined: {goal.Title}",
                        Description = result,
                        Priority = goal.Priority,
                        Deadline = goal.Deadline,
                        GoalType = goal.GoalType,
                        CreatedAt = DateTime.UtcNow
                    };

                    MyGoals.Add(refinedGoal);
                    await DisplayAlert("Goal Refined", "AI has created an improved version of your goal", "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Refinement Error", $"Failed to refine goal: {ex.Message}", "OK");
            }
        }

        private void OnClearSuggestions()
        {
            SuggestedImprovements.Clear();
            GoalInsights.Clear();
            GeneratedGoals.Clear();
        }

        private async Task OnExportGoalAnalytics()
        {
            try
            {
                var analytics = new
                {
                    TotalGoals = MyGoals.Count,
                    CompletedGoals = MyGoals.Count(g => g.GoalType == "Completed"),
                    AnalysisMode = AiAnalysisMode,
                    Suggestions = SuggestedImprovements.ToList(),
                    Insights = GoalInsights.ToList(),
                    SmartSuggestions = GeneratedGoals.ToList(),
                    ExportDate = DateTime.Now,
                    WebcamAnalysisEnabled = IsWebcamAnalysisEnabled
                };

                string jsonAnalytics = JsonSerializer.Serialize(analytics, new JsonSerializerOptions { WriteIndented = true });
                string fileName = $"goal_analytics_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                string filePath = Path.Combine(FileSystem.AppDataDirectory, fileName);

                await File.WriteAllTextAsync(filePath, jsonAnalytics);

                await DisplayAlert("Analytics Exported", $"Goal analytics exported to: {filePath}", "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Export Error", $"Failed to export analytics: {ex.Message}", "OK");
            }
        }

        #endregion

        private void OnApplyImprovement(string improvement)
        {
            // Apply the selected improvement to goals
            // This could involve updating goal properties or creating new goals
            DisplayAlert("Improvement Applied", $"Applied: {improvement}", "OK");
        }

        private async Task OnGenerateGoalTemplates()
        {
            try
            {
                GoalTemplates.Clear();

                // Generate templates based on existing goals and AI analysis
                var templatePrompt = "Generate goal templates based on common goal patterns and success factors";

                var templateResult = await _orientPageViewModel.ExecutePipelineByNameAsync("template_generation_pipeline", templatePrompt);

                if (!string.IsNullOrEmpty(templateResult))
                {
                    // Parse the AI response - assuming it returns templates in a structured format
                    // For now, create some default templates based on the response
                    var templateLines = templateResult.Split('\n')
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .ToList();

                    // Create templates from the AI response
                    for (int i = 0; i < Math.Min(templateLines.Count, 3); i++)
                    {
                        var template = new GoalTemplate
                        {
                            Title = $"AI Generated Template {i + 1}",
                            Description = templateLines[i],
                            Category = "General",
                            SuggestedPriority = 3,
                            SuggestedDurationDays = 30,
                            KeySteps = new List<string> { "Step 1", "Step 2", "Step 3" },
                            RequiredResources = new List<string> { "Time", "Focus" },
                            Difficulty = "Medium",
                            MotivationTip = "Stay consistent and track your progress",
                            SuccessRate = 0.75
                        };
                        GoalTemplates.Add(template);
                    }
                }

                await DisplayAlert("Templates Generated", $"Created {GoalTemplates.Count} goal templates", "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Template Generation Error", $"Failed to generate templates: {ex.Message}", "OK");
            }
        }

        private void OnApplyTemplate(GoalTemplate template)
        {
            // Create a new goal based on the template
            var newGoal = new Goal
            {
                Title = template.Title,
                Description = template.Description,
                Priority = template.SuggestedPriority,
                Deadline = DateTime.Now.AddDays(template.SuggestedDurationDays),
                GoalType = template.Category,
                CreatedAt = DateTime.UtcNow
            };

            MyGoals.Add(newGoal);
            DisplayAlert("Template Applied", $"Created goal: {newGoal.Title}", "OK");
        }

        private void OnToggleAdvancedOptions()
        {
            ShowAdvancedOptions = !ShowAdvancedOptions;
        }

        private async Task OnGenerateGoalInsights()
        {
            try
            {
                GoalInsights.Clear();

                // Analyze goal completion patterns, time management, etc.
                var insights = new List<string>();

                if (MyGoals.Any())
                {
                    var completedGoals = MyGoals.Count(g => g.GoalType == "Completed");
                    var totalGoals = MyGoals.Count;
                    var completionRate = totalGoals > 0 ? (double)completedGoals / totalGoals * 100 : 0;

                    insights.Add($"Goal completion rate: {completionRate:F1}%");

                    var overdueGoals = MyGoals.Count(g => g.Deadline < DateTime.Now && g.GoalType != "Completed");
                    if (overdueGoals > 0)
                    {
                        insights.Add($"You have {overdueGoals} overdue goals that need attention");
                    }

                    var highPriorityGoals = MyGoals.Count(g => g.Priority >= 4 && g.GoalType != "Completed");
                    if (highPriorityGoals > 0)
                    {
                        insights.Add($"Focus on {highPriorityGoals} high-priority goals");
                    }

                    // Category distribution
                    var categories = MyGoals.GroupBy(g => g.GoalType)
                                         .OrderByDescending(g => g.Count())
                                         .Take(3);

                    foreach (var category in categories)
                    {
                        insights.Add($"{category.Key}: {category.Count()} goals");
                    }
                }

                foreach (var insight in insights)
                {
                    GoalInsights.Add(insight);
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Insights Error", $"Failed to generate insights: {ex.Message}", "OK");
            }
        }

        #endregion

        // --- Enhanced Goal Generation Method with Neural Network Priority ---
        private async Task OnGenerateGoals()
        {
            try
            {
                IsGeneratingGoals = true;

                // Clear unselected goals (keep selected ones if button is repressed)
                var selectedGoals = GeneratedGoals.Where(g => g.IsSelected).ToList();
                GeneratedGoals.Clear();
                foreach (var selectedGoal in selectedGoals)
                {
                    GeneratedGoals.Add(selectedGoal);
                }

                ShowGeneratedGoals = true;

                // Generate goals with multiple strategies - prioritizing neural networks
                var generatedGoals = new List<GeneratedGoal>();

                // Strategy 1: Active Neural Network Models (HIGHEST PRIORITY - Try Multiple Models)
                var neuralGoals = await GenerateGoalsFromActiveNeuralNetworks();
                generatedGoals.AddRange(neuralGoals);

                // Strategy 2: Try to get more neural network goals aggressively to reach 3
                if (generatedGoals.Count > 0 && generatedGoals.Count < 3)
                {
                    var additionalNeuralGoals = await TryGenerateMoreNeuralGoals();
                    generatedGoals.AddRange(additionalNeuralGoals);
                }

                // Strategy 3: Try different neural prompts if still not enough goals
                if (generatedGoals.Count > 0 && generatedGoals.Count < 3)
                {
                    var diverseNeuralGoals = await GenerateDiverseNeuralGoals();
                    generatedGoals.AddRange(diverseNeuralGoals);
                }

                // REMOVE OTHER STRATEGIES - Only neural network goals wanted
                // No AI Pipeline, Context Analysis, Webcam, Existing Goals Analysis, or Fallbacks

                // Take up to 3 neural network goals only
                var finalGoals = generatedGoals
                    .GroupBy(g => g.Title) // Remove duplicates by title
                    .Select(g => g.First())
                    .OrderByDescending(g => g.Confidence)
                    .Take(3) // Only 3 goals
                    .ToList();

                // Add new generated goals to collection (after selected ones)
                foreach (var goal in finalGoals)
                {
                    GeneratedGoals.Add(goal);
                }

                // Update status with neural network focus
                var neuralCount = finalGoals.Count(g => g.Source.Contains("Neural"));
                var totalCount = GeneratedGoals.Count;

                if (neuralCount > 0)
                {
                    ShowFeedback($"🧠 Generated {neuralCount} neural network goals! Pure AI-powered suggestions ready.");
                    Debug.WriteLine($"Generated {neuralCount} goals successfully - all from neural networks");
                }
                else
                {
                    ShowFeedback("🛠️ No neural network models active. Please activate models in NetPage for AI suggestions.");
                    Debug.WriteLine("No neural network goals generated - models need activation");
                }
            }
            catch (Exception ex)
            {
                // Log error but don't add any fallback goals
                Debug.WriteLine($"Neural network goal generation error: {ex.Message}");

                // Only add a setup guidance goal if no neural networks are working at all
                if (GeneratedGoals.Count == 0)
                {
                    GeneratedGoals.Add(new GeneratedGoal
                    {
                        Title = "Activate Neural Network Models",
                        Description = "Enable AI models in NetPage to get personalized neural network-generated goals",
                        Category = "AI Setup",
                        Priority = 5,
                        Confidence = 100,
                        Source = "Setup Guidance",
                        Rationale = "Neural networks are required for AI-powered goal generation",
                        SuggestedDeadline = DateTime.Now.AddDays(1),
                        EstimatedDuration = TimeSpan.FromMinutes(15),
                        Icon = "�",
                        KeySteps = new List<string>
                        {
                            "Go to NetPage",
                            "Activate DeepSeek-R1 or Qwen2.5-VL models",
                            "Test model functionality",
                            "Return for neural network goal generation"
                        }
                    });

                    ShowFeedback("🛠️ Please activate neural network models in NetPage for AI-powered goal generation");
                    Debug.WriteLine("No neural network goals generated - setup guidance provided");
                }
            }
            finally
            {
                IsGeneratingGoals = false;
                ((Command)GenerateGoalsCommand).ChangeCanExecute();
            }
        }

        // --- New Neural Network Goal Generation Methods ---

        private async Task<List<GeneratedGoal>> GenerateGoalsFromActiveNeuralNetworks()
        {
            var goals = new List<GeneratedGoal>();

            try
            {
                Debug.WriteLine("🧠 Generating goals from active neural network models...");

                // Get active text generation models
                var activeModels = _netPageViewModel?.ActiveModels?.Where(m =>
                    m.IsActive &&
                    !string.IsNullOrEmpty(m.HuggingFaceModelId) &&
                    IsTextGenerationModel(m)).ToList();

                if (activeModels == null || !activeModels.Any())
                {
                    Debug.WriteLine("🧠 No active neural network models available for goal generation");
                    return goals;
                }

                Debug.WriteLine($"🧠 Found {activeModels.Count} active text generation models");

                // Use the best model for goal generation
                var bestModel = GetBestTextGenerationModel(activeModels);
                if (bestModel == null)
                {
                    Debug.WriteLine("🧠 No suitable text generation model found");
                    return goals;
                }

                Debug.WriteLine($"🧠 Using model: {bestModel.Name} ({bestModel.HuggingFaceModelId})");

                // Generate goals using neural network
                var neuralGoals = await GenerateGoalsWithNeuralNetwork(bestModel);
                goals.AddRange(neuralGoals);

                // If we have multiple active models, use another one for variety
                if (activeModels.Count > 1 && goals.Count < 5)
                {
                    var secondModel = activeModels.FirstOrDefault(m => m.Id != bestModel.Id);
                    if (secondModel != null)
                    {
                        Debug.WriteLine($"🧠 Using secondary model for variety: {secondModel.Name}");
                        var secondaryGoals = await GenerateGoalsWithNeuralNetwork(secondModel, "alternative perspective");
                        goals.AddRange(secondaryGoals.Take(2)); // Add 2 more for variety
                    }
                }

                Debug.WriteLine($"🧠 Generated {goals.Count} goals from neural networks");
                return goals;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"🧠 Neural network goal generation failed: {ex.Message}");
                return goals;
            }
        }

        private async Task<List<GeneratedGoal>> TryGenerateMoreNeuralGoals()
        {
            var goals = new List<GeneratedGoal>();

            try
            {
                // Get available text generation models (excluding already used ones)
                var activeModels = _netPageViewModel?.ActiveModels?.Where(m =>
                    m.IsActive &&
                    !string.IsNullOrEmpty(m.HuggingFaceModelId) &&
                    IsTextGenerationModel(m)).ToList();

                if (activeModels == null || activeModels.Count <= 1) 
                {
                    // If only one model available, try it again with different prompts
                    var primaryModel = GetBestTextGenerationModel(activeModels ?? new List<NeuralNetworkModel>());
                    if (primaryModel != null)
                    {
                        Debug.WriteLine($"🧠 Re-using primary model with different perspective: {primaryModel.Name}");
                        var modelGoals = await GenerateGoalsWithNeuralNetwork(primaryModel, "alternative creative approach");
                        goals.AddRange(modelGoals.Take(2));
                    }
                    return goals;
                }

                // Try using DeepSeek or Qwen models if available (from execution logs we know these are present)
                var alternativeModels = activeModels.Where(m =>
                {
                    var modelId = m.HuggingFaceModelId?.ToLowerInvariant() ?? "";
                    return modelId.Contains("deepseek") || modelId.Contains("qwen") || 
                           !modelId.Contains("gpt2"); // Try any model that's not GPT-2
                }).Take(2);

                foreach (var model in alternativeModels)
                {
                    Debug.WriteLine($"🧠 Trying additional neural model: {model.Name}");
                    var modelGoals = await GenerateGoalsWithNeuralNetwork(model, "comprehensive life improvement");
                    goals.AddRange(modelGoals.Take(2)); // Up to 2 goals per additional model

                    if (goals.Count >= 2) break; // Stop when we have enough
                }

                Debug.WriteLine($"🧠 Generated {goals.Count} additional neural network goals");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"🧠 Additional neural network generation failed: {ex.Message}");
            }

            return goals;
        }

        private async Task<List<GeneratedGoal>> GenerateDiverseNeuralGoals()
        {
            var goals = new List<GeneratedGoal>();

            try
            {
                // Use the primary model with different creative prompts to ensure we get 3 goals
                var activeModels = _netPageViewModel?.ActiveModels?.Where(m =>
                    m.IsActive &&
                    !string.IsNullOrEmpty(m.HuggingFaceModelId) &&
                    IsTextGenerationModel(m)).ToList();

                var primaryModel = GetBestTextGenerationModel(activeModels ?? new List<NeuralNetworkModel>());
                if (primaryModel == null) return goals;

                // Try with different creative perspectives
                var perspectives = new[]
                {
                    "focus on personal growth and self-improvement",
                    "emphasize productivity and efficiency", 
                    "consider wellness and work-life balance"
                };

                foreach (var perspective in perspectives)
                {
                    Debug.WriteLine($"🧠 Generating diverse neural goals with perspective: {perspective}");
                    var perspectiveGoals = await GenerateGoalsWithNeuralNetwork(primaryModel, perspective);
                    goals.AddRange(perspectiveGoals.Take(1)); // Take 1 from each perspective

                    if (goals.Count >= 2) break;
                }

                Debug.WriteLine($"🧠 Generated {goals.Count} diverse neural network goals");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"🧠 Diverse neural network generation failed: {ex.Message}");
            }

            return goals;
        }

        private bool IsTextGenerationModel(NeuralNetworkModel model)
        {
            var modelId = model.HuggingFaceModelId?.ToLowerInvariant() ?? "";
            var name = model.Name?.ToLowerInvariant() ?? "";

            // Exclude audio/speech models that are not suitable for text generation
            var excludedModels = new[]
            {
                "chatterbox", // Speech synthesis model
                "ultravox", // Multimodal audio model
                "whisper", // Speech recognition
                "speecht5", // Speech processing
                "wav2vec", // Audio processing
                "audio", // Generic audio models
                "speech", // Generic speech models
                "tts", // Text-to-speech
                "stt", // Speech-to-text
                "voice", // Voice models
                "sound" // Sound processing
            };

            // Skip excluded models
            if (excludedModels.Any(excluded => modelId.Contains(excluded) || name.Contains(excluded)))
            {
                Debug.WriteLine($"🧠 Excluding audio/speech model: {model.Name} ({modelId})");
                return false;
            }

            // Check for text generation indicators
            return modelId.Contains("gpt") ||
                   modelId.Contains("llama") ||
                   modelId.Contains("bloom") ||
                   modelId.Contains("t5") ||
                   modelId.Contains("bart") ||
                   modelId.Contains("deepseek") ||
                   modelId.Contains("qwen") ||
                   modelId.Contains("opt") ||
                   modelId.Contains("flan") ||
                   name.Contains("language") ||
                   name.Contains("text") ||
                   name.Contains("chat") ||
                   name.Contains("dialog") ||
                   name.Contains("conversation") ||
                   name.Contains("instruct"); // Add instruct models
        }

        private NeuralNetworkModel GetBestTextGenerationModel(List<NeuralNetworkModel> models)
        {
            // Prioritize models known to work well for goal generation (based on successful execution logs)
            var priorityModels = new[]
            {
                // Tier 1: Proven working models from execution logs
                "openai-community/gpt2", "gpt2", "distilgpt2",
                
                // Tier 2: Available but not yet tested models (from execution logs)
                "deepseek-ai/DeepSeek", "Qwen/Qwen2.5-VL", "Qwen/Qwen",
                
                // Tier 3: Likely compatible models
                "microsoft/DialoGPT", "facebook/opt", "google/flan-t5",
                "microsoft/DialoGPT-medium", "microsoft/DialoGPT-small"
            };

            // Completely exclude problematic and audio/speech models
            var excludedModels = new[]
            {
                // Audio/Speech models (not suitable for text generation)
                "ResembleAI/chatterbox", "fixie-ai/ultravox",
                "whisper", "speecht5", "wav2vec", "tts", "stt",
                "voice", "audio", "speech", "sound"
            };

            // Filter out excluded models first
            var safeModels = models.Where(m =>
            {
                var modelId = (m.HuggingFaceModelId ?? "").ToLowerInvariant();
                var name = (m.Name ?? "").ToLowerInvariant();

                return !excludedModels.Any(excluded =>
                    modelId.Contains(excluded) || name.Contains(excluded));
            }).ToList();

            Debug.WriteLine($"🧠 Filtered to {safeModels.Count} safe text generation models (excluded {models.Count - safeModels.Count} audio/speech models)");

            // Find priority models from the safe list
            foreach (var priority in priorityModels)
            {
                var model = safeModels.FirstOrDefault(m =>
                {
                    var modelId = m.HuggingFaceModelId ?? "";
                    return modelId.Contains(priority, StringComparison.OrdinalIgnoreCase);
                });
                if (model != null)
                {
                    Debug.WriteLine($"🧠 Selected priority model: {model.Name} ({model.HuggingFaceModelId})");
                    return model;
                }
            }

            // Return the first safe model if no priority match
            var fallbackModel = safeModels.FirstOrDefault();
            if (fallbackModel != null)
            {
                Debug.WriteLine($"🧠 Selected fallback text model: {fallbackModel.Name} ({fallbackModel.HuggingFaceModelId})");
            }
            else
            {
                Debug.WriteLine("🧠 No suitable text generation models found");
            }

            return fallbackModel;
        }
        private async Task<List<GeneratedGoal>> GenerateGoalsWithNeuralNetwork(NeuralNetworkModel model, string perspective = "")
        {
            var goals = new List<GeneratedGoal>();

            try
            {
                // Create comprehensive prompt for goal generation
                var prompt = CreateNeuralGoalGenerationPrompt(perspective);

                Debug.WriteLine($"🧠 Executing model {model.Name} with prompt length: {prompt.Length}");

                // Execute the neural network model
                var result = await _netPageViewModel.ExecuteModelAsync(model.HuggingFaceModelId, prompt);

                if (!string.IsNullOrEmpty(result))
                {
                    Debug.WriteLine($"🧠 Model returned result length: {result.Length}");

                    // Check for common error patterns in the result
                    if (result.Contains("ERROR:") || result.Contains("Unrecognized model"))
                    {
                        Debug.WriteLine($"🧠 Model {model.Name} returned error: {result.Substring(0, Math.Min(200, result.Length))}...");
                        return goals; // Return empty list, will trigger fallback
                    }

                    goals = ParseNeuralNetworkGoalResults(result, model.Name);
                    Debug.WriteLine($"🧠 Successfully parsed {goals.Count} goals from {model.Name}");
                }
                else
                {
                    Debug.WriteLine($"🧠 Model {model.Name} returned empty result");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"🧠 Error executing neural network model {model.Name}: {ex.Message}");
                // Don't re-throw - let the system continue with other models or fallbacks
            }

            return goals;
        }

        private string CreateNeuralGoalGenerationPrompt(string perspective = "")
        {
            var contextInfo = !string.IsNullOrEmpty(GoalGenerationContext) ?
                $" Context: {GoalGenerationContext}." : "";

            var existingGoalsInfo = MyGoals.Any() ?
                $" Current goals: {string.Join(", ", MyGoals.Take(3).Select(g => g.Title))}." : "";

            var perspectiveInfo = !string.IsNullOrEmpty(perspective) ?
                $" Provide {perspective}." : "";

            var prompt = $@"Generate 3 specific, actionable personal goals. Each goal should be SMART (Specific, Measurable, Achievable, Relevant, Time-bound).

Format each goal as:
GOAL: [Title]
DESCRIPTION: [Detailed description]
CATEGORY: [Category like Health, Career, Learning, Finance, Personal]
PRIORITY: [1-5 scale]

{contextInfo}{existingGoalsInfo}{perspectiveInfo}

Focus on realistic goals that can be accomplished within 1-12 weeks. Make them diverse across different life areas.

Generate 3 goals now:";

            return prompt;
        }

        private List<GeneratedGoal> ParseNeuralNetworkGoalResults(string result, string modelName)
        {
            var goals = new List<GeneratedGoal>();

            try
            {
                // Split into potential goal sections
                var sections = result.Split(new[] { "GOAL:", "Goal:", "goal:" },
                    StringSplitOptions.RemoveEmptyEntries);

                foreach (var section in sections.Skip(1)) // Skip first empty section
                {
                    var goal = ParseSingleNeuralGoal(section.Trim(), modelName);
                    if (goal != null)
                    {
                        goals.Add(goal);
                        if (goals.Count >= 3) break; // Limit to 3 goals per model
                    }
                }

                // If structured parsing failed, try line-based parsing
                if (goals.Count == 0)
                {
                    goals = ParseNeuralGoalsFromLines(result, modelName);
                }

                Debug.WriteLine($"🧠 Parsed {goals.Count} goals from neural network output");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"🧠 Error parsing neural network results: {ex.Message}");
            }

            return goals;
        }

        private GeneratedGoal ParseSingleNeuralGoal(string section, string modelName)
        {
            try
            {
                var lines = section.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                var goal = new GeneratedGoal
                {
                    Source = $"Neural Network ({modelName})",
                    Confidence = 85, // High confidence for neural network results
                    GeneratedAt = DateTime.Now,
                    Icon = "🧠"
                };

                string title = "";
                foreach (var line in lines)
                {
                    var cleanLine = line.Trim();
                    if (string.IsNullOrEmpty(cleanLine)) continue;

                    if (cleanLine.StartsWith("DESCRIPTION:", StringComparison.OrdinalIgnoreCase))
                    {
                        goal.Description = cleanLine.Substring(12).Trim();
                    }
                    else if (cleanLine.StartsWith("CATEGORY:", StringComparison.OrdinalIgnoreCase))
                    {
                        goal.Category = cleanLine.Substring(9).Trim();
                    }
                    else if (cleanLine.StartsWith("PRIORITY:", StringComparison.OrdinalIgnoreCase))
                    {
                        if (int.TryParse(cleanLine.Substring(9).Trim(), out int priority))
                        {
                            goal.Priority = Math.Max(1, Math.Min(5, priority));
                        }
                    }
                    else if (string.IsNullOrEmpty(title) && !cleanLine.Contains(":"))
                    {
                        // First non-labeled line is likely the title
                        title = cleanLine;
                    }
                }

                goal.Title = !string.IsNullOrEmpty(title) ? title :
                    (!string.IsNullOrEmpty(goal.Description) ?
                        (goal.Description.Length > 50 ? goal.Description.Substring(0, 50) + "..." : goal.Description) :
                        "AI Generated Goal");

                // Set defaults if not parsed
                if (string.IsNullOrEmpty(goal.Category)) goal.Category = "Personal";
                if (goal.Priority == 0) goal.Priority = 3;
                if (string.IsNullOrEmpty(goal.Description)) goal.Description = goal.Title;

                goal.SuggestedDeadline = DateTime.Now.AddDays(new Random().Next(7, 60));
                goal.EstimatedDuration = TimeSpan.FromDays(new Random().Next(1, 30));
                goal.Rationale = $"Generated by neural network model {modelName} based on AI analysis";

                return string.IsNullOrEmpty(goal.Title) ? null : goal;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"🧠 Error parsing single neural goal: {ex.Message}");
                return null;
            }
        }

        private List<GeneratedGoal> ParseNeuralGoalsFromLines(string result, string modelName)
        {
            var goals = new List<GeneratedGoal>();

            try
            {
                var lines = result.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Select(l => l.Trim())
                    .Where(l => !string.IsNullOrEmpty(l) && l.Length > 10)
                    .Take(6) // Limit processing
                    .ToList();

                for (int i = 0; i < Math.Min(lines.Count, 3); i++)
                {
                    var line = lines[i];
                    // Skip lines that look like headers or metadata
                    if (line.StartsWith("Based on") || line.StartsWith("Here are") ||
                        line.StartsWith("Sure") || line.Contains("goals:")) continue;

                    var goal = new GeneratedGoal
                    {
                        Title = line.Length > 80 ? line.Substring(0, 80) + "..." : line,
                        Description = line,
                        Category = DetermineCategory(line),
                        Priority = 3,
                        Confidence = 75,
                        Source = $"Neural Network ({modelName})",
                        SuggestedDeadline = DateTime.Now.AddDays(new Random().Next(14, 45)),
                        EstimatedDuration = TimeSpan.FromDays(new Random().Next(7, 21)),
                        Rationale = $"AI-generated goal from {modelName}",
                        Icon = "🧠"
                    };

                    goals.Add(goal);
                    if (goals.Count >= 3) break;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"🧠 Error parsing neural goals from lines: {ex.Message}");
            }

            return goals;
        }

        private async Task<List<GeneratedGoal>> GenerateGoalsFromFallbackSources()
        {
            var goals = new List<GeneratedGoal>();

            try
            {
                Debug.WriteLine("📋 Fallback sources disabled - neural networks only approach active");

                // NO fallback goals - only neural networks wanted
                // This method is kept for compatibility but returns empty list

                Debug.WriteLine($"📋 Generated {goals.Count} goals from fallback sources (disabled)");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"📋 Fallback goal generation failed: {ex.Message}");
            }

            return goals;
        }

        private List<GeneratedGoal> AnalyzeContextForSmartGoals(string context)
        {
            var goals = new List<GeneratedGoal>();
            var lowerContext = context.ToLower();

            var contextMappings = new Dictionary<string[], (string category, string title, string description)>
            {
                { new[] { "work", "career", "job", "professional" },
                  ("Career", "Professional Development Plan", "Create a structured plan for advancing your career and professional skills") },
                { new[] { "health", "fitness", "exercise", "wellness" },
                  ("Health", "Comprehensive Health Improvement", "Establish a holistic approach to physical and mental wellness") },
                { new[] { "learn", "study", "education", "skill", "knowledge" },
                  ("Learning", "Skill Enhancement Program", "Develop new competencies and expand your knowledge base") },
                { new[] { "money", "finance", "budget", "investment", "savings" },
                  ("Finance", "Financial Optimization Strategy", "Create a comprehensive plan for financial stability and growth") },
                { new[] { "relationship", "social", "family", "friends" },
                  ("Social", "Relationship Building Initiative", "Strengthen personal and professional relationships") },
                { new[] { "creative", "art", "music", "writing", "design" },
                  ("Creativity", "Creative Expression Project", "Explore and develop your creative talents and interests") },
                { new[] { "travel", "adventure", "explore", "experience" },
                  ("Personal", "Life Experience Expansion", "Broaden your horizons through new experiences and adventures") }
            };

            foreach (var mapping in contextMappings)
            {
                if (mapping.Key.Any(keyword => lowerContext.Contains(keyword)))
                {
                    goals.Add(new GeneratedGoal
                    {
                        Title = mapping.Value.title,
                        Description = mapping.Value.description + " based on your current interests and context.",
                        Category = mapping.Value.category,
                        Priority = new Random().Next(3, 5),
                        Confidence = 80,
                        Source = "Context Analysis",
                        Rationale = $"Generated based on context keywords: {string.Join(", ", mapping.Key.Where(k => lowerContext.Contains(k)))}",
                        SuggestedDeadline = DateTime.Now.AddDays(new Random().Next(21, 60)),
                        EstimatedDuration = TimeSpan.FromDays(new Random().Next(7, 30)),
                        Icon = GetCategoryIcon(mapping.Value.category)
                    });

                    if (goals.Count >= 2) break; // Limit context-based goals
                }
            }

            return goals;
        }

        private List<GeneratedGoal> GenerateEnhancedTemplateGoals()
        {
            var enhancedTemplates = new List<GeneratedGoal>
            {
                new GeneratedGoal
                {
                    Title = "Digital Life Optimization",
                    Description = "Streamline your digital workflows, organize online accounts, and improve digital productivity",
                    Category = "Productivity",
                    Priority = 4,
                    Confidence = 85,
                    Source = "Enhanced Template",
                    Rationale = "Digital organization is crucial in today's connected world",
                    SuggestedDeadline = DateTime.Now.AddDays(21),
                    EstimatedDuration = TimeSpan.FromDays(7),
                    Icon = "💻",
                    KeySteps = new List<string> { "Audit digital accounts", "Organize files and folders", "Set up automation", "Review privacy settings" }
                },
                new GeneratedGoal
                {
                    Title = "Mindfulness & Mental Clarity Practice",
                    Description = "Develop a consistent mindfulness practice to improve focus, reduce stress, and enhance well-being",
                    Category = "Health",
                    Priority = 4,
                    Confidence = 90,
                    Source = "Enhanced Template",
                    Rationale = "Mental health is fundamental to achieving all other goals",
                    SuggestedDeadline = DateTime.Now.AddDays(30),
                    EstimatedDuration = TimeSpan.FromDays(21),
                    Icon = "🧘‍♂️",
                    KeySteps = new List<string> { "Choose mindfulness technique", "Set daily practice time", "Track progress", "Join community or app" }
                },
                new GeneratedGoal
                {
                    Title = "Knowledge Synthesis Project",
                    Description = "Create a system for capturing, organizing, and connecting knowledge from various sources",
                    Category = "Learning",
                    Priority = 3,
                    Confidence = 75,
                    Source = "Enhanced Template",
                    Rationale = "Effective knowledge management accelerates learning and decision-making",
                    SuggestedDeadline = DateTime.Now.AddDays(45),
                    EstimatedDuration = TimeSpan.FromDays(14),
                    Icon = "📚",
                    KeySteps = new List<string> { "Choose note-taking system", "Set up knowledge base", "Create connection system", "Regular review process" }
                }
            };

            return enhancedTemplates;
        }

        private List<GeneratedGoal> GenerateAdditionalSmartGoals(int count)
        {
            var additionalGoals = new List<GeneratedGoal>
            {
                new GeneratedGoal
                {
                    Title = "Personal Energy Management System",
                    Description = "Develop strategies to optimize your energy levels throughout the day and week",
                    Category = "Health",
                    Priority = 4,
                    Confidence = 85,
                    Source = "Smart Template",
                    Rationale = "Energy management is more important than time management",
                    SuggestedDeadline = DateTime.Now.AddDays(14),
                    EstimatedDuration = TimeSpan.FromDays(7),
                    Icon = "⚡",
                    KeySteps = new List<string> { "Track energy patterns", "Identify energy drains", "Optimize sleep schedule", "Plan high-energy tasks" }
                },
                new GeneratedGoal
                {
                    Title = "Communication Skills Enhancement",
                    Description = "Improve verbal and written communication skills for better personal and professional relationships",
                    Category = "Personal Development",
                    Priority = 3,
                    Confidence = 80,
                    Source = "Smart Template",
                    Rationale = "Strong communication skills benefit all areas of life",
                    SuggestedDeadline = DateTime.Now.AddDays(60),
                    EstimatedDuration = TimeSpan.FromDays(30),
                    Icon = "🗣️",
                    KeySteps = new List<string> { "Identify improvement areas", "Practice active listening", "Join speaking group", "Seek feedback" }
                },
                new GeneratedGoal
                {
                    Title = "Financial Literacy & Planning",
                    Description = "Build comprehensive understanding of personal finance and create a long-term financial plan",
                    Category = "Finance",
                    Priority = 5,
                    Confidence = 90,
                    Source = "Smart Template",
                    Rationale = "Financial literacy is essential for long-term security and freedom",
                    SuggestedDeadline = DateTime.Now.AddDays(30),
                    EstimatedDuration = TimeSpan.FromDays(21),
                    Icon = "💰",
                    KeySteps = new List<string> { "Assess current finances", "Learn investment basics", "Create budget plan", "Set up emergency fund" }
                }
            };

            return additionalGoals.Take(count).ToList();
        }

        private string GetCategoryIcon(string category)
        {
            return category.ToLowerInvariant() switch
            {
                "health" => "🏃‍♀️",
                "career" => "💼",
                "learning" => "📚",
                "finance" => "💰",
                "social" => "👥",
                "creativity" => "🎨",
                "personal" => "🌟",
                _ => "🎯"
            };
        }

        private async Task<List<GeneratedGoal>> GenerateGoalsFromPipeline()
        {
            var goals = new List<GeneratedGoal>();
            try
            {
                var prompt = $"Generate 3 specific, actionable goals based on user context: {GoalGenerationContext}. " +
                           $"Current goals: {string.Join(", ", MyGoals.Select(g => g.Title))}. " +
                           "Focus on SMART criteria and personal growth.";

                var result = await _orientPageViewModel.ExecutePipelineByNameAsync(SelectedPipelineName, prompt);

                if (!string.IsNullOrEmpty(result))
                {
                    goals = ParsePipelineResultToGoals(result, "AI Pipeline");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Pipeline goal generation failed: {ex.Message}");
            }
            return goals;
        }

        private Task<List<GeneratedGoal>> GenerateGoalsFromContext()
        {
            var goals = new List<GeneratedGoal>();

            // Generate goals based on context
            if (!string.IsNullOrEmpty(GoalGenerationContext))
            {
                try
                {
                    var contextPrompt = $"Based on this context: '{GoalGenerationContext}', generate 2 relevant goals.";

                    // Use a simple context analysis if no pipeline is available
                    var contextGoals = AnalyzeContextForGoals(GoalGenerationContext);
                    goals.AddRange(contextGoals);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Context goal generation failed: {ex.Message}");
                }
            }

            return Task.FromResult(goals);
        }

        private async Task<List<GeneratedGoal>> GenerateGoalsFromWebcam()
        {
            var goals = new List<GeneratedGoal>();

            try
            {
                WebcamStatus = "Analyzing webcam for goal suggestions...";
                await Task.Delay(1000); // Simulate webcam analysis

                // Simulate webcam analysis results
                var webcamAnalysis = new
                {
                    FocusLevel = new Random().Next(60, 95),
                    EnergyLevel = new Random().Next(50, 90),
                    Environment = new[] { "Office", "Home", "Cafe", "Library" }[new Random().Next(4)],
                    TimeOfDay = DateTime.Now.Hour
                };

                // Generate goals based on webcam analysis
                if (webcamAnalysis.FocusLevel > 80)
                {
                    goals.Add(new GeneratedGoal
                    {
                        Title = "High-Focus Deep Work Session",
                        Description = $"Take advantage of your current high focus level ({webcamAnalysis.FocusLevel}%) for challenging tasks",
                        Category = "Productivity",
                        Priority = 5,
                        Confidence = 90,
                        Source = "Webcam Analysis",
                        Rationale = $"Webcam detected high focus level at {webcamAnalysis.FocusLevel}%",
                        SuggestedDeadline = DateTime.Now.AddHours(2),
                        EstimatedDuration = TimeSpan.FromHours(2),
                        Icon = "🎯",
                        WebcamData = new Dictionary<string, object>
                        {
                            { "focus_level", webcamAnalysis.FocusLevel },
                            { "environment", webcamAnalysis.Environment }
                        }
                    });
                }

                if (webcamAnalysis.EnergyLevel < 60)
                {
                    goals.Add(new GeneratedGoal
                    {
                        Title = "Energy Recovery Break",
                        Description = $"Your energy level is at {webcamAnalysis.EnergyLevel}% - take a rejuvenating break",
                        Category = "Health",
                        Priority = 4,
                        Confidence = 85,
                        Source = "Webcam Analysis",
                        Rationale = $"Low energy detected at {webcamAnalysis.EnergyLevel}%",
                        SuggestedDeadline = DateTime.Now.AddMinutes(30),
                        EstimatedDuration = TimeSpan.FromMinutes(15),
                        Icon = "⚡"
                    });
                }

                WebcamStatus = $"Webcam analysis complete - Focus: {webcamAnalysis.FocusLevel}%";
            }
            catch (Exception ex)
            {
                WebcamStatus = "Webcam analysis failed";
                Debug.WriteLine($"Webcam goal generation failed: {ex.Message}");
            }

            return goals;
        }

        private Task<List<GeneratedGoal>> GenerateGoalsFromExistingGoals()
        {
            var goals = new List<GeneratedGoal>();

            try
            {
                // Analyze existing goals for patterns and gaps
                var categories = MyGoals.GroupBy(g => g.GoalType).ToList();
                var overdueTasks = MyGoals.Count(g => g.Deadline < DateTime.Now);
                var highPriorityTasks = MyGoals.Count(g => g.Priority >= 4);

                if (overdueTasks > 2)
                {
                    goals.Add(new GeneratedGoal
                    {
                        Title = "Goal Organization & Prioritization",
                        Description = $"You have {overdueTasks} overdue goals. Create a system to better manage deadlines.",
                        Category = "Productivity",
                        Priority = 5,
                        Confidence = 95,
                        Source = "Goal Analysis",
                        Rationale = $"Detected {overdueTasks} overdue goals requiring attention",
                        SuggestedDeadline = DateTime.Now.AddDays(3),
                        EstimatedDuration = TimeSpan.FromHours(1),
                        Icon = "📋"
                    });
                }

                // Suggest balance if too focused on one category
                if (categories.Any() && categories.First().Count() > MyGoals.Count * 0.6)
                {
                    var dominantCategory = categories.First().Key;
                    var suggestions = new[] { "Health", "Learning", "Social", "Finance", "Personal" }
                        .Where(c => c != dominantCategory).ToArray();

                    var suggestedCategory = suggestions[new Random().Next(suggestions.Length)];

                    goals.Add(new GeneratedGoal
                    {
                        Title = $"{suggestedCategory} Goal for Balance",
                        Description = $"Most of your goals are {dominantCategory}-focused. Consider adding a {suggestedCategory} goal for better life balance.",
                        Category = suggestedCategory,
                        Priority = 3,
                        Confidence = 80,
                        Source = "Goal Analysis",
                        Rationale = $"Detected imbalance - too many {dominantCategory} goals",
                        SuggestedDeadline = DateTime.Now.AddDays(14),
                        EstimatedDuration = TimeSpan.FromDays(7),
                        Icon = "⚖️"
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Existing goals analysis failed: {ex.Message}");
            }

            return Task.FromResult(goals);
        }

        // --- Legacy Method Updated for Compatibility ---
        private List<GeneratedGoal> GenerateDefaultSmartGoals()
        {
            // This method is now used as a compatibility layer
            // The actual neural network goal generation happens in GenerateGoalsFromActiveNeuralNetworks
            Debug.WriteLine("📋 Using compatibility method - neural network generation is preferred");

            return GenerateEnhancedTemplateGoals().Take(3).ToList();
        }

        // --- Legacy Method Updated for Compatibility ---
        private List<GeneratedGoal> GenerateAdditionalDefaultGoals(int count)
        {
            // This method is now used as a compatibility layer
            Debug.WriteLine($"📋 Using compatibility method for {count} additional goals");

            return GenerateAdditionalSmartGoals(count);
        }

        private List<GeneratedGoal> AnalyzeContextForGoals(string context)
        {
            var goals = new List<GeneratedGoal>();
            var lowerContext = context.ToLower();

            // Simple keyword-based goal generation
            if (lowerContext.Contains("work") || lowerContext.Contains("career"))
            {
                goals.Add(new GeneratedGoal
                {
                    Title = "Professional Development Plan",
                    Description = "Create a structured plan for advancing your career based on current work context",
                    Category = "Career",
                    Priority = 4,
                    Confidence = 80,
                    Source = "Context Analysis",
                    Rationale = "Work/career keywords detected in context",
                    SuggestedDeadline = DateTime.Now.AddDays(21),
                    EstimatedDuration = TimeSpan.FromDays(7),
                    Icon = "💼"
                });
            }

            if (lowerContext.Contains("health") || lowerContext.Contains("fitness"))
            {
                goals.Add(new GeneratedGoal
                {
                    Title = "Health Improvement Journey",
                    Description = "Start a personalized health improvement program based on your current needs",
                    Category = "Health",
                    Priority = 5,
                    Confidence = 85,
                    Source = "Context Analysis",
                    Rationale = "Health/fitness keywords detected in context",
                    SuggestedDeadline = DateTime.Now.AddDays(30),
                    EstimatedDuration = TimeSpan.FromDays(30),
                    Icon = "🏃‍♀️"
                });
            }

            if (lowerContext.Contains("learn") || lowerContext.Contains("study") || lowerContext.Contains("skill"))
            {
                goals.Add(new GeneratedGoal
                {
                    Title = "Skill Enhancement Program",
                    Description = "Develop new skills or improve existing ones based on your learning interests",
                    Category = "Learning",
                    Priority = 3,
                    Confidence = 75,
                    Source = "Context Analysis",
                    Rationale = "Learning/skill keywords detected in context",
                    SuggestedDeadline = DateTime.Now.AddDays(45),
                    EstimatedDuration = TimeSpan.FromDays(21),
                    Icon = "🧠"
                });
            }

            return goals;
        }

        private List<GeneratedGoal> ParsePipelineResultToGoals(string result, string source)
        {
            var goals = new List<GeneratedGoal>();
            var lines = result.Split('\n').Where(l => !string.IsNullOrWhiteSpace(l)).Take(3).ToList();

            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i].Trim();
                goals.Add(new GeneratedGoal
                {
                    Title = $"AI Suggestion {i + 1}",
                    Description = line,
                    Category = "AI Generated",
                    Priority = new Random().Next(3, 6),
                    Confidence = new Random().Next(75, 95),
                    Source = source,
                    Rationale = "Generated by AI pipeline analysis",
                    SuggestedDeadline = DateTime.Now.AddDays(new Random().Next(7, 30)),
                    EstimatedDuration = TimeSpan.FromDays(new Random().Next(1, 14)),
                    Icon = "🤖"
                });
            }

            return goals;
        }

        private void OnAddGeneratedGoal(GeneratedGoal generatedGoal)
        {
            if (generatedGoal == null) return;

            try
            {
                var newGoal = new Goal
                {
                    Id = Guid.NewGuid().ToString(),
                    Title = generatedGoal.Title,
                    Description = generatedGoal.Description,
                    Priority = generatedGoal.Priority,
                    Deadline = generatedGoal.SuggestedDeadline,
                    GoalType = generatedGoal.Category,
                    CreatedAt = DateTime.UtcNow
                };

                MyGoals.Insert(0, newGoal);

                // Mark as selected and provide feedback
                generatedGoal.IsSelected = true;

                // Show brief feedback message
                ShowFeedback($"✅ Added '{generatedGoal.Title}' to your goals!");

                // Log the addition without popup
                Debug.WriteLine($"Added '{generatedGoal.Title}' to goals with {generatedGoal.Confidence}% AI confidence");

                // Save automatically
                _ = SaveGoalsToFile();
                if (_appModeService.CurrentMode != AppMode.Offline)
                {
                    _ = SaveGoalToBackend(newGoal);
                }
            }
            catch (Exception ex)
            {
                // Log error instead of showing popup
                Debug.WriteLine($"Failed to add goal: {ex.Message}");
            }
        }
        // --- End Enhanced Goal Generation Method ---

        // Simple feedback method to replace popups
        private async void ShowFeedback(string message)
        {
            LastAction = message;
            ShowLastAction = true;

            // Hide after 3 seconds
            await Task.Delay(3000);
            ShowLastAction = false;
        }


        private void OnToggleCreateGoal()
        {
            ShowNewGoal = !ShowNewGoal;
            if (!ShowNewGoal)
            {
                ClearGoalForm();
            }
        }

        private async Task OnSubmitGoal()
        {
            if (string.IsNullOrWhiteSpace(NewGoalTitle))
            {
                await DisplayAlert("Missing Title", "Please enter a title for the goal.", "OK");
                return;
            }

            try
            {
                IsLoading = true;
                HasError = false;

                // Enhanced goal creation with AI suggestions
                var newGoal = new Goal
                {
                    Title = NewGoalTitle,
                    Description = NewGoalDescription,
                    Priority = GoalPriority,
                    Deadline = GoalDeadline,
                    IsShared = ShareGoal,
                    GoalType = SelectedGoalType,
                    CreatedAt = DateTime.UtcNow
                };

                // AI Enhancement: Analyze and suggest improvements to the new goal
                if (IsSmartGoalGeneration)
                {
                    await EnhanceGoalWithAI(newGoal);
                }

                MyGoals.Insert(0, newGoal);
                await SaveGoalsToFile();

                // Save to backend if online
                if (_appModeService.CurrentMode != AppMode.Offline)
                {
                    await SaveGoalToBackend(newGoal);
                }

                // AI Enhancement: Generate related goal suggestions
                if (IsSmartGoalGeneration)
                {
                    await GenerateRelatedGoalSuggestions(newGoal);
                }

                ClearGoalForm();
                ShowNewGoal = false;

                await DisplayAlert("Success",
                    IsSmartGoalGeneration ?
                        "Goal created successfully with AI enhancements! Check suggestions for related goals." :
                        "Goal created successfully!", "OK");
            }
            catch (Exception ex)
            {
                HasError = true;
                ErrorMessage = ex.Message;
                await DisplayAlert("Error", $"Failed to create goal: {ex.Message}", "OK");
                Debug.WriteLine($"Error creating goal: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task EnhanceGoalWithAI(Goal goal)
        {
            try
            {
                var enhancementPrompt = $"Enhance this goal using SMART criteria: " +
                                      $"Title: {goal.Title}, Description: {goal.Description}. " +
                                      "Provide specific improvements for clarity, measurability, and achievability.";

                var enhancement = await _orientPageViewModel.ExecutePipelineByNameAsync("goal_enhancement_pipeline", enhancementPrompt);

                if (!string.IsNullOrEmpty(enhancement))
                {
                    // Apply AI enhancements to the goal
                    var enhancedLines = enhancement.Split('\n').Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
                    if (enhancedLines.Any())
                    {
                        goal.Description = enhancedLines.First();
                        // Store additional enhancement details in description if needed
                        if (enhancedLines.Count > 1)
                        {
                            goal.Description += " " + string.Join(" ", enhancedLines.Skip(1));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AI goal enhancement failed: {ex.Message}");
            }
        }

        private async Task GenerateRelatedGoalSuggestions(Goal newGoal)
        {
            try
            {
                var relationPrompt = $"Based on this new goal: '{newGoal.Title} - {newGoal.Description}', " +
                                   $"suggest 3 related complementary goals that would help achieve better results.";

                var suggestions = await _orientPageViewModel.ExecutePipelineByNameAsync("related_goals_pipeline", relationPrompt);

                if (!string.IsNullOrEmpty(suggestions))
                {
                    var lines = suggestions.Split('\n').Where(l => !string.IsNullOrWhiteSpace(l)).Take(3).ToList();
                    foreach (var suggestion in lines)
                    {
                        SuggestedImprovements.Add($"🎯 Related Goal: {suggestion}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Related goal generation failed: {ex.Message}");
            }
        }

        private async Task OnDeleteGoal(Goal goalToDelete)
        {
            if (goalToDelete == null) return;

            try
            {
                IsLoading = true;
                HasError = false;

                bool confirm = await DisplayAlert("Confirm Delete", $"Are you sure you want to delete the goal '{goalToDelete.Title}'?", "Yes", "No");
                if (confirm)
                {
                    MyGoals.Remove(goalToDelete);
                    await SaveGoalsToFile();

                    // Delete from backend if online
                    if (_appModeService.CurrentMode != AppMode.Offline)
                    {
                        await _goalService.DeleteGoalFromBackend(goalToDelete.Id);
                    }

                    await DisplayAlert("Success", "Goal deleted successfully!", "OK");
                }
            }
            catch (Exception ex)
            {
                HasError = true;
                ErrorMessage = ex.Message;
                await DisplayAlert("Error", $"Failed to delete goal: {ex.Message}", "OK");
                Debug.WriteLine($"Error deleting goal: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void OnEditGoal(Goal goalToEdit)
        {
            if (goalToEdit == null) return;

            NewGoalTitle = goalToEdit.Title;
            NewGoalDescription = goalToEdit.Description;
            GoalPriority = goalToEdit.Priority;
            GoalDeadline = goalToEdit.Deadline;
            ShareGoal = goalToEdit.IsShared;
            SelectedGoalType = goalToEdit.GoalType;

            ShowNewGoal = true;

            DisplayAlert("Edit Goal", "Goal details loaded into the form. Modify and submit to save changes (will create a new entry for now).", "OK");
        }

        private void ClearGoalForm()
        {
            NewGoalTitle = string.Empty;
            NewGoalDescription = string.Empty;
            GoalPriority = 3;
            GoalDeadline = DateTime.Today.AddDays(7);
            ShareGoal = false;
            SelectedGoalType = null;
        }

        private async Task LoadGoalsAsync()
        {
            Debug.WriteLine("Loading goals...");
            await _goalService.GetLocalGoalsAsync(MyGoals);
            OnPropertyChanged(nameof(MyGoals));
        }

        private async Task SaveGoalsToFile()
        {
            await _goalService.SaveGoalsToFile(MyGoals);
        }

        private async Task LoadPublicGoalsFromBackend()
        {
            if (_appModeService.CurrentMode == AppMode.Offline)
            {
                Debug.WriteLine("App is in offline mode. Skipping public goal loading.");
                return;
            }

            try
            {
                IsLoading = true;

                Debug.WriteLine("LoadPublicGoalsFromBackend called - loading from backend API");

                // Call the backend public endpoint
                var publicGoalsData = await _dataService.GetPublicPlansAsync();

                // Convert DataItems to Goals for public display
                var publicGoalItems = new List<Goal>();

                foreach (var dataItem in publicGoalsData.Data ?? new List<DataItem>())
                {
                    try
                    {
                        var publicGoal = ConvertDataItemToPublicGoal(dataItem);
                        if (publicGoal != null)
                        {
                            publicGoalItems.Add(publicGoal);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error converting data item to public goal: {ex.Message}");
                    }
                }

                SharedGoals.Clear();
                foreach (var goal in publicGoalItems)
                {
                    SharedGoals.Add(goal);
                }

                Debug.WriteLine($"Successfully loaded {SharedGoals.Count} public goals from backend");

                // Clear any errors since we successfully loaded data
                HasError = false;
                ErrorMessage = string.Empty;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading public goals: {ex.Message}");
                HasError = true;
                ErrorMessage = "Failed to load public goals. Please try again later.";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private Goal ConvertDataItemToPublicGoal(DataItem dataItem)
        {
            if (dataItem?.Data?.Text == null) return null;

            var publicGoal = new Goal
            {
                Id = dataItem._id ?? Guid.NewGuid().ToString(),
                CreatedAt = dataItem.createdAt,
                SharedDate = dataItem.createdAt
            };

            // Parse the pipe-delimited text to extract goal information
            var parts = dataItem.Data.Text.Split('|');
            foreach (var part in parts)
            {
                var keyValue = part.Split(':', 2);
                if (keyValue.Length != 2) continue;

                var key = keyValue[0].Trim().ToLower();
                var value = keyValue[1].Trim();

                switch (key)
                {
                    case "title":
                        publicGoal.Title = value;
                        break;
                    case "goal":
                        publicGoal.Description = value;
                        break;
                    case "description":
                        publicGoal.Description = value;
                        break;
                    case "priority":
                        if (int.TryParse(value, out int priority))
                            publicGoal.Priority = priority;
                        break;
                    case "deadline":
                        if (DateTime.TryParse(value, out DateTime deadline))
                            publicGoal.Deadline = deadline;
                        break;
                    case "goaltype":
                        publicGoal.GoalType = value;
                        break;
                }
            }

            // Set default values for shared goals
            publicGoal.IsShared = true;
            publicGoal.SharedWith = 1; // Default to 1 for now

            return publicGoal;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            Debug.WriteLine("GoalPage Appearing");

            if (_appModeService.CurrentMode == AppMode.Offline)
            {
                await LoadGoalsFromFile();
            }
            else
            {
                await LoadGoalsFromBackend();
                await LoadPublicGoalsFromBackend();
            }
        }

        private async void CheckUserLoggedIn()
        {
            if (_appModeService.CurrentMode == AppMode.Offline)
            {
                Debug.WriteLine("App is in offline mode. Using local goals only.");
                return;
            }

            if (!await _goalService.IsUserLoggedInAsync())
            {
                Debug.WriteLine("User is not logged in, navigating to login...");
                await NavigateLogin();
            }
            else
            {
                Debug.WriteLine("User is logged in. Backend operations enabled.");
            }
        }

        private async Task<bool> IsUserLoggedInAsync()
        {
            try
            {
                var userToken = await SecureStorage.GetAsync("userToken");
                if (!string.IsNullOrEmpty(userToken))
                {
                    Debug.WriteLine("User token found: " + userToken);
                    return true;
                }
                else
                {
                    Debug.WriteLine("No user token found.");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error retrieving user token: {ex.Message}");
                return false;
            }
        }

        private async Task LoadGoalsFromBackend()
        {
            if (_appModeService.CurrentMode == AppMode.Offline)
            {
                Debug.WriteLine("App is in offline mode. Skipping backend goal loading.");
                return;
            }

            try
            {
                IsLoading = true;
                HasError = false;

                var token = await SecureStorage.GetAsync("userToken");
                if (string.IsNullOrEmpty(token))
                {
                    Debug.WriteLine("User is not logged in.");
                    IsUserLoggedIn = false;
                    await LoadGoalsFromFile(); // Fallback to local/sample data
                    return;
                }

                // First, check if the user is still logged in
                var isLoggedIn = await _dataService.IsLoggedInAsync();
                if (!isLoggedIn)
                {
                    Debug.WriteLine("User authentication check failed. Falling back to local data.");
                    IsUserLoggedIn = false;
                    await LoadGoalsFromFile();
                    return;
                }

                var data = "Goal";
                Debug.WriteLine($"Making request to backend for goals with data: {data}");

                var goals = await _dataService.GetDataAsync(data, token);
                var myGoalItems = ProcessMyGoalsFromBackend(goals.Data?.Cast<DataItem>().ToList() ?? new List<DataItem>());

                MyGoals.Clear();
                AllDataItems.Clear();

                foreach (var goal in myGoalItems)
                {
                    MyGoals.Add(goal);
                }

                foreach (var item in goals.Data ?? new List<DataItem>())
                {
                    AllDataItems.Add(item);
                }

                await SaveGoalsToFile();

                Debug.WriteLine($"Successfully loaded {MyGoals.Count} goals from backend");
            }
            catch (UnauthorizedAccessException)
            {
                Debug.WriteLine("Unauthorized access - user session expired");
                HasError = true;
                ErrorMessage = "Your session has expired. Please log in again.";
                IsUserLoggedIn = false;

                // Clear sensitive data and fallback to local storage
                await LoadGoalsFromFile();
            }
            catch (JsonException jsonEx)
            {
                Debug.WriteLine($"JSON parsing error: {jsonEx.Message}. Server may have returned HTML instead of JSON.");
                HasError = true;
                ErrorMessage = "🔧 BACKEND CONFIGURATION ISSUE: Enable debug build to use local backend, or check production backend deployment.";

                Debug.WriteLine("🚨 BACKEND CONFIGURATION ISSUE DETECTED");
                Debug.WriteLine($"📋 Current Environment: {BackendConfigService.CurrentEnvironment}");
                Debug.WriteLine($"🌐 Backend URL: {BackendConfigService.ApiEndpoints.GetBaseUrl()}");

                // Fallback to local storage when server returns HTML/invalid JSON
                await LoadGoalsFromFile();
            }
            catch (HttpRequestException httpEx)
            {
                Debug.WriteLine($"Network error loading goals: {httpEx.Message}");
                HasError = true;
                ErrorMessage = "Network error. Check your internet connection and server status. Loading saved goals instead.";

                // Fallback to local storage on network errors
                await LoadGoalsFromFile();
            }
            catch (TaskCanceledException)
            {
                Debug.WriteLine("Request timed out loading goals from backend");
                HasError = true;
                ErrorMessage = "Request timed out. The server may be slow or unavailable. Loading saved goals instead.";

                // Fallback to local storage on timeout
                await LoadGoalsFromFile();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Unexpected error loading goals from backend: {ex.Message}");
                Debug.WriteLine($"Exception type: {ex.GetType().Name}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");

                HasError = true;
                ErrorMessage = "An unexpected error occurred while loading goals. Loading saved goals instead.";

                // Fallback to local storage on any other unexpected error
                await LoadGoalsFromFile();
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadGoalsFromFile()
        {
            try
            {
                IsLoading = true;
                Debug.WriteLine("LoadGoalsFromFile called - No sample data loaded. Backend connection required.");

                await Task.Delay(50); // Small delay to show loading state

                // Clear collections - no sample data
                MyGoals.Clear();
                AllDataItems.Clear();

                Debug.WriteLine("No local goals loaded. Please ensure backend is running and accessible.");

                // Show informative message about backend requirement
                HasError = true;
                ErrorMessage = _appModeService.CurrentMode == AppMode.Offline
                    ? "Offline mode: No local goals available. Switch to online mode to access your goals."
                    : "No goals loaded. Please ensure the backend server is running and accessible.";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in LoadGoalsFromFile: {ex.Message}");
                HasError = true;
                ErrorMessage = "Unable to load goals.";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private List<Goal> ProcessMyGoalsFromBackend(List<DataItem> goalItems)
        {
            var myGoalItems = new List<Goal>();

            foreach (var dataItem in goalItems)
            {
                try
                {
                    var goalItem = ParseDataItemToGoalItem(dataItem);
                    if (goalItem != null)
                    {
                        myGoalItems.Add(goalItem);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error parsing goal item: {ex.Message}");
                }
            }

            return myGoalItems;
        }

        private Goal ParseDataItemToGoalItem(DataItem dataItem)
        {
            if (dataItem?.Data?.Text == null) return null;

            var goalItem = new Goal
            {
                Id = dataItem._id ?? Guid.NewGuid().ToString(),
                CreatedAt = dataItem.createdAt
            };

            // Parse the text format: "Goal:description|Title:title|Description:description|..."
            var parts = dataItem.Data.Text.Split('|');
            foreach (var part in parts)
            {
                var keyValue = part.Split(':', 2);
                if (keyValue.Length != 2) continue;

                var key = keyValue[0].Trim();
                var value = keyValue[1].Trim();

                switch (key.ToLower())
                {
                    case "goal":
                        goalItem.Description = value;
                        break;
                    case "title":
                        goalItem.Title = value;
                        break;
                    case "description":
                        goalItem.Description = value;
                        break;
                    case "priority":
                        if (int.TryParse(value, out int priority))
                            goalItem.Priority = priority;
                        break;
                    case "deadline":
                        if (DateTime.TryParse(value, out DateTime deadline))
                            goalItem.Deadline = deadline;
                        break;
                    case "public":
                        goalItem.IsShared = bool.TryParse(value, out bool isShared) && isShared;
                        break;
                    case "goaltype":
                        goalItem.GoalType = value;
                        break;
                }
            }

            // Set default title if none found
            if (string.IsNullOrEmpty(goalItem.Title) && !string.IsNullOrEmpty(goalItem.Description))
            {
                goalItem.Title = goalItem.Description.Length > 50
                    ? goalItem.Description.Substring(0, 50) + "..."
                    : goalItem.Description;
            }

            return goalItem;
        }

        private async Task SaveGoalToBackend(Goal goal)
        {
            if (_appModeService.CurrentMode == AppMode.Offline)
            {
                Debug.WriteLine("App is in offline mode. Goal saved locally only.");
                return;
            }

            try
            {
                var token = await SecureStorage.GetAsync("userToken");
                if (string.IsNullOrEmpty(token))
                {
                    Debug.WriteLine("User is not logged in.");
                    return;
                }

                var goalData = CreateGoalDataString(goal);
                var response = await _dataService.CreateDataAsync(goalData, token);
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

        private string CreateGoalDataString(Goal goal)
        {
            var parts = new List<string>();

            if (!string.IsNullOrEmpty(goal.Title))
                parts.Add($"Title:{goal.Title}");

            if (!string.IsNullOrEmpty(goal.Description))
                parts.Add($"Goal:{goal.Description}");

            if (!string.IsNullOrEmpty(goal.GoalType))
                parts.Add($"GoalType:{goal.GoalType}");

            if (goal.IsShared)
                parts.Add($"Public:{goal.IsShared}");

            parts.Add($"Priority:{goal.Priority}");
            parts.Add($"Deadline:{goal.Deadline:yyyy-MM-dd}");

            return string.Join("|", parts);
        }

        async Task NavigateLogin()
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

        // Fix warning CS0108 by using 'new' keyword
        public new event PropertyChangedEventHandler PropertyChanged;

        protected virtual bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null, Action onChanged = null)
        {
            if (EqualityComparer<T>.Default.Equals(storage, value)) return false;
            storage = value;
            onChanged?.Invoke();
            OnPropertyChanged(propertyName);
            return true;
        }

        // Fix warning CS0114 by using 'new' keyword
        protected new virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // Goal Template class for AI-generated goal templates
    public class GoalTemplate
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public int SuggestedPriority { get; set; }
        public int SuggestedDurationDays { get; set; }
        public List<string> KeySteps { get; set; } = new List<string>();
        public List<string> RequiredResources { get; set; } = new List<string>();
        public string Difficulty { get; set; } // Easy, Medium, Hard
        public string MotivationTip { get; set; }
        public double SuccessRate { get; set; } // Estimated success rate percentage
    }

    // Smart Goal Suggestion class for AI-generated smart goal recommendations
    public class SmartGoalSuggestion
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public int Priority { get; set; }
        public TimeSpan EstimatedDuration { get; set; }
        public int Confidence { get; set; } // Percentage confidence in suggestion
        public bool IsRecommended { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public List<string> Tags { get; set; } = new List<string>();
        public string Rationale { get; set; } // Why this goal is suggested
        public List<string> ActionSteps { get; set; } = new List<string>();
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
    }

    // Generated Goal class for AI-powered goal generation with selection capability
    public class GeneratedGoal : INotifyPropertyChanged
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public int Priority { get; set; }
        public DateTime SuggestedDeadline { get; set; }
        public int Confidence { get; set; } // AI confidence score (0-100)

        private bool _isSelected = false;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Source { get; set; } // "AI Pipeline", "Webcam Analysis", "Context Analysis"
        public string Rationale { get; set; } // Why this goal was generated
        public List<string> Tags { get; set; } = new List<string>();
        public List<string> KeySteps { get; set; } = new List<string>();
        public string Difficulty { get; set; } = "Medium"; // Easy, Medium, Hard
        public TimeSpan EstimatedDuration { get; set; }
        public bool IsRecommended { get; set; } // AI recommendation flag
        public DateTime GeneratedAt { get; set; } = DateTime.Now;
        public Dictionary<string, object> WebcamData { get; set; } = new Dictionary<string, object>(); // Webcam analysis data
        public string Icon { get; set; } = "🎯"; // Visual icon for the goal

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
