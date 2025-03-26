using CSimple.Models;
using CSimple.Services;
using CSimple.Pages; // Add this import for ActionPage
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Maui.Controls;

namespace CSimple.ViewModels
{
    public class ActionPageViewModel : INotifyPropertyChanged
    {
        // Tab selection properties
        private bool _isAllActionsTabSelected = true;
        public bool IsAllActionsTabSelected
        {
            get => _isAllActionsTabSelected;
            set
            {
                if (_isAllActionsTabSelected != value)
                {
                    _isAllActionsTabSelected = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _isChainsTabSelected = false;
        public bool IsChainsTabSelected
        {
            get => _isChainsTabSelected;
            set
            {
                if (_isChainsTabSelected != value)
                {
                    _isChainsTabSelected = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _isAnalyticsTabSelected = false;
        public bool IsAnalyticsTabSelected
        {
            get => _isAnalyticsTabSelected;
            set
            {
                if (_isAnalyticsTabSelected != value)
                {
                    _isAnalyticsTabSelected = value;
                    OnPropertyChanged();
                }
            }
        }

        // Tab switching commands
        public ICommand SelectAllActionsTabCommand { get; }
        public ICommand SelectChainsTabCommand { get; }
        public ICommand SelectAnalyticsTabCommand { get; }

        // Action assistant properties
        public string ActionAssistantStatus { get; set; } = "AI Assistant Active";
        public string ActionAssistantDetail { get; set; } = "Monitoring for action execution opportunities";

        // Auto execution
        private bool _isAutoExecuteEnabled;
        public bool IsAutoExecuteEnabled
        {
            get => _isAutoExecuteEnabled;
            set
            {
                if (_isAutoExecuteEnabled != value)
                {
                    _isAutoExecuteEnabled = value;
                    OnPropertyChanged();
                }
            }
        }

        // Action insights
        private bool _hasActionInsights;
        public bool HasActionInsights
        {
            get => _hasActionInsights;
            set
            {
                if (_hasActionInsights != value)
                {
                    _hasActionInsights = value;
                    OnPropertyChanged();
                }
            }
        }

        public string CurrentActionInsight { get; set; } = "Based on your pattern, you could automate the process of opening applications and arranging them.";
        public double InsightConfidence { get; set; } = 0.89;
        public string InsightSourceName { get; set; } = "Screen Activity Analysis";

        // Action statistics
        public int TotalRecordedActions { get; set; } = 156;
        public int TotalAutomatedActions { get; set; } = 78;
        public double ActionSuccessRate { get; set; } = 0.92;

        // Collections for dropdowns
        public ObservableCollection<string> ActionTypeFilters { get; } = new ObservableCollection<string>
        {
            "All Types",
            "Mouse Actions",
            "Keyboard Actions",
            "Application Launch",
            "Data Entry",
            "Window Management",
            "File Operations"
        };

        public ObservableCollection<string> SortOptions { get; } = new ObservableCollection<string>
        {
            "Date (Newest)",
            "Date (Oldest)",
            "Most Used",
            "Success Rate",
            "Execution Time"
        };

        public ObservableCollection<string> GroupOptions { get; } = new ObservableCollection<string>
        {
            "None",
            "Action Type",
            "Application",
            "Time of Day",
            "Goal"
        };

        // Command instances
        public ICommand ToggleAutoExecuteCommand { get; }
        public ICommand ApplyInsightCommand { get; }
        public ICommand DismissInsightCommand { get; }
        public ICommand NavigateToObservePageCommand { get; }
        public ICommand ImportActionCommand { get; }
        public ICommand CreateActionChainCommand { get; }
        public ICommand ApplySmartFilterCommand { get; }
        public ICommand RefreshDataCommand { get; }
        public ICommand SearchCommand { get; }
        public ICommand ExecuteActionCommand { get; }
        public ICommand DeleteActionGroupCommand { get; }
        public ICommand ViewActionDetailsCommand { get; }
        public ICommand ShareSelectedActionsCommand { get; }
        public ICommand CopySelectedActionsCommand { get; }
        public ICommand AddToChainCommand { get; }
        public ICommand DeleteSelectedActionsCommand { get; }
        public ICommand CreateNewChainCommand { get; }
        public ICommand RunChainCommand { get; }
        public ICommand EditChainCommand { get; }
        public ICommand ShareChainCommand { get; }
        public ICommand DeleteChainCommand { get; }
        public ICommand QuickRecordCommand { get; }
        public ICommand ApplyRecommendationCommand { get; }
        public ICommand ToggleSimulateActionGroupCommand { get; }

        // Properties for creating a new action
        private string _newActionName;
        public string NewActionName
        {
            get => _newActionName;
            set
            {
                if (_newActionName != value)
                {
                    _newActionName = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _newActionDescription;
        public string NewActionDescription
        {
            get => _newActionDescription;
            set
            {
                if (_newActionDescription != value)
                {
                    _newActionDescription = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _newActionArray;
        public string NewActionArray
        {
            get => _newActionArray;
            set
            {
                if (_newActionArray != value)
                {
                    _newActionArray = value;
                    OnPropertyChanged();
                }
            }
        }

        private ObservableCollection<object> _newActionMedia = new ObservableCollection<object>();
        public ObservableCollection<object> NewActionMedia
        {
            get => _newActionMedia;
            set
            {
                _newActionMedia = value;
                OnPropertyChanged();
            }
        }

        // Commands for action creation popup
        public ICommand CreateActionCommand { get; }
        public ICommand CancelCreateActionCommand { get; }
        public ICommand AddScreenshotCommand { get; }
        public ICommand AddFileCommand { get; }
        public ICommand RemoveMediaCommand { get; }

        // Constructor
        public ActionPageViewModel()
        {
            // Initialize command implementations
            SelectAllActionsTabCommand = new Command(() => SelectTab(1));
            SelectChainsTabCommand = new Command(() => SelectTab(2));
            SelectAnalyticsTabCommand = new Command(() => SelectTab(3));

            ToggleAutoExecuteCommand = new Command(ToggleAutoExecute);
            ApplyInsightCommand = new Command(ApplyInsight);
            DismissInsightCommand = new Command(DismissInsight);
            NavigateToObservePageCommand = new Command(NavigateToObservePage);
            ImportActionCommand = new Command(ImportAction);
            CreateActionChainCommand = new Command(CreateActionChain);
            ApplySmartFilterCommand = new Command(ApplySmartFilter);
            RefreshDataCommand = new Command(async () => await RefreshData());
            SearchCommand = new Command(Search);
            ExecuteActionCommand = new Command<object>(ExecuteAction);
            DeleteActionGroupCommand = new Command<object>(DeleteActionGroup);
            ViewActionDetailsCommand = new Command<object>(ViewActionDetails);
            ShareSelectedActionsCommand = new Command(ShareSelectedActions);
            CopySelectedActionsCommand = new Command(CopySelectedActions);
            AddToChainCommand = new Command(AddToChain);
            DeleteSelectedActionsCommand = new Command(DeleteSelectedActions);
            CreateNewChainCommand = new Command(CreateNewChain);
            RunChainCommand = new Command<object>(RunChain);
            EditChainCommand = new Command<object>(EditChain);
            ShareChainCommand = new Command<object>(ShareChain);
            DeleteChainCommand = new Command<object>(DeleteChain);
            QuickRecordCommand = new Command(QuickRecord);
            ApplyRecommendationCommand = new Command<object>(ApplyRecommendation);
            ToggleSimulateActionGroupCommand = new Command<object>(ToggleSimulateActionGroup);

            // Initialize action creation commands
            CreateActionCommand = new Command(CreateAction);
            CancelCreateActionCommand = new Command(CancelCreateAction);
            AddScreenshotCommand = new Command(AddScreenshot);
            AddFileCommand = new Command(AddFile);
            RemoveMediaCommand = new Command<object>(RemoveMedia);

            // Initialize data
            InitializeData();
        }

        private void SelectTab(int tabIndex)
        {
            IsAllActionsTabSelected = (tabIndex == 1);
            IsChainsTabSelected = (tabIndex == 2);
            IsAnalyticsTabSelected = (tabIndex == 3);
        }

        // Sample data initialization
        private void InitializeData()
        {
            HasActionInsights = true;
            IsAutoExecuteEnabled = false;
        }

        // Command implementations
        private void ToggleAutoExecute() => IsAutoExecuteEnabled = !IsAutoExecuteEnabled;

        private void ApplyInsight()
        {
            // Implementation for applying AI insight
            HasActionInsights = false;
        }

        private void DismissInsight()
        {
            // Implementation for dismissing AI insight
            HasActionInsights = false;
        }

        private void NavigateToObservePage()
        {
            // Implementation for navigating to observe page
        }

        // More command implementations...

        private void ImportAction()
        {
            // Implementation for importing action
        }

        private void CreateActionChain()
        {
            // Implementation for creating action chain
        }

        private void ApplySmartFilter()
        {
            // Implementation for applying smart filter
        }

        private async Task RefreshData()
        {
            IsRefreshing = true;

            try
            {
                // If this is linked to a page instance, call its refresh method
                if (App.Current.MainPage is Shell shell)
                {
                    var actionPage = shell.CurrentPage as ActionPage;
                    if (actionPage != null)
                    {
                        // Use reflection to call LoadDataItemsFromBackend method
                        var methodInfo = actionPage.GetType().GetMethod(
                            "LoadDataItemsFromBackend",
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                        if (methodInfo != null)
                        {
                            await (Task)methodInfo.Invoke(actionPage, null);
                            System.Diagnostics.Debug.WriteLine("Refreshed data from backend");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error refreshing data: {ex.Message}");
            }
            finally
            {
                IsRefreshing = false;
            }
        }

        private void Search()
        {
            // Implementation for searching
        }

        private void ExecuteAction(object actionItem)
        {
            // Implementation for executing action
        }

        private void DeleteActionGroup(object actionGroup)
        {
            // Implementation for deleting action group
        }

        private void ViewActionDetails(object action)
        {
            // Implementation for viewing action details
        }

        private void ShareSelectedActions()
        {
            // Implementation for sharing selected actions
        }

        private void CopySelectedActions()
        {
            // Implementation for copying selected actions
        }

        private void AddToChain()
        {
            // Implementation for adding to chain
        }

        private void DeleteSelectedActions()
        {
            // Implementation for deleting selected actions
        }

        private void CreateNewChain()
        {
            // Implementation for creating new chain
        }

        private void RunChain(object chain)
        {
            // Implementation for running chain
        }

        private void EditChain(object chain)
        {
            // Implementation for editing chain
        }

        private void ShareChain(object chain)
        {
            // Implementation for sharing chain
        }

        private void DeleteChain(object chain)
        {
            // Implementation for deleting chain
        }

        private void QuickRecord()
        {
            // Implementation for quick recording
        }

        private void ApplyRecommendation(object recommendation)
        {
            // Implementation for applying recommendation
        }

        private void ToggleSimulateActionGroup(object actionGroup)
        {
            // Implementation for toggling simulate action group
        }

        // Methods for action creation
        private void CreateAction()
        {
            // Implementation for creating a new action
            // TODO: Add real implementation
            ClearActionFields();
        }

        private void CancelCreateAction()
        {
            // Implementation for canceling action creation
            ClearActionFields();
        }

        private void AddScreenshot()
        {
            // Implementation for adding a screenshot
            // TODO: Add real implementation
        }

        private void AddFile()
        {
            // Implementation for adding a file
            // TODO: Add real implementation
        }

        private void RemoveMedia(object media)
        {
            if (media != null && NewActionMedia.Contains(media))
            {
                NewActionMedia.Remove(media);
            }
        }

        private void ClearActionFields()
        {
            NewActionName = string.Empty;
            NewActionDescription = string.Empty;
            NewActionArray = string.Empty;
            NewActionMedia.Clear();
        }

        // INotifyPropertyChanged implementation
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // Add IsRefreshing property
        private bool _isRefreshing;
        public bool IsRefreshing
        {
            get => _isRefreshing;
            set
            {
                if (_isRefreshing != value)
                {
                    _isRefreshing = value;
                    OnPropertyChanged();
                }
            }
        }
    }
}
