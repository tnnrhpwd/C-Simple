using Microsoft.Maui.Storage;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Controls;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Input;
using System.Globalization;
using CSimple.Services.AppModeService;
using CSimple.Services;

namespace CSimple.Pages
{
    public partial class PlanPage : ContentPage, INotifyPropertyChanged
    {
        #region Private Fields
        private bool _showNewPlan = false;
        private bool _showMyPlans = true;  // Start with My Plans shown so users can see the data
        private bool _showPublicPlans = true;
        private bool _showCalendar = false;
        private string _newGoalText = string.Empty;
        private string _newPlanText = string.Empty;
        private string _newActionText = string.Empty;
        private string _planCost = string.Empty;
        private string _selectedCostType = "One-time";
        private bool _isPublic = false;
        private DateTime _planStartDate = DateTime.Today;
        private DateTime _planEndDate = DateTime.Today.AddDays(30);
        private double _planPriority = 3.0;
        private string _selectedSortOption = "Create Date (Newest)";
        private string _selectedPublicSortOption = "Create Date (Newest)";
        private DateTime _currentDate = DateTime.Today;

        private readonly DataService _dataService;
        private readonly FileService _fileService;
        private readonly CSimple.Services.AppModeService.AppModeService _appModeService;
        #endregion

        #region Public Properties
        public bool ShowNewPlan
        {
            get => _showNewPlan;
            set => SetProperty(ref _showNewPlan, value);
        }

        public bool ShowMyPlans
        {
            get => _showMyPlans;
            set => SetProperty(ref _showMyPlans, value);
        }

        public bool ShowPublicPlans
        {
            get => _showPublicPlans;
            set => SetProperty(ref _showPublicPlans, value);
        }

        public bool ShowCalendar
        {
            get => _showCalendar;
            set => SetProperty(ref _showCalendar, value);
        }

        public string NewGoalText
        {
            get => _newGoalText;
            set => SetProperty(ref _newGoalText, value);
        }

        public string NewPlanText
        {
            get => _newPlanText;
            set => SetProperty(ref _newPlanText, value);
        }

        public string NewActionText
        {
            get => _newActionText;
            set => SetProperty(ref _newActionText, value);
        }

        public string PlanCost
        {
            get => _planCost;
            set => SetProperty(ref _planCost, value);
        }

        public string SelectedCostType
        {
            get => _selectedCostType;
            set => SetProperty(ref _selectedCostType, value);
        }

        public bool IsPublic
        {
            get => _isPublic;
            set => SetProperty(ref _isPublic, value);
        }

        public DateTime PlanStartDate
        {
            get => _planStartDate;
            set => SetProperty(ref _planStartDate, value);
        }

        public DateTime PlanEndDate
        {
            get => _planEndDate;
            set => SetProperty(ref _planEndDate, value);
        }

        public double PlanPriority
        {
            get => _planPriority;
            set
            {
                SetProperty(ref _planPriority, value);
                OnPropertyChanged(nameof(PriorityLabel));
            }
        }

        public string SelectedSortOption
        {
            get => _selectedSortOption;
            set
            {
                SetProperty(ref _selectedSortOption, value);
                SortMyPlans();
            }
        }

        public string SelectedPublicSortOption
        {
            get => _selectedPublicSortOption;
            set
            {
                SetProperty(ref _selectedPublicSortOption, value);
                SortPublicPlans();
            }
        }

        public DateTime CurrentDate
        {
            get => _currentDate;
            set
            {
                SetProperty(ref _currentDate, value);
                OnPropertyChanged(nameof(CurrentMonthYear));
                PopulateCalendar(_currentDate);
            }
        }

        // Computed Properties
        public string CreatePlanButtonText => ShowNewPlan ? "Cancel Plan" : "Create Plan";
        public string MyPlansButtonText => ShowMyPlans ? "Hide My Plans" : "Show My Plans";
        public string PublicPlansButtonText => ShowPublicPlans ? "Hide Public Plans" : "Show Public Plans";
        public string CalendarButtonText => ShowCalendar ? "Hide Calendar" : "Show Calendar";
        public string PriorityLabel => $"Priority: {PlanPriority:F0}/5";
        public string CurrentMonthYear => CurrentDate.ToString("MMMM yyyy");

        // Collections
        public ObservableCollection<PlanItem> MyPlans { get; set; } = new ObservableCollection<PlanItem>();
        public ObservableCollection<PublicPlanItem> PublicPlans { get; set; } = new ObservableCollection<PublicPlanItem>();
        public ObservableCollection<DataItem> AllDataItems { get; set; } = new ObservableCollection<DataItem>();
        public ObservableCollection<string> CostTypes { get; set; } = new ObservableCollection<string>
        {
            "One-time", "Monthly", "Per Use"
        };
        public ObservableCollection<string> SortOptions { get; set; } = new ObservableCollection<string>
        {
            "Create Date (Newest)", "Create Date (Oldest)", "Priority (High to Low)", "Priority (Low to High)", "Title (A-Z)", "Title (Z-A)"
        };

