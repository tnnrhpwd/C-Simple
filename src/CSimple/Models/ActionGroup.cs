using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CSimple.Models
{
    public partial class ActionGroup : INotifyPropertyChanged
    {
        private ObservableCollection<ActionStep> _recentSteps;

        // Add a property for recent step executions
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

        // Helper property to check if there are any recent steps
        public bool HasRecentSteps => RecentSteps != null && RecentSteps.Count > 0;

        // Rest of existing ActionGroup implementation

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
