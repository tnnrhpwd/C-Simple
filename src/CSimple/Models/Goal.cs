using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CSimple.Models
{
    public class Goal : INotifyPropertyChanged
    {
        private string _id;
        private string _title;
        private string _description;
        private double _progress; // 0.0 to 1.0
        private DateTime _deadline;
        private int _priority; // 1 (Low) to 5 (High)
        private bool _isShared;
        private DateTime _createdAt;
        private string _goalType; // e.g., "Personal", "Work", "Learning"

        public string Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }
        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }
        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }
        public double Progress
        {
            get => _progress;
            set => SetProperty(ref _progress, value);
        }
        public DateTime Deadline
        {
            get => _deadline;
            set => SetProperty(ref _deadline, value);
        }
        public int Priority
        {
            get => _priority;
            set => SetProperty(ref _priority, value);
        }
        public bool IsShared
        {
            get => _isShared;
            set => SetProperty(ref _isShared, value);
        }
        public DateTime CreatedAt
        {
            get => _createdAt;
            set => SetProperty(ref _createdAt, value);
        }
        public string GoalType
        {
            get => _goalType;
            set => SetProperty(ref _goalType, value);
        }

        // Parameterless constructor for serialization
        public Goal()
        {
            Id = Guid.NewGuid().ToString();
            CreatedAt = DateTime.UtcNow;
            Deadline = DateTime.UtcNow.AddDays(7); // Default deadline 1 week
            Priority = 3; // Default priority medium
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(storage, value)) return false;
            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