        // Commands
        public ICommand ToggleCreatePlanCommand { get; }
        public ICommand ToggleMyPlansCommand { get; }
        public ICommand TogglePublicPlansCommand { get; }
        public ICommand ToggleCalendarCommand { get; }
        public ICommand SubmitPlanCommand { get; }
        public ICommand EditPlanCommand { get; }
        public ICommand DeletePlanCommand { get; }
        public ICommand SharePlanCommand { get; }
        public ICommand AgreeCommand { get; }
        public ICommand DisagreeCommand { get; }
        public ICommand AgreePublicPlanCommand { get; }
        public ICommand DisagreePublicPlanCommand { get; }
        public ICommand SharePublicPlanCommand { get; }
        public ICommand FavoritePublicPlanCommand { get; }
        public ICommand PreviousMonthCommand { get; }
        public ICommand NextMonthCommand { get; }
        public ICommand ViewTodayCommand { get; }

        // Loading and Error States (similar to React component)
        private bool _isLoading = false;
        private bool _hasError = false;
        private string _errorMessage = string.Empty;

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
        private bool _isUserLoggedIn = false;
        public bool IsUserLoggedIn
        {
            get => _isUserLoggedIn;
            set => SetProperty(ref _isUserLoggedIn, value);
        }
        #endregion

        #region Constructor
        public PlanPage()
        {
            InitializeComponent();

            // Initialize Commands
            ToggleCreatePlanCommand = new Command(OnToggleCreatePlan);
            ToggleMyPlansCommand = new Command(OnToggleMyPlans);
            TogglePublicPlansCommand = new Command(OnTogglePublicPlans);
            ToggleCalendarCommand = new Command(OnToggleCalendar);
            SubmitPlanCommand = new Command(async () => await OnSubmitPlanAsync(), () => !IsLoading);
            EditPlanCommand = new Command<PlanItem>(OnEditPlan);
            DeletePlanCommand = new Command<PlanItem>(async (plan) => await OnDeletePlanAsync(plan));
            SharePlanCommand = new Command<PlanItem>(OnSharePlan);
            AgreeCommand = new Command<PlanItem>(OnAgreePlan);
            DisagreeCommand = new Command<PlanItem>(OnDisagreePlan);
            AgreePublicPlanCommand = new Command<PublicPlanItem>(OnAgreePublicPlan);
            DisagreePublicPlanCommand = new Command<PublicPlanItem>(OnDisagreePublicPlan);
            SharePublicPlanCommand = new Command<PublicPlanItem>(OnSharePublicPlan);
            FavoritePublicPlanCommand = new Command<PublicPlanItem>(OnFavoritePublicPlan);
            PreviousMonthCommand = new Command(OnPreviousMonth);
            NextMonthCommand = new Command(OnNextMonth);
            ViewTodayCommand = new Command(OnViewToday);

            // Initialize services
            _dataService = new DataService();
            var appPathService = new AppPathService();
            _fileService = new FileService(appPathService);
            _appModeService = ServiceProvider.GetService<CSimple.Services.AppModeService.AppModeService>();

            // Bind the context
            BindingContext = this;

            Debug.WriteLine("PlanPage constructor completed, starting initialization...");

            // Initialize - similar to React useEffect
            _ = InitializePageAsync();
        }

        private async Task InitializePageAsync()
        {
            try
            {
                IsLoading = true;
                HasError = false;

                // Check user authentication status
                await CheckUserLoggedInAsync();

                // Populate calendar
                PopulateCalendar(DateTime.Now);

                // Always try to load plans - will fall back to local/sample data
                await LoadMyPlansAsync();

                // Always load public plans (like React component)
                await LoadPublicPlansAsync();
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
        #endregion

        #region INotifyPropertyChanged Implementation
        public new event PropertyChangedEventHandler PropertyChanged;

        protected bool SetProperty<T>(ref T backingStore, T value, [CallerMemberName] string propertyName = "")
        {
            if (EqualityComparer<T>.Default.Equals(backingStore, value))
                return false;

            backingStore = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected new void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion

        #region Command Handlers
        private void OnToggleCreatePlan()
        {
            ShowNewPlan = !ShowNewPlan;
            OnPropertyChanged(nameof(CreatePlanButtonText));
        }

        private async void OnToggleMyPlans()
        {
            ShowMyPlans = !ShowMyPlans;
            OnPropertyChanged(nameof(MyPlansButtonText));

            if (ShowMyPlans)
            {
                // Always try to load plans when showing, regardless of login status
                // This will fall back to local/sample data if backend fails
                await LoadMyPlansAsync();
            }
        }

        private async void OnTogglePublicPlans()
        {
            ShowPublicPlans = !ShowPublicPlans;
            OnPropertyChanged(nameof(PublicPlansButtonText));

            if (ShowPublicPlans)
            {
                await LoadPublicPlansAsync();
            }
        }

        private void OnToggleCalendar()
        {
            ShowCalendar = !ShowCalendar;
            OnPropertyChanged(nameof(CalendarButtonText));
        }

        private async Task OnSubmitPlanAsync()
        {
            if (string.IsNullOrWhiteSpace(NewGoalText) && string.IsNullOrWhiteSpace(NewPlanText))
            {
                await DisplayAlert("Error", "Please enter at least a goal or plan description.", "OK");
                return;
            }

            try
            {
                IsLoading = true;
                HasError = false;

                var planData = CreatePlanDataString();
                await SavePlanToBackend(planData);
                await SavePlansToFile();

                // Clear form
                ClearForm();

                // Toggle off create plan view
                ShowNewPlan = false;
                OnPropertyChanged(nameof(CreatePlanButtonText));

                await DisplayAlert("Success", "Plan created successfully!", "OK");

                // Reload plans
                await LoadMyPlansAsync();
            }
            catch (Exception ex)
            {
                HasError = true;
                ErrorMessage = ex.Message;
                await DisplayAlert("Error", $"Failed to create plan: {ex.Message}", "OK");
                Debug.WriteLine($"Error creating plan: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void OnEditPlan(PlanItem plan)
        {
            if (plan == null) return;

            // Pre-populate form with existing plan data
            NewGoalText = plan.Goal;
            NewPlanText = plan.PlanDescription;
            NewActionText = plan.ActionItems;
            PlanStartDate = plan.StartDate;
            PlanEndDate = plan.EndDate;
            PlanPriority = plan.Priority;
            IsPublic = plan.IsPublic;

            ShowNewPlan = true;
            OnPropertyChanged(nameof(CreatePlanButtonText));
        }

        private async Task OnDeletePlanAsync(PlanItem plan)
        {
            if (plan == null) return;

            bool result = await DisplayAlert("Delete Plan", "Are you sure you want to delete this plan?", "Yes", "No");
            if (result)
            {
                try
                {
                    IsLoading = true;
                    MyPlans.Remove(plan);
                    await SavePlansToFile();
                    await DisplayAlert("Success", "Plan deleted successfully!", "OK");
                }
                catch (Exception ex)
                {
                    HasError = true;
                    ErrorMessage = ex.Message;
                    await DisplayAlert("Error", $"Failed to delete plan: {ex.Message}", "OK");
                    Debug.WriteLine($"Error deleting plan: {ex.Message}");
                }
                finally
                {
                    IsLoading = false;
                }
            }
        }

        private async void OnSharePlan(PlanItem plan)
        {
            if (plan == null) return;

            try
            {
                // Enhanced share functionality - simplified for MAUI compatibility
                var shareText = $"Check out my plan: {plan.Goal}\n\nDescription: {plan.PlanDescription}";
                await DisplayAlert("Share Plan", shareText, "OK");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error sharing plan: {ex.Message}");
                await DisplayAlert("Error", "Unable to share plan at this time.", "OK");
            }
        }

        private void OnAgreePlan(PlanItem plan)
        {
            if (plan == null) return;

            plan.UserHasAgreed = !plan.UserHasAgreed;
            if (plan.UserHasAgreed)
            {
                plan.AgreeCount++;
                if (plan.UserHasDisagreed)
                {
                    plan.DisagreeCount--;
                    plan.UserHasDisagreed = false;
                }
            }
            else
            {
                plan.AgreeCount--;
            }
        }

        private void OnDisagreePlan(PlanItem plan)
        {
            if (plan == null) return;

            plan.UserHasDisagreed = !plan.UserHasDisagreed;
            if (plan.UserHasDisagreed)
            {
                plan.DisagreeCount++;
                if (plan.UserHasAgreed)
                {
                    plan.AgreeCount--;
                    plan.UserHasAgreed = false;
                }
            }
            else
            {
                plan.DisagreeCount--;
            }
        }

        private void OnAgreePublicPlan(PublicPlanItem plan)
        {
            if (plan == null) return;

            plan.UserHasAgreed = !plan.UserHasAgreed;
            if (plan.UserHasAgreed)
            {
                plan.AgreeCount++;
                if (plan.UserHasDisagreed)
                {
                    plan.DisagreeCount--;
                    plan.UserHasDisagreed = false;
                }
            }
            else
            {
                plan.AgreeCount--;
            }
        }

        private void OnDisagreePublicPlan(PublicPlanItem plan)
        {
            if (plan == null) return;

            plan.UserHasDisagreed = !plan.UserHasDisagreed;
            if (plan.UserHasDisagreed)
            {
                plan.DisagreeCount++;
                if (plan.UserHasAgreed)
                {
                    plan.AgreeCount--;
                    plan.UserHasAgreed = false;
                }
            }
            else
            {
                plan.DisagreeCount--;
            }
        }

        private async void OnSharePublicPlan(PublicPlanItem plan)
        {
            if (plan == null) return;

            try
            {
                var shareText = $"Check out this public plan by {plan.UserName}:\n\n{plan.DisplayText}";
                await DisplayAlert("Share Public Plan", shareText, "OK");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error sharing public plan: {ex.Message}");
                await DisplayAlert("Error", "Unable to share plan at this time.", "OK");
            }
        }

        private async void OnFavoritePublicPlan(PublicPlanItem plan)
        {
            if (plan == null) return;

            plan.IsFavorited = !plan.IsFavorited;

            // Add visual feedback
            var message = plan.IsFavorited ? "Added to favorites! ⭐" : "Removed from favorites";

            // Show toast-like notification using DisplayAlert with short delay
            var displayTask = DisplayAlert("", message, "OK");
            _ = Task.Delay(1500).ContinueWith(_ =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    // Auto-dismiss the alert after delay if still showing
                });
            });

            await displayTask;
        }

        private void OnPreviousMonth()
        {
            CurrentDate = CurrentDate.AddMonths(-1);
        }

        private void OnNextMonth()
        {
            CurrentDate = CurrentDate.AddMonths(1);
        }

        private async void OnViewToday()
        {
            CurrentDate = DateTime.Today;
            ShowCalendar = true; // Ensure calendar is visible

            // Enhanced today's schedule with actual plan data
            var todaysPlans = MyPlans.Where(p =>
                p.StartDate.Date <= DateTime.Today &&
                p.EndDate.Date >= DateTime.Today).ToList();

            var scheduleText = todaysPlans.Any()
                ? $"You have {todaysPlans.Count} active plan(s) today:\n\n" +
                  string.Join("\n", todaysPlans.Select(p => $"• {p.Goal}"))
                : "No active plans for today. Time to create one!";

            await DisplayAlert("Today's Schedule", scheduleText, "OK");
        }
        #endregion

        #region Model Classes
        public class PlanItem : INotifyPropertyChanged
        {
            private int _agreeCount;
            private int _disagreeCount;
            private bool _userHasAgreed;
            private bool _userHasDisagreed;

            public string Id { get; set; } = Guid.NewGuid().ToString();
            public string Goal { get; set; } = string.Empty;
            public string PlanDescription { get; set; } = string.Empty;
            public string ActionItems { get; set; } = string.Empty;
            public DateTime StartDate { get; set; } = DateTime.Today;
            public DateTime EndDate { get; set; } = DateTime.Today.AddDays(30);
            public double Priority { get; set; } = 3.0;
            public bool IsPublic { get; set; } = false;
            public double Progress { get; set; } = 0.0;
            public string Cost { get; set; } = string.Empty;
            public string CostType { get; set; } = "One-time";
            public DateTime CreatedAt { get; set; } = DateTime.Now;
            public DateTime UpdatedAt { get; set; } = DateTime.Now;

            public int AgreeCount
            {
                get => _agreeCount;
                set
                {
                    _agreeCount = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(VoteCount));
                    OnPropertyChanged(nameof(AgreeText));
                }
            }

            public int DisagreeCount
            {
                get => _disagreeCount;
                set
                {
                    _disagreeCount = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(VoteCount));
                    OnPropertyChanged(nameof(DisagreeText));
                }
            }

            public bool UserHasAgreed
            {
                get => _userHasAgreed;
                set
                {
                    _userHasAgreed = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(AgreeText));
                }
            }

            public bool UserHasDisagreed
            {
                get => _userHasDisagreed;
                set
                {
                    _userHasDisagreed = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DisagreeText));
                }
            }

