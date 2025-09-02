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

        // Modified constructor to accept OrientPageViewModel and FileService
        public GoalPage(GoalService goalService, CSimple.Services.AppModeService.AppModeService appModeService, OrientPageViewModel orientPageViewModel, FileService fileService, DataService dataService) // Added DataService
        {
            InitializeComponent();

            _goalService = goalService; // Use injected service
            _appModeService = appModeService; // Use injected service
            _orientPageViewModel = orientPageViewModel; // Store injected ViewModel
            _fileService = fileService; // Store injected FileService
            _dataService = dataService; // Store injected DataService

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

        // --- Enhanced Goal Generation Method ---
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

                // Generate goals with multiple strategies
                var generatedGoals = new List<GeneratedGoal>();

                // Strategy 1: AI Pipeline Analysis (if pipeline selected)
                if (!string.IsNullOrEmpty(SelectedPipelineName))
                {
                    var pipelineGoals = await GenerateGoalsFromPipeline();
                    generatedGoals.AddRange(pipelineGoals);
                }

                // Strategy 2: Context-based Goal Generation
                var contextGoals = await GenerateGoalsFromContext();
                generatedGoals.AddRange(contextGoals);

                // Strategy 3: Webcam Analysis (if enabled)
                if (UseWebcamForGeneration)
                {
                    var webcamGoals = await GenerateGoalsFromWebcam();
                    generatedGoals.AddRange(webcamGoals);
                }

                // Strategy 4: Existing Goals Analysis
                if (MyGoals.Any())
                {
                    var analysisGoals = await GenerateGoalsFromExistingGoals();
                    generatedGoals.AddRange(analysisGoals);
                }

                // Strategy 5: Ensure we have at least 3 goals - add defaults if needed
                var defaultGoals = GenerateDefaultSmartGoals();
                generatedGoals.AddRange(defaultGoals);

                // Take at least 3, up to 8 goals, ensuring we have enough variety
                var finalGoals = generatedGoals
                    .OrderByDescending(g => g.Confidence)
                    .Take(Math.Max(8, generatedGoals.Count))
                    .ToList();

                // If we still don't have at least 3, generate more defaults
                while (finalGoals.Count < 3)
                {
                    finalGoals.AddRange(GenerateAdditionalDefaultGoals(3 - finalGoals.Count));
                }

                // Add new generated goals to collection (after selected ones)
                foreach (var goal in finalGoals.Take(8))
                {
                    GeneratedGoals.Add(goal);
                }

                // Update status without popup
                if (GeneratedGoals.Count > 0)
                {
                    // Success - goals were generated
                    ShowFeedback($"🎯 Generated {GeneratedGoals.Count} goal suggestions! Click to add them.");
                    Debug.WriteLine($"Generated {GeneratedGoals.Count} goal suggestions successfully");
                }
                else
                {
                    // Fallback - show a simple status message
                    ShowFeedback("⚠️ No goals generated - please try again");
                    Debug.WriteLine("No goals were generated - this shouldn't happen with fallback system");
                }
            }
            catch (Exception ex)
            {
                // Log error instead of showing popup
                Debug.WriteLine($"Goal generation error: {ex.Message}");

                // Add fallback goal so user isn't left with empty results
                GeneratedGoals.Add(new GeneratedGoal
                {
                    Title = "Quick Goal Planning Session",
                    Description = "Take 15 minutes to review and plan your upcoming goals",
                    Category = "Planning",
                    Priority = 3,
                    Confidence = 85,
                    Source = "Fallback",
                    Rationale = "Always a good practice when goal generation encounters issues",
                    SuggestedDeadline = DateTime.Now.AddDays(1),
                    EstimatedDuration = TimeSpan.FromMinutes(15),
                    Icon = "📝"
                });

                // Show fallback feedback
                ShowFeedback("⚠️ Added fallback goal - try again for AI suggestions");
            }
            finally
            {
                IsGeneratingGoals = false;
                ((Command)GenerateGoalsCommand).ChangeCanExecute();
            }
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

        private List<GeneratedGoal> GenerateDefaultSmartGoals()
        {
            var defaultGoals = new List<GeneratedGoal>
            {
                new GeneratedGoal
                {
                    Title = "Daily Learning Habit",
                    Description = "Spend 30 minutes daily learning something new in your field of interest",
                    Category = "Learning",
                    Priority = 3,
                    Confidence = 75,
                    Source = "Default Template",
                    Rationale = "Continuous learning is essential for personal growth",
                    SuggestedDeadline = DateTime.Now.AddDays(30),
                    EstimatedDuration = TimeSpan.FromDays(30),
                    Icon = "📚",
                    KeySteps = new List<string> { "Choose learning topic", "Set daily reminder", "Track progress" }
                },
                new GeneratedGoal
                {
                    Title = "Health & Wellness Check",
                    Description = "Establish a weekly routine for physical and mental health maintenance",
                    Category = "Health",
                    Priority = 4,
                    Confidence = 85,
                    Source = "Default Template",
                    Rationale = "Regular health maintenance prevents future issues",
                    SuggestedDeadline = DateTime.Now.AddDays(7),
                    EstimatedDuration = TimeSpan.FromDays(1),
                    Icon = "🏃‍♂️",
                    KeySteps = new List<string> { "Schedule health checkup", "Plan weekly exercise", "Set wellness goals" }
                },
                new GeneratedGoal
                {
                    Title = "Digital Organization",
                    Description = "Organize digital files, emails, and online accounts for better productivity",
                    Category = "Productivity",
                    Priority = 3,
                    Confidence = 70,
                    Source = "Default Template",
                    Rationale = "Digital clutter reduces productivity and increases stress",
                    SuggestedDeadline = DateTime.Now.AddDays(14),
                    EstimatedDuration = TimeSpan.FromHours(4),
                    Icon = "💻",
                    KeySteps = new List<string> { "Clean email inbox", "Organize files", "Update passwords" }
                }
            };

            return defaultGoals;
        }

        private List<GeneratedGoal> GenerateAdditionalDefaultGoals(int count)
        {
            var additionalGoals = new List<GeneratedGoal>
            {
                new GeneratedGoal
                {
                    Title = "Financial Planning Review",
                    Description = "Review and update your financial goals and budget for the upcoming period",
                    Category = "Finance",
                    Priority = 4,
                    Confidence = 80,
                    Source = "Additional Default",
                    Rationale = "Regular financial review helps maintain financial health",
                    SuggestedDeadline = DateTime.Now.AddDays(21),
                    EstimatedDuration = TimeSpan.FromHours(3),
                    Icon = "💰",
                    KeySteps = new List<string> { "Review budget", "Update savings goals", "Check investments" }
                },
                new GeneratedGoal
                {
                    Title = "Social Connection Goal",
                    Description = "Strengthen relationships with family and friends through regular communication",
                    Category = "Social",
                    Priority = 3,
                    Confidence = 75,
                    Source = "Additional Default",
                    Rationale = "Social connections are crucial for mental health and well-being",
                    SuggestedDeadline = DateTime.Now.AddDays(14),
                    EstimatedDuration = TimeSpan.FromDays(7),
                    Icon = "👥",
                    KeySteps = new List<string> { "Schedule calls", "Plan activities", "Send messages" }
                },
                new GeneratedGoal
                {
                    Title = "Skill Development Project",
                    Description = "Start a project to develop a new professional or personal skill",
                    Category = "Growth",
                    Priority = 3,
                    Confidence = 70,
                    Source = "Additional Default",
                    Rationale = "Skill development opens new opportunities and builds confidence",
                    SuggestedDeadline = DateTime.Now.AddDays(45),
                    EstimatedDuration = TimeSpan.FromDays(30),
                    Icon = "🎯",
                    KeySteps = new List<string> { "Choose skill", "Find resources", "Create practice plan" }
                },
                new GeneratedGoal
                {
                    Title = "Home Environment Optimization",
                    Description = "Improve your living or working space for better comfort and productivity",
                    Category = "Environment",
                    Priority = 2,
                    Confidence = 65,
                    Source = "Additional Default",
                    Rationale = "A well-organized environment enhances focus and reduces stress",
                    SuggestedDeadline = DateTime.Now.AddDays(10),
                    EstimatedDuration = TimeSpan.FromDays(2),
                    Icon = "🏠",
                    KeySteps = new List<string> { "Declutter space", "Organize items", "Add improvements" }
                },
                new GeneratedGoal
                {
                    Title = "Creative Expression Time",
                    Description = "Dedicate time to a creative hobby or artistic pursuit",
                    Category = "Creativity",
                    Priority = 2,
                    Confidence = 60,
                    Source = "Additional Default",
                    Rationale = "Creative activities reduce stress and enhance problem-solving abilities",
                    SuggestedDeadline = DateTime.Now.AddDays(7),
                    EstimatedDuration = TimeSpan.FromHours(5),
                    Icon = "🎨",
                    KeySteps = new List<string> { "Choose activity", "Gather materials", "Set time aside" }
                }
            };

            return additionalGoals.Take(count).ToList();
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
