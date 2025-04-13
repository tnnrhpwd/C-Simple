using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CSimple.Models
{
    public class DataModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public class User
        {
            public string _id { get; set; }
            public string Nickname { get; set; }
            public string Email { get; set; }
            public string Token { get; set; }
        }

        public List<DataItem> Data { get; set; } = new List<DataItem>();
        public bool DataIsError { get; set; }
        public bool DataIsSuccess { get; set; }
        public bool DataIsLoading { get; set; }
        public string DataMessage { get; set; }
        public string Operation { get; set; }
    }

    public class DataItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public DataObject Data { get; set; } = new DataObject();
        public DateTime updatedAt { get; set; }
        public DateTime createdAt { get; set; }
        public string _id { get; set; }
        public int __v { get; set; }
        public string Creator { get; set; }
        public bool IsPublic { get; set; }
        public bool deleted { get; set; }
    }

    public class DataObject
    {
        public string Text { get; set; }
        public List<ActionFile> Files { get; set; } = new List<ActionFile>();
        public ActionGroup ActionGroupObject { get; set; } = new ActionGroup();
    }

    public class FileItem
    {
        public string filename { get; set; }
        public string contentType { get; set; }
        public string data { get; set; }
    }

    public class ActionGroup : INotifyPropertyChanged
    {
        private bool _isSimulating = false;
        private ObservableCollection<ActionStep> _recentSteps;

        public event PropertyChangedEventHandler PropertyChanged;

        public Guid Id { get; set; } = Guid.NewGuid();
        public string ActionName { get; set; }
        public List<ActionItem> ActionArray { get; set; } = new List<ActionItem>();
        public List<ActionModifier> ActionModifiers { get; set; } = new List<ActionModifier>();
        public string ActionArrayFormatted { get; set; }
        public bool IsSimulating
        {
            get => _isSimulating;
            set
            {
                if (_isSimulating != value)
                {
                    _isSimulating = value;
                    OnPropertyChanged(nameof(IsSimulating));
                }
            }
        }
        public string Category { get; set; } = "Productivity";
        public DateTime? CreatedAt { get; set; } = DateTime.Now;
        public string ActionType { get; set; } = "Custom Action";
        public int UsageCount { get; set; } = 0;
        public double SuccessRate { get; set; } = 0.85;
        public bool IsPartOfTraining { get; set; } = false;
        public bool IsChained { get; set; } = false;
        public bool HasMetrics { get; set; } = false;
        public string Description { get; set; } = string.Empty;
        public bool IsSelected { get; set; } = false;
        public string ChainName { get; set; } = string.Empty;
        public List<ActionFile> Files { get; set; } = new List<ActionFile>(); // Add this property to store attached files
        public bool IsLocal { get; set; } = false; // Indicates if the action is locally stored

        public ObservableCollection<ActionStep> RecentSteps
        {
            get => _recentSteps ?? (_recentSteps = new ObservableCollection<ActionStep>());
            set
            {
                if (_recentSteps != value)
                {
                    _recentSteps = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HasRecentSteps));
                }
            }
        }

        public bool HasRecentSteps => RecentSteps != null && RecentSteps.Count > 0;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class ActionStep : INotifyPropertyChanged
    {
        private string _stepNumber;
        private string _stepAction;
        private bool _isSuccess;
        private DateTime _executedAt;
        private string _duration;

        public string StepNumber
        {
            get => _stepNumber;
            set
            {
                if (_stepNumber != value)
                {
                    _stepNumber = value;
                    OnPropertyChanged();
                }
            }
        }

        public string StepAction
        {
            get => _stepAction;
            set
            {
                if (_stepAction != value)
                {
                    _stepAction = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsSuccess
        {
            get => _isSuccess;
            set
            {
                if (_isSuccess != value)
                {
                    _isSuccess = value;
                    OnPropertyChanged();
                }
            }
        }

        public DateTime ExecutedAt
        {
            get => _executedAt;
            set
            {
                if (_executedAt != value)
                {
                    _executedAt = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Duration
        {
            get => _duration;
            set
            {
                if (_duration != value)
                {
                    _duration = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class ActionModifier
    {
        public string ModifierName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int Priority { get; set; } = 0;
        public Func<ActionItem, int> Condition { get; set; }
        public Action<ActionItem> ModifyAction { get; set; }
    }

    public class ActionItem
    {
        public object Timestamp { get; set; }
        public int EventType { get; set; }
        public int KeyCode { get; set; }
        public int Duration { get; set; } // Hold duration in milliseconds

        // Enhanced mouse movement properties
        public Coordinates Coordinates { get; set; }
        public int DeltaX { get; set; } // Raw delta X
        public int DeltaY { get; set; } // Raw delta Y
        public uint MouseData { get; set; }
        public uint Flags { get; set; }
        public bool IsLeftButtonDown { get; set; }
        public bool IsRightButtonDown { get; set; }
        public bool IsMiddleButtonDown { get; set; }
        public long TimeSinceLastMoveMs { get; set; }
        public float VelocityX { get; set; }
        public float VelocityY { get; set; }

        // Additional properties to track button state changes 
        public bool IsButtonStateChange { get; set; }
        public bool IsPrimaryButtonEvent =>
            EventType == 0x0201 || EventType == 0x0202; // WM_LBUTTONDOWN or WM_LBUTTONUP

        public bool IsSecondaryButtonEvent =>
            EventType == 0x0204 || EventType == 0x0205; // WM_RBUTTONDOWN or WM_RBUTTONUP

        public bool IsMiddleButtonEvent =>
            EventType == 0x0207 || EventType == 0x0208; // WM_MBUTTONDOWN or WM_MBUTTONUP

        // Touch input properties
        public bool IsTouch { get; set; }
        public int TouchId { get; set; }
        public int TouchAction { get; set; } // 0=None, 1=Down, 2=Move, 3=Up, 4=Cancel
        public int TouchWidth { get; set; }  // Contact width in device units
        public int TouchHeight { get; set; } // Contact height in device units
        public float Pressure { get; set; }  // Touch pressure if available

        public override string ToString()
        {
            if (IsTouch)
            {
                string actionName = TouchAction == 1 ? "Down" :
                                   TouchAction == 2 ? "Move" :
                                   TouchAction == 3 ? "Up" :
                                   TouchAction == 4 ? "Cancel" : "Unknown";

                return $"Touch {actionName} at X:{Coordinates?.X}, Y:{Coordinates?.Y}, ID:{TouchId}";
            }
            else if (EventType == 256 || EventType == 257) // Keyboard events
                return $"Key {KeyCode} {(EventType == 256 ? "Down" : "Up")}";
            else if (EventType == 512) // Mouse move
                return $"Mouse Move (Raw) DeltaX:{DeltaX}, DeltaY:{DeltaY}";
            else if (EventType == 0x0201) // Left mouse button down
                return $"Left Click";
            else if (EventType == 0x0204) // Right mouse button down
                return $"Right Click";
            else
                return $"Action Type:{EventType} at {Timestamp}";
        }
    }

    public class Coordinates
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int AbsoluteX { get; set; }
        public int AbsoluteY { get; set; }
        public int RelativeX { get; set; }
        public int RelativeY { get; set; }
    }

    public class ModelAssignment
    {
        public string ModelId { get; set; }
        public string ModelName { get; set; }
        public string ModelType { get; set; }
        public DateTime AssignedDate { get; set; } = DateTime.Now;
    }

    public class ActionFile
    {
        public string Filename { get; set; }
        public string ContentType { get; set; }
        public string Data { get; set; }
        public DateTime AddedAt { get; set; } = DateTime.Now;
        public bool IsProcessed { get; set; } = false;
    }

    /// <summary>
    /// Extension methods for ActionGroup
    /// </summary>
    public static class ActionGroupExtensions
    {
        /// <summary>
        /// Gets the Files property from an ActionGroup or returns null if it doesn't exist
        /// </summary>
        public static List<ActionFile> GetFiles(this ActionGroup actionGroup)
        {
            if (actionGroup == null) return null;

            try
            {
                // Try to get Files via reflection
                var filesProperty = actionGroup.GetType().GetProperty("Files");
                if (filesProperty != null)
                {
                    return filesProperty.GetValue(actionGroup) as List<ActionFile>;
                }
            }
            catch
            {
                // Ignore errors and return null
            }

            return null;
        }

        /// <summary>
        /// Sets the Files property on an ActionGroup if it exists
        /// </summary>
        public static void SetFiles(this ActionGroup actionGroup, List<ActionFile> files)
        {
            if (actionGroup == null) return;

            try
            {
                // Try to set Files via reflection
                var filesProperty = actionGroup.GetType().GetProperty("Files");
                if (filesProperty != null)
                {
                    filesProperty.SetValue(actionGroup, files);
                }
            }
            catch
            {
                // Ignore errors
            }
        }
    }
}
