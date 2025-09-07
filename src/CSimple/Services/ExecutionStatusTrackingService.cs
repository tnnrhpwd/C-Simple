using CSimple.Models;
using Microsoft.Maui.Controls;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Timers;
using System.Windows.Input;

namespace CSimple.Services
{
    /// <summary>
    /// Service responsible for tracking execution status, timing, and progress during model pipeline execution
    /// </summary>
    public class ExecutionStatusTrackingService : INotifyPropertyChanged, IDisposable
    {
        private Timer _executionTimer;
        private DateTime _executionStartTime;
        private DateTime _groupExecutionStartTime;
        private readonly SystemMonitoringService _systemMonitoringService;

        // --- Properties ---
        private bool _isExecutingModels;
        public bool IsExecutingModels
        {
            get => _isExecutingModels;
            set => SetProperty(ref _isExecutingModels, value);
        }

        private string _executionStatus = "Ready";
        public string ExecutionStatus
        {
            get => _executionStatus;
            set => SetProperty(ref _executionStatus, value);
        }

        private int _executionProgress;
        public int ExecutionProgress
        {
            get => _executionProgress;
            set => SetProperty(ref _executionProgress, value);
        }

        private int _totalModelsToExecute;
        public int TotalModelsToExecute
        {
            get => _totalModelsToExecute;
            set => SetProperty(ref _totalModelsToExecute, value);
        }

        private int _modelsExecutedCount;
        public int ModelsExecutedCount
        {
            get => _modelsExecutedCount;
            set
            {
                if (SetProperty(ref _modelsExecutedCount, value))
                {
                    // Update progress percentage with safer calculation
                    if (TotalModelsToExecute > 0)
                    {
                        ExecutionProgress = Math.Min(100, (int)((double)value / TotalModelsToExecute * 100));
                    }
                    else
                    {
                        ExecutionProgress = value > 0 ? 100 : 0;
                    }
                }
            }
        }

        private string _currentExecutingModel = "";
        public string CurrentExecutingModel
        {
            get => _currentExecutingModel;
            set => SetProperty(ref _currentExecutingModel, value);
        }

        private string _currentExecutingModelType = "";
        public string CurrentExecutingModelType
        {
            get => _currentExecutingModelType;
            set => SetProperty(ref _currentExecutingModelType, value);
        }

        private double _executionDurationSeconds;
        public double ExecutionDurationSeconds
        {
            get => _executionDurationSeconds;
            set => SetProperty(ref _executionDurationSeconds, value);
        }

        public string ExecutionDurationDisplay
        {
            get
            {
                if (_executionDurationSeconds <= 0)
                    return "No cycle time";

                var timeSpan = TimeSpan.FromSeconds(_executionDurationSeconds);
                if (timeSpan.TotalMinutes >= 1)
                    return $"{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
                else
                    return $"{timeSpan.TotalSeconds:F1}s";
            }
        }

        // Execution group tracking properties
        private bool _isExecutingInGroups;
        public bool IsExecutingInGroups
        {
            get => _isExecutingInGroups;
            set => SetProperty(ref _isExecutingInGroups, value);
        }

        private int _currentExecutionGroup;
        public int CurrentExecutionGroup
        {
            get => _currentExecutionGroup;
            set => SetProperty(ref _currentExecutionGroup, value);
        }

        private int _totalExecutionGroups;
        public int TotalExecutionGroups
        {
            get => _totalExecutionGroups;
            set => SetProperty(ref _totalExecutionGroups, value);
        }

        private double _groupExecutionDurationSeconds;
        public double GroupExecutionDurationSeconds
        {
            get => _groupExecutionDurationSeconds;
            set => SetProperty(ref _groupExecutionDurationSeconds, value);
        }

        public string GroupExecutionDurationDisplay
        {
            get
            {
                if (_groupExecutionDurationSeconds <= 0)
                    return "0.0s";

                var timeSpan = TimeSpan.FromSeconds(_groupExecutionDurationSeconds);
                if (timeSpan.TotalMinutes >= 1)
                    return $"{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
                else
                    return $"{timeSpan.TotalSeconds:F1}s";
            }
        }

        public ObservableCollection<ExecutionGroupInfo> ExecutionGroups { get; } = new ObservableCollection<ExecutionGroupInfo>();

