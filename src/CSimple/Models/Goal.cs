using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CSimple.Models
{
    public class Goal : INotifyPropertyChanged
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; }
        public string Description { get; set; }
        public bool IsCompleted { get; set; }
        public int Priority { get; set; } = 3; // 1-5 scale
        public DateTime Deadline { get; set; } = DateTime.Today.AddDays(7);
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string GoalType { get; set; } // Personal, Work, Learning, etc.
        public bool IsShared { get; set; }
        public double Progress { get; set; } // 0.0 to 1.0

        // Properties for shared goals
        public int SharedWith { get; set; } // Number of people shared with
        public DateTime SharedDate { get; set; } // When it was shared

        // Properties for discover goals
        public string Creator { get; set; } // Who created the goal
        public double Rating { get; set; } // User rating
        public int Downloads { get; set; } // Number of downloads
        public string CreatorImage { get; set; } // URL to creator's profile image

        // INotifyPropertyChanged implementation
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (Object.Equals(storage, value))
                return false;

            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