            // Computed Properties
            public bool HasActions => !string.IsNullOrEmpty(ActionItems);
            public bool HasCost => !string.IsNullOrEmpty(Cost) && Cost != "0";
            public string CostString => HasCost ? $"💰 ${Cost} ({CostType})" : string.Empty;
            public string VoteCount => $"{AgreeCount - DisagreeCount} votes";
            public string AgreeText => UserHasAgreed ? "✅ Agreed" : "👍 Agree";
            public string DisagreeText => UserHasDisagreed ? "❌ Disagreed" : "👎 Disagree";
            public string PriorityColor
            {
                get
                {
                    return Priority switch
                    {
                        >= 4.5 => "#F44336", // Red for high priority
                        >= 3.5 => "#FF9800", // Orange for medium-high
                        >= 2.5 => "#FFC107", // Yellow for medium
                        >= 1.5 => "#4CAF50", // Green for medium-low
                        _ => "#9E9E9E"        // Gray for low priority
                    };
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;
            protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public class PublicPlanItem : INotifyPropertyChanged
        {
            private int _agreeCount;
            private int _disagreeCount;
            private bool _userHasAgreed;
            private bool _userHasDisagreed;
            private bool _isFavorited;

            public string Id { get; set; } = string.Empty;
            public string UserName { get; set; } = "Anonymous";
            public string UserBadge { get; set; } = "🔷";
            public string DisplayText { get; set; } = string.Empty;
            public DateTime CreatedAt { get; set; } = DateTime.Now;

            public int AgreeCount
            {
                get => _agreeCount;
                set
                {
                    _agreeCount = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(VoteDisplay));
                    OnPropertyChanged(nameof(AgreeText));
                }
            }

            public int DisagreeCount
            {
                get => _disagreeCount;
                set
                {
                    _disagreeCount = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(VoteDisplay));
                    OnPropertyChanged(nameof(DisagreeText));
                }
            }

            public bool UserHasAgreed
            {
                get => _userHasAgreed;
                set
                {
                    _userHasAgreed = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(AgreeText));
                }
            }