        // --- System Monitoring Properties ---
        public bool IsSystemMonitoringEnabled
        {
            get => _systemMonitoringService.IsSystemMonitoringEnabled;
            set => _systemMonitoringService.IsSystemMonitoringEnabled = value;
        }

        public string SystemUsageDisplay => _systemMonitoringService.SystemUsageDisplay;

        public double CpuUsagePercent => _systemMonitoringService.CpuUsagePercent;
        public double RamUsagePercent => _systemMonitoringService.RamUsagePercent;
        public double GpuUsagePercent => _systemMonitoringService.GpuUsagePercent;

        // --- Commands ---
        public ICommand ToggleSystemMonitoringCommand { get; private set; }

        // --- Constructor ---
        public ExecutionStatusTrackingService()
        {
            _systemMonitoringService = new SystemMonitoringService();

            // Subscribe to system monitoring property changes
            _systemMonitoringService.PropertyChanged += OnSystemMonitoringPropertyChanged;

            InitializeExecutionTimer();
            InitializeCommands();
        }

        // --- Timer Methods ---
        /// <summary>
        /// Initialize commands for system monitoring
        /// </summary>
        private void InitializeCommands()
        {
            ToggleSystemMonitoringCommand = new Microsoft.Maui.Controls.Command(() => _systemMonitoringService.ToggleMonitoring());
        }

        /// <summary>
        /// Handle property changes from the system monitoring service
        /// </summary>
        private void OnSystemMonitoringPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Forward relevant property change notifications
            switch (e.PropertyName)
            {
                case nameof(SystemMonitoringService.IsSystemMonitoringEnabled):
                    OnPropertyChanged(nameof(IsSystemMonitoringEnabled));
                    break;
                case nameof(SystemMonitoringService.SystemUsageDisplay):
                    OnPropertyChanged(nameof(SystemUsageDisplay));
                    break;
                case nameof(SystemMonitoringService.CpuUsagePercent):
                    OnPropertyChanged(nameof(CpuUsagePercent));
                    break;
                case nameof(SystemMonitoringService.RamUsagePercent):
                    OnPropertyChanged(nameof(RamUsagePercent));
                    break;
                case nameof(SystemMonitoringService.GpuUsagePercent):
                    OnPropertyChanged(nameof(GpuUsagePercent));
                    break;
            }
        }

        /// <summary>
        /// Initialize the execution timer for tracking model execution duration
        /// </summary>
        private void InitializeExecutionTimer()
        {
            _executionTimer = new Timer(100); // Update every 100ms for smooth display
            _executionTimer.Elapsed += OnExecutionTimerElapsed;
            _executionTimer.AutoReset = true;
            ExecutionDurationSeconds = 0;
        }

        /// <summary>
        /// Start the execution timer
        /// </summary>
        public void StartExecutionTimer()
        {
            _executionStartTime = DateTime.Now;
            ExecutionDurationSeconds = 0;
            _executionTimer?.Start();
            OnPropertyChanged(nameof(ExecutionDurationDisplay));
        }

        /// <summary>
        /// Stop the execution timer
        /// </summary>
        public void StopExecutionTimer()
        {
            _executionTimer?.Stop();
            OnPropertyChanged(nameof(ExecutionDurationDisplay));
        }

        /// <summary>
        /// Timer elapsed event handler to update execution duration
        /// </summary>
        private void OnExecutionTimerElapsed(object sender, ElapsedEventArgs e)
        {
            ExecutionDurationSeconds = (DateTime.Now - _executionStartTime).TotalSeconds;

            // Update group execution duration if executing in groups
            if (IsExecutingInGroups && CurrentExecutionGroup > 0)
            {
                GroupExecutionDurationSeconds = (DateTime.Now - _groupExecutionStartTime).TotalSeconds;

                // Update the current group's duration in the collection
                var currentGroup = ExecutionGroups.FirstOrDefault(g => g.IsCurrentlyExecuting);
                if (currentGroup != null)
                {
                    currentGroup.ExecutionDurationSeconds = GroupExecutionDurationSeconds;
                }
            }

            Application.Current?.Dispatcher.Dispatch(() =>
            {
                OnPropertyChanged(nameof(ExecutionDurationDisplay));
                OnPropertyChanged(nameof(GroupExecutionDurationDisplay));
            });
        }

        // --- Group Execution Methods ---
        /// <summary>
        /// Initialize execution groups for tracking
        /// </summary>
        public void InitializeExecutionGroups(int groupCount)
        {
            ExecutionGroups.Clear();
            for (int i = 1; i <= groupCount; i++)
            {
                ExecutionGroups.Add(new ExecutionGroupInfo
                {
                    GroupNumber = i,
                    ModelCount = 0, // Will be updated when groups are actually processed
                    ExecutionDurationSeconds = 0,
                    IsCurrentlyExecuting = false,
                    IsCompleted = false
                });
            }

            TotalExecutionGroups = groupCount;
            IsExecutingInGroups = groupCount > 1;
            CurrentExecutionGroup = 0;
        }

        /// <summary>
        /// Start execution for a specific group
        /// </summary>
        public void StartGroupExecution(int groupNumber, int modelCount)
        {
            // Complete previous group if any
            if (CurrentExecutionGroup > 0)
            {
                CompleteGroupExecution(CurrentExecutionGroup);
            }

            CurrentExecutionGroup = groupNumber;
            _groupExecutionStartTime = DateTime.Now;
            GroupExecutionDurationSeconds = 0;

            // Update the group info
            var group = ExecutionGroups.FirstOrDefault(g => g.GroupNumber == groupNumber);
            if (group != null)
            {
                group.ModelCount = modelCount;
                group.IsCurrentlyExecuting = true;
                group.IsCompleted = false;
                group.ExecutionDurationSeconds = 0;
            }
        }

        /// <summary>
        /// Complete execution for a specific group
        /// </summary>
        public void CompleteGroupExecution(int groupNumber)
        {
            var group = ExecutionGroups.FirstOrDefault(g => g.GroupNumber == groupNumber);
            if (group != null)
            {
                // Capture the final duration before changing execution state
                if (group.IsCurrentlyExecuting && _groupExecutionStartTime != default)
                {
                    var finalDuration = (DateTime.Now - _groupExecutionStartTime).TotalSeconds;
                    group.ExecutionDurationSeconds = finalDuration;
                }

                group.IsCurrentlyExecuting = false;
                group.IsCompleted = true;
                // The final duration is now preserved in ExecutionDurationSeconds
            }
        }

        /// <summary>
        /// Reset group execution tracking
        /// </summary>
        public void ResetGroupExecution()
        {
            IsExecutingInGroups = false;
            CurrentExecutionGroup = 0;
            TotalExecutionGroups = 0;
            GroupExecutionDurationSeconds = 0;
            ExecutionGroups.Clear();
        }

        /// <summary>
        /// Initialize execution status to default values
        /// </summary>
        public void InitializeExecutionStatus()
        {
            IsExecutingModels = false;
            ExecutionStatus = "Ready";
            ExecutionProgress = 0;
            TotalModelsToExecute = 0;
            ModelsExecutedCount = 0;
            CurrentExecutingModel = "";
            CurrentExecutingModelType = "";
            ExecutionDurationSeconds = 0;
            ResetGroupExecution();
        }

        /// <summary>
        /// Reset execution progress after a delay for UI feedback
        /// </summary>
        public async Task ResetProgressAfterDelayAsync(int delayMs = 3000)
        {
            await Task.Delay(delayMs);
            if (!IsExecutingModels)
            {
                ExecutionProgress = 0;
                ModelsExecutedCount = 0;
                CurrentExecutingModel = "";
                CurrentExecutingModelType = "";
                IsExecutingInGroups = false;
                CurrentExecutionGroup = 0;
                GroupExecutionDurationSeconds = 0;
            }
        }

        // --- INotifyPropertyChanged Implementation ---
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T backingStore, T value, [CallerMemberName] string propertyName = "")
        {
            if (object.Equals(backingStore, value))
                return false;

            backingStore = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        // --- IDisposable Implementation ---
        public void Dispose()
        {
            _executionTimer?.Stop();
            _executionTimer?.Dispose();
            _executionTimer = null;

            // Dispose system monitoring service
            if (_systemMonitoringService != null)
            {
                _systemMonitoringService.PropertyChanged -= OnSystemMonitoringPropertyChanged;
                _systemMonitoringService.Dispose();
            }
        }
    }
}
