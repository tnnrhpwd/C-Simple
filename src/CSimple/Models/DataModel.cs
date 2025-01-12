using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Collections.ObjectModel;

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
}

public class DataObject
{
    public string Text { get; set; }
    public List<FileItem> Files { get; set; } = new List<FileItem>();
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
    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
    private bool _isSimulating = false;
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
}

public class ActionItem
{
    public DateTime Timestamp { get; set; }
    public ushort KeyCode { get; set; } // Key Code: 49 for execute key press
    public int EventType { get; set; } // Event type: 0x0000 for keydown
    public int Duration { get; set; } // Duration: key press duration in milliseconds
    public Coordinates Coordinates { get; set; }
}

public class Coordinates // Optional, used for mouse events
{
    public int X { get; set; }
    public int Y { get; set; }
}

public class ActionModifier
{
    public string ModifierName { get; set; } // Example: "DelayModifier"
    public string Description { get; set; } // Example: "Adds a delay before executing the action"
    public int Priority { get; set; } // Example: 1 (Higher priority modifiers are applied first)
    public Func<ActionItem, int> Condition { get; set; } // Example: item => item.KeyCode == 49 (Apply only if the KeyCode is 49)
    public Action<ActionItem> ModifyAction { get; set; } // Example: item => item.Duration += 1000 (Add 1000 milliseconds to the duration)
}