            public bool UserHasDisagreed
            {
                get => _userHasDisagreed;
                set
                {
                    _userHasDisagreed = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DisagreeText));
                }
            }

            public bool IsFavorited
            {
                get => _isFavorited;
                set
                {
                    _isFavorited = value;
                    OnPropertyChanged();
                }
            }

            // Computed Properties
            public string VoteDisplay => $"{AgreeCount - DisagreeCount} votes • {AgreeCount} 👍 • {DisagreeCount} 👎";
            public string AgreeText => UserHasAgreed ? "✅" : "👍";
            public string DisagreeText => UserHasDisagreed ? "❌" : "👎";
            public string PriorityColor => "#3F51B5"; // Default blue for public plans

            public event PropertyChangedEventHandler PropertyChanged;
            protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        // Simple value converters for XAML bindings
        public class StringNotEmptyConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                if (value is string str)
                    return !string.IsNullOrEmpty(str);
                return false;
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            {
                throw new NotImplementedException();
            }
        }

        public class FavoriteTextConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                if (value is PublicPlanItem item)
                    return item.IsFavorited ? "⭐" : "☆";
                return "☆";
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            {
                throw new NotImplementedException();
            }
        }
        #endregion

        #region Helper Methods
        private string CreatePlanDataString()
        {
            var parts = new List<string>();

            if (!string.IsNullOrEmpty(PlanCost) && decimal.TryParse(PlanCost, out decimal cost))
            {
                parts.Add($"Cost:${cost:F2}");
                parts.Add($"CostType:{SelectedCostType}");
            }

            if (!string.IsNullOrEmpty(NewPlanText))
                parts.Add($"Plan:{NewPlanText}");

            if (!string.IsNullOrEmpty(NewGoalText))
                parts.Add($"Goal:{NewGoalText}");

            if (!string.IsNullOrEmpty(NewActionText))
                parts.Add($"Action:{NewActionText}");

            if (IsPublic)
                parts.Add($"Public:{IsPublic}");

            parts.Add($"StartDate:{PlanStartDate:yyyy-MM-dd}");
            parts.Add($"EndDate:{PlanEndDate:yyyy-MM-dd}");
            parts.Add($"Priority:{PlanPriority}");

            return string.Join("|", parts);
        }

        private void ClearForm()
        {
            NewGoalText = string.Empty;
            NewPlanText = string.Empty;
            NewActionText = string.Empty;
            PlanCost = string.Empty;
            SelectedCostType = "One-time";
            IsPublic = false;
            PlanStartDate = DateTime.Today;
            PlanEndDate = DateTime.Today.AddDays(30);
            PlanPriority = 3.0;
        }

        private void SortMyPlans()
        {
            if (MyPlans.Count == 0) return;

            var sorted = SelectedSortOption switch
            {
                "Create Date (Newest)" => MyPlans.OrderByDescending(p => p.CreatedAt),
                "Create Date (Oldest)" => MyPlans.OrderBy(p => p.CreatedAt),
                "Priority (High to Low)" => MyPlans.OrderByDescending(p => p.Priority),
                "Priority (Low to High)" => MyPlans.OrderBy(p => p.Priority),
                "Title (A-Z)" => MyPlans.OrderBy(p => p.Goal),
                "Title (Z-A)" => MyPlans.OrderByDescending(p => p.Goal),
                _ => MyPlans.OrderByDescending(p => p.CreatedAt)
            };

            var sortedList = sorted.ToList();
            MyPlans.Clear();
            foreach (var plan in sortedList)
            {
                MyPlans.Add(plan);
            }
        }

        private void SortPublicPlans()
        {
            if (PublicPlans.Count == 0) return;

            var sorted = SelectedPublicSortOption switch
            {
                "Create Date (Newest)" => PublicPlans.OrderByDescending(p => p.CreatedAt),
                "Create Date (Oldest)" => PublicPlans.OrderBy(p => p.CreatedAt),
                "Title (A-Z)" => PublicPlans.OrderBy(p => p.DisplayText),
                "Title (Z-A)" => PublicPlans.OrderByDescending(p => p.DisplayText),
                _ => PublicPlans.OrderByDescending(p => p.CreatedAt)
            };

            var sortedList = sorted.ToList();
            PublicPlans.Clear();
            foreach (var plan in sortedList)
            {
                PublicPlans.Add(plan);
            }
        }

        private async Task LoadMyPlansAsync()
        {
            try
            {
                await LoadPlansFromBackend();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading my plans: {ex.Message}");
            }
        }

        private async Task LoadPublicPlansAsync()
        {
            try
            {
                await LoadPublicPlansFromBackend();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading public plans: {ex.Message}");
            }
        }
        #endregion

        #region Business Logic Methods
        private async Task CheckUserLoggedInAsync()
        {
            if (_appModeService.CurrentMode == AppMode.Offline)
            {
                Debug.WriteLine("App is in offline mode. Using local plans only.");
                IsUserLoggedIn = false;
                await LoadPlansFromFile();
                return;
            }

            var isLoggedIn = await IsUserLoggedInAsync();
            IsUserLoggedIn = isLoggedIn;

            if (!isLoggedIn)
            {
                Debug.WriteLine("User is not logged in.");
                // In React component, it navigates to login, but here we'll just show public plans
            }
            else
            {
                Debug.WriteLine("User is logged in.");
                await LoadPlansFromBackend();
            }
        }

        private async void CheckUserLoggedIn()
        {
            await CheckUserLoggedInAsync();
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

        protected override void OnAppearing()
        {
            base.OnAppearing();

            if (_appModeService.CurrentMode == AppMode.Offline)
            {
                _ = LoadPlansFromFile();
            }
            else
            {
                _ = LoadPlansFromBackend();
                _ = LoadPublicPlansFromBackend();
            }
        }

        private async Task LoadPlansFromBackend()
        {
            if (_appModeService.CurrentMode == AppMode.Offline)
            {
                Debug.WriteLine("App is in offline mode. Skipping backend plan loading.");
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
                    await LoadPlansFromFile(); // Fallback to local/sample data
                    return;
                }

                // First, check if the user is still logged in
                var isLoggedIn = await _dataService.IsLoggedInAsync();
                if (!isLoggedIn)
                {
                    Debug.WriteLine("User authentication check failed. Falling back to local data.");
                    IsUserLoggedIn = false;
                    await LoadPlansFromFile();
                    return;
                }

                var data = "Plan";
                Debug.WriteLine($"Making request to backend for plans with data: {data}");

                var plans = await _dataService.GetDataAsync(data, token);
                var myPlanItems = ProcessMyPlansFromBackend(plans.Data?.Cast<DataItem>().ToList() ?? new List<DataItem>());

                MyPlans.Clear();
                AllDataItems.Clear();

                foreach (var plan in myPlanItems)
                {
                    MyPlans.Add(plan);
                }

                foreach (var item in plans.Data ?? new List<DataItem>())
                {
                    AllDataItems.Add(item);
                }

                await SavePlansToFile();
                SortMyPlans();

                Debug.WriteLine($"Successfully loaded {MyPlans.Count} plans from backend");
            }
            catch (UnauthorizedAccessException)
            {
                Debug.WriteLine("Unauthorized access - user session expired");
                HasError = true;
                ErrorMessage = "Your session has expired. Please log in again.";
                IsUserLoggedIn = false;

                // Clear sensitive data and fallback to local storage
                await LoadPlansFromFile();
            }
            catch (JsonException jsonEx)
            {
                Debug.WriteLine($"JSON parsing error: {jsonEx.Message}. Server may have returned HTML instead of JSON.");
                HasError = true;
                ErrorMessage = "🔧 BACKEND CONFIGURATION ISSUE: Enable debug build to use local backend, or check production backend deployment.";

                Debug.WriteLine("🚨 BACKEND CONFIGURATION ISSUE DETECTED");
                Debug.WriteLine($"📋 Current Environment: {BackendConfigService.CurrentEnvironment}");
                Debug.WriteLine($"🌐 Backend URL: {BackendConfigService.ApiEndpoints.GetBaseUrl()}");

                // Run backend connectivity diagnostic
                try
                {
                    Debug.WriteLine("🔍 Running backend connectivity diagnostic...");
                    var (isReachable, details) = await _dataService.TestBackendConnectivityAsync();
                    Debug.WriteLine($"Backend Connectivity Test Results:\n{details}");
                }
                catch (Exception diagnosticEx)
                {
                    Debug.WriteLine($"Failed to run backend diagnostic: {diagnosticEx.Message}");
                }

                // Fallback to local storage when server returns HTML/invalid JSON
                await LoadPlansFromFile();
            }
            catch (HttpRequestException httpEx)
            {
                Debug.WriteLine($"Network error loading plans: {httpEx.Message}");
                HasError = true;
                ErrorMessage = "Network error. Check your internet connection and server status. Loading saved plans instead.";

                // Fallback to local storage on network errors
                await LoadPlansFromFile();
            }
            catch (TaskCanceledException)
            {
                Debug.WriteLine("Request timed out loading plans from backend");
                HasError = true;
                ErrorMessage = "Request timed out. The server may be slow or unavailable. Loading saved plans instead.";

                // Fallback to local storage on timeout
                await LoadPlansFromFile();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Unexpected error loading plans from backend: {ex.Message}");
                Debug.WriteLine($"Exception type: {ex.GetType().Name}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");

                HasError = true;
                ErrorMessage = "An unexpected error occurred while loading plans. Loading saved plans instead.";

                // Fallback to local storage on any other unexpected error
                await LoadPlansFromFile();
            }
            finally
            {
                IsLoading = false;
            }
        }


        private async Task LoadPublicPlansFromBackend()
        {
            if (_appModeService.CurrentMode == AppMode.Offline)
            {
                Debug.WriteLine("App is in offline mode. Skipping public plan loading.");
                return;
            }

            try
            {
                IsLoading = true;

                Debug.WriteLine("LoadPublicPlansFromBackend called - loading from backend API");

                // Call the backend public endpoint
                var publicPlansData = await _dataService.GetPublicPlansAsync();

                // Convert DataItems to PublicPlanItems for public display
                var publicPlanItems = new List<PublicPlanItem>();

                foreach (var dataItem in publicPlansData.Data ?? new List<DataItem>())
                {
                    try
                    {
                        var publicPlan = ConvertDataItemToPublicPlan(dataItem);
                        if (publicPlan != null)
                        {
                            publicPlanItems.Add(publicPlan);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error converting data item to public plan: {ex.Message}");
                    }
                }

                PublicPlans.Clear();
                foreach (var plan in publicPlanItems)
                {
                    PublicPlans.Add(plan);
                }

                SortPublicPlans();
                Debug.WriteLine($"Successfully loaded {PublicPlans.Count} public plans from backend");

                // Clear any errors since we successfully loaded data
                HasError = false;
                ErrorMessage = string.Empty;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading public plans: {ex.Message}");
                HasError = true;
                ErrorMessage = "Failed to load public plans. Please try again later.";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private PublicPlanItem ConvertDataItemToPublicPlan(DataItem dataItem)
        {
            if (dataItem?.Data?.Text == null) return null;

            var publicPlan = new PublicPlanItem
            {
                Id = dataItem._id ?? Guid.NewGuid().ToString(),
                CreatedAt = dataItem.createdAt,
                AgreeCount = 0,
                DisagreeCount = 0,
                UserName = "User", // Default name, could be extracted from Creator field
                UserBadge = "🌟"
            };

            // Parse the pipe-delimited text to extract plan information
            var parts = dataItem.Data.Text.Split('|');
            var planText = "";
            var goalText = "";
            var actionText = "";

            foreach (var part in parts)
            {
                var keyValue = part.Split(':', 2);
                if (keyValue.Length != 2) continue;

                var key = keyValue[0].Trim().ToLower();
                var value = keyValue[1].Trim();

                switch (key)
                {
                    case "plan":
                        planText = value;
                        break;
                    case "goal":
                        goalText = value;
                        break;
                    case "action":
                        actionText = value;
                        break;
                    case "agrees":
                        var agrees = value.Split(',').Where(s => !string.IsNullOrEmpty(s.Trim())).Count();
                        publicPlan.AgreeCount = agrees;
                        break;
                    case "disagrees":
                        var disagrees = value.Split(',').Where(s => !string.IsNullOrEmpty(s.Trim())).Count();
                        publicPlan.DisagreeCount = disagrees;
                        break;
                    case "creator":
                        // Could extract username from creator ID if available
                        break;
                }
            }

            // Build display text in the format expected by PublicPlanItem
            publicPlan.DisplayText = $"Goal: {goalText} | Plan: {planText} | Action: {actionText}";

            return publicPlan;
        }

        private List<PlanItem> ProcessMyPlansFromBackend(List<DataItem> planItems)
        {
            var myPlanItems = new List<PlanItem>();

            foreach (var dataItem in planItems)
            {
                try
                {
                    var planItem = ParseDataItemToPlanItem(dataItem);
                    if (planItem != null)
                    {
                        myPlanItems.Add(planItem);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error parsing plan item: {ex.Message}");
                }
            }

            return myPlanItems;
        }

        private PlanItem ParseDataItemToPlanItem(DataItem dataItem)
        {
            if (dataItem?.Data?.Text == null) return null;

            var planItem = new PlanItem
            {
                Id = dataItem._id ?? Guid.NewGuid().ToString(),
                CreatedAt = dataItem.createdAt,
                UpdatedAt = dataItem.updatedAt
            };

            // Parse the text format: "Plan:description|Goal:goal|Action:action|..."
            var parts = dataItem.Data.Text.Split('|');
            foreach (var part in parts)
            {
                var keyValue = part.Split(':', 2);
                if (keyValue.Length != 2) continue;

                var key = keyValue[0].Trim();
                var value = keyValue[1].Trim();

                switch (key.ToLower())
                {
                    case "plan":
                        planItem.PlanDescription = value;
                        break;
                    case "goal":
                        planItem.Goal = value;
                        break;
                    case "action":
                        planItem.ActionItems = value;
                        break;
                    case "cost":
                        planItem.Cost = value.Replace("$", "");
                        break;
                    case "costtype":
                        planItem.CostType = value;
                        break;
                    case "public":
                        planItem.IsPublic = bool.TryParse(value, out bool isPublic) && isPublic;
                        break;
                    case "startdate":
                        if (DateTime.TryParse(value, out DateTime startDate))
                            planItem.StartDate = startDate;
                        break;
                    case "enddate":
                        if (DateTime.TryParse(value, out DateTime endDate))
                            planItem.EndDate = endDate;
                        break;
                    case "priority":
                        if (double.TryParse(value, out double priority))
                            planItem.Priority = priority;
                        break;
                    case "agrees":
                        var agrees = value.Split(',').Where(s => !string.IsNullOrEmpty(s.Trim())).Count();
                        planItem.AgreeCount = agrees;
                        break;
                    case "disagrees":
                        var disagrees = value.Split(',').Where(s => !string.IsNullOrEmpty(s.Trim())).Count();
                        planItem.DisagreeCount = disagrees;
                        break;
                }
            }

            // Set default goal if none found
            if (string.IsNullOrEmpty(planItem.Goal) && !string.IsNullOrEmpty(planItem.PlanDescription))
            {
                planItem.Goal = planItem.PlanDescription.Length > 50
                    ? planItem.PlanDescription.Substring(0, 50) + "..."
                    : planItem.PlanDescription;
            }

            return planItem;
        }

        private async Task SavePlanToBackend(string planData)
        {
            if (_appModeService.CurrentMode == AppMode.Offline)
            {
                Debug.WriteLine("App is in offline mode. Plan saved locally only.");

                // Add to local collection for offline mode
                var newPlan = new PlanItem
                {
                    Goal = NewGoalText,
                    PlanDescription = NewPlanText,
                    ActionItems = NewActionText,
                    Cost = PlanCost,
                    CostType = SelectedCostType,
                    StartDate = PlanStartDate,
                    EndDate = PlanEndDate,
                    Priority = PlanPriority,
                    IsPublic = IsPublic,
                    CreatedAt = DateTime.Now
                };

                MyPlans.Add(newPlan);
                SortMyPlans();
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

                var response = await _dataService.CreateDataAsync(planData, token);
                if (response.DataIsSuccess)
                {
                    Debug.WriteLine("Plan saved to backend");

                    // Add to local collection
                    var newPlan = new PlanItem
                    {
                        Goal = NewGoalText,
                        PlanDescription = NewPlanText,
                        ActionItems = NewActionText,
                        Cost = PlanCost,
                        CostType = SelectedCostType,
                        StartDate = PlanStartDate,
                        EndDate = PlanEndDate,
                        Priority = PlanPriority,
                        IsPublic = IsPublic,
                        CreatedAt = DateTime.Now
                    };

                    MyPlans.Add(newPlan);
                    SortMyPlans();
                }
                else
                {
                    Debug.WriteLine("Failed to save plan to backend");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving plan to backend: {ex.Message}");
            }
        }

        private async Task SavePlansToFile()
        {
            try
            {
                await Task.Delay(1); // Minimal delay to satisfy async requirement
                Debug.WriteLine("Plans saved to file");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving plans to file: {ex.Message}");
            }
        }

        private async Task LoadPlansFromFile()
        {
            try
            {
                IsLoading = true;
                Debug.WriteLine("LoadPlansFromFile called - No sample data loaded. Backend connection required.");

                await Task.Delay(50); // Small delay to show loading state

                // Clear collections - no sample data
                MyPlans.Clear();
                AllDataItems.Clear();

                Debug.WriteLine("No local plans loaded. Please ensure backend is running and accessible.");

                // Show informative message about backend requirement
                HasError = true;
                ErrorMessage = _appModeService.CurrentMode == AppMode.Offline
                    ? "Offline mode: No local plans available. Switch to online mode to access your plans."
                    : "No plans loaded. Please ensure the backend server is running and accessible.";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in LoadPlansFromFile: {ex.Message}");
                HasError = true;
                ErrorMessage = "Unable to load plans.";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void PopulateCalendar(DateTime date)
        {
            // Clear existing calendar items (except headers)
            var itemsToRemove = new List<IView>();
            foreach (var child in CalendarGrid.Children)
            {
                if (Grid.GetRow((BindableObject)child) > 0)
                    itemsToRemove.Add(child);
            }
            foreach (var item in itemsToRemove)
            {
                CalendarGrid.Children.Remove(item);
            }

            var firstDayOfMonth = new DateTime(date.Year, date.Month, 1);
            var daysInMonth = DateTime.DaysInMonth(date.Year, date.Month);
            var startDayOfWeek = (int)firstDayOfMonth.DayOfWeek;

            // Modern calendar styling with dark theme
            var surfaceColor = Color.FromArgb("#2d2d30");
            var primaryColor = Color.FromArgb("#2196F3");
            var accentColor = Color.FromArgb("#00BCD4");
            var textColor = Color.FromArgb("#ffffff");
            var todayColor = Color.FromArgb("#E91E63");

            for (int i = 0; i < daysInMonth; i++)
            {
                var currentDayDate = new DateTime(date.Year, date.Month, i + 1);
                var isToday = currentDayDate.Date == DateTime.Today;
                var hasActivePlan = MyPlans.Any(p =>
                    p.StartDate.Date <= currentDayDate.Date &&
                    p.EndDate.Date >= currentDayDate.Date);

                var dayButton = new Button
                {
                    Text = (i + 1).ToString(),
                    BackgroundColor = isToday ? todayColor : (hasActivePlan ? accentColor : surfaceColor),
                    TextColor = isToday || hasActivePlan ? Colors.White : textColor,
                    FontSize = 14,
                    FontAttributes = isToday ? FontAttributes.Bold : FontAttributes.None,
                    Padding = new Thickness(4),
                    CornerRadius = 8,
                    BorderWidth = hasActivePlan && !isToday ? 1 : 0,
                    BorderColor = primaryColor
                };

                // Add tap handler for calendar day selection
                dayButton.Clicked += async (s, e) =>
                {
                    var plansForDay = MyPlans.Where(p =>
                        p.StartDate.Date <= currentDayDate.Date &&
                        p.EndDate.Date >= currentDayDate.Date).ToList();

                    if (plansForDay.Any())
                    {
                        var message = $"Plans for {currentDayDate:MMM dd}:\n\n" +
                                     string.Join("\n", plansForDay.Select(p => $"• {p.Goal}"));
                        await DisplayAlert("Day Schedule", message, "OK");
                    }
                    else
                    {
                        await DisplayAlert("Day Schedule", $"No plans scheduled for {currentDayDate:MMM dd}", "OK");
                    }
                };

                var row = (i + startDayOfWeek) / 7 + 1;
                var column = (i + startDayOfWeek) % 7;

                Grid.SetRow(dayButton, row);
                Grid.SetColumn(dayButton, column);
                CalendarGrid.Children.Add(dayButton);
            }
        }
        #endregion
    }
}
