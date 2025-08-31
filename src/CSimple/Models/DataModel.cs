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

    public class UserSubscription
    {
        public string SubscriptionPlan { get; set; }
        public SubscriptionDetails SubscriptionDetails { get; set; }
    }

    public class SubscriptionDetails
    {
        public string Id { get; set; }
        public string Status { get; set; }
        public DateTime CurrentPeriodEnd { get; set; }
        public string ProductName { get; set; }
        public string PriceId { get; set; }
        public int Amount { get; set; }
        public string Currency { get; set; }
        public string Interval { get; set; }
    }

    public class UserUsage
    {
        public decimal TotalUsage { get; set; }
        public decimal AvailableCredits { get; set; }
        public decimal Limit { get; set; }
        public decimal? CustomLimit { get; set; }
        public List<UsageBreakdown> UsageBreakdown { get; set; } = new List<UsageBreakdown>();
        public string Membership { get; set; }
        public DateTime? LastReset { get; set; }
        public decimal RemainingBalance { get; set; }
        public double PercentUsed { get; set; }
        public DateTime? NextReset { get; set; }
    }

    public class UsageBreakdown
    {
        public string Api { get; set; }
        public string Date { get; set; }
        public string FullDate { get; set; }
        public string Usage { get; set; }
        public decimal Cost { get; set; }
    }

    public class UserStorage
    {
        public string TotalStorageFormatted { get; set; }
        public string StorageLimitFormatted { get; set; }
        public int ItemCount { get; set; }
        public int FileCount { get; set; }
        public long TotalStorage { get; set; }
        public long StorageLimit { get; set; }
        public double StorageUsagePercent { get; set; }
        public bool IsOverLimit { get; set; }
        public bool IsNearLimit { get; set; }
        public string Membership { get; set; }
        public List<StorageBreakdown> StorageBreakdown { get; set; } = new List<StorageBreakdown>();
    }

    public class StorageBreakdown
    {
        public bool HasFiles { get; set; }
        public DateTime? CreatedAt { get; set; }
        public int FileCount { get; set; }
        public string SizeFormatted { get; set; }
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
    private long? _size = null;

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

    // Size property with lazy calculation
    public long Size
    {
        get
        {
            if (!_size.HasValue)
            {
                _size = CalculateSize();
            }
            return _size.Value;
        }
    }

    // Formatted size for display
    public string FormattedSize => FormatFileSize(Size);

    // Calculate total size of ActionGroup
    private long CalculateSize()
    {
        long totalSize = 0;

        // Include size contribution from ActionName and Description
        totalSize += ActionName?.Length * 2 ?? 0; // Unicode chars = ~2 bytes
        totalSize += Description?.Length * 2 ?? 0;

        // Size of ActionArray (estimated)
        if (ActionArray != null)
        {
            // Rough estimate of serialized size per action item
            foreach (var item in ActionArray)
            {
                // Base size for each ActionItem (conservative estimate)
                long actionItemSize = 50; // Base struct size estimate

                // Add specific properties sizes
                actionItemSize += 8; // Timestamp (assume 8 bytes for DateTime or similar)
                actionItemSize += 4; // EventType (int)
                actionItemSize += 4; // KeyCode (int)
                actionItemSize += 4; // Duration (int)

                // Add mouse properties sizes
                actionItemSize += 8; // DeltaX & DeltaY (int * 2)
                actionItemSize += 8; // MouseData & Flags (uint * 2)
                actionItemSize += 3; // Various boolean flags (3 bytes)
                actionItemSize += 8; // TimeSinceLastMove (long)
                actionItemSize += 8; // Velocity values (float * 2)

                // Add Coordinates size if present
                if (item.Coordinates != null)
                {
                    actionItemSize += 24; // 6 int values * 4 bytes
                }

                totalSize += actionItemSize;
            }
        }

        // Include size of action modifiers
        if (ActionModifiers != null)
        {
            foreach (var modifier in ActionModifiers)
            {
                totalSize += modifier.ModifierName?.Length * 2 ?? 0;
                totalSize += modifier.Description?.Length * 2 ?? 0;
                totalSize += 4; // Priority (int)
                totalSize += 16; // Rough estimate for delegate references
            }
        }

        // Include size of files
        if (Files != null)
        {
            foreach (var file in Files)
            {
                // Filename and ContentType
                totalSize += file.Filename?.Length * 2 ?? 0;
                totalSize += file.ContentType?.Length * 2 ?? 0;

                // File data (if it's base64, each character represents ~0.75 bytes of actual data)
                if (!string.IsNullOrEmpty(file.Data))
                {
                    // Use actual length for calculation
                    totalSize += (long)(file.Data.Length * 0.75);
                }
            }
        }

        return totalSize;
    }

    // Format file size in a human-readable format
    private string FormatFileSize(long bytes)
    {
        const long KB = 1024;
        const long MB = KB * 1024;
        const long GB = MB * 1024;
        const long TB = GB * 1024;

        if (bytes < KB)
            return $"{bytes} B";
        else if (bytes < MB)
            return $"{bytes / (double)KB:0.##} KB";
        else if (bytes < GB)
            return $"{bytes / (double)MB:0.##} MB";
        else if (bytes < TB)
            return $"{bytes / (double)GB:0.##} GB";
        else
            return $"{bytes / (double)TB:0.##} TB";
    }

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