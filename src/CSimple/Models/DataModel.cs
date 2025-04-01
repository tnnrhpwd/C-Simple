using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using CSimple; // Add reference to CSimple namespace

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
    public bool deleted { get; set; } // Add the missing deleted property
}

public class DataItemComparer : IEqualityComparer<DataItem>
{
    public bool Equals(DataItem x, DataItem y)
    {
        if (x == null || y == null) return false;
        return x._id == y._id; // Compare by unique ID
    }

    public int GetHashCode(DataItem obj)
    {
        return obj._id?.GetHashCode() ?? 0;
    }
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

    public Guid Id { get; set; } = Guid.NewGuid(); // Unique identifier for each ActionGroup
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
    public double SuccessRate { get; set; } = 0.85; // 85% default success rate
    public bool IsPartOfTraining { get; set; } = false;
    public bool IsChained { get; set; } = false;
    public bool HasMetrics { get; set; } = false;
    public string Description { get; set; } = string.Empty;
    public bool IsSelected { get; set; } = false;
    public string ChainName { get; set; } = string.Empty;
    public bool IsLocal { get; set; } = false; // Indicates if the action is locally stored
    public List<ActionFile> Files { get; set; } = new List<ActionFile>(); // Add this property to store attached files

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

public class Coordinates // Optional, used for mouse events
{
    public int X { get; set; }
    public int Y { get; set; }
}