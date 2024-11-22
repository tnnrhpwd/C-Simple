using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace CSimple.ViewModels
{
    public class ActionViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private ObservableCollection<ActionGroupModel> _actionGroups;
        public ObservableCollection<ActionGroupModel> ActionGroups
        {
            get => _actionGroups;
            set
            {
                _actionGroups = value;
                OnPropertyChanged();
            }
        }

        private bool _isSimulating = false;
        public bool IsSimulating
        {
            get => _isSimulating;
            set
            {
                _isSimulating = value;
                OnPropertyChanged();
            }
        }

        public ICommand RowTappedCommand { get; }
        public ICommand SimulateActionGroupCommand { get; }
        public ICommand ToggleSimulateActionGroupCommand { get; }

        public ActionViewModel()
        {
            RowTappedCommand = new Command<ActionGroupModel>(OnRowTapped);
            SimulateActionGroupCommand = new Command<ActionGroupModel>(SimulateActionGroup);
            ToggleSimulateActionGroupCommand = new Command<ActionGroupModel>(ToggleSimulateActionGroup);

            // Initialize with some sample data or fetch from your data source
            ActionGroups = new ObservableCollection<ActionGroupModel>
            {
                new ActionGroupModel { ActionName = "Sample1", ActionArrayFormatted = "Action1, Action2" },
                new ActionGroupModel { ActionName = "Sample2", ActionArrayFormatted = "Action3, Action4" }
            };
        }

        // Implement INotifyPropertyChanged interface
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            // Ensure the PropertyChanged event is not null before invoking it
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // Implement the logic for the commands
        private void OnRowTapped(ActionGroupModel actionGroup)
        {
            if (actionGroup != null)
            {
                // Implement logic to show details in a popup or a new page
                Debug.WriteLine("Logic to show details in a popup or a new page");
                // Example: OpenPopup(actionGroup);
            }
        }

        private async void SimulateActionGroup(ActionGroupModel actionGroup)
        {
            if (actionGroup != null)
            {
                actionGroup.IsSimulating = true;
                try
                {
                    // Implement logic to simulate the actions in actionGroup
                    Debug.WriteLine($"Simulating actions for {actionGroup.ActionName}");
                    await Task.Delay(2000); // Simulate some delay for the actions
                }
                finally
                {
                    actionGroup.IsSimulating = false;
                }
            }
        }

        private void ToggleSimulateActionGroup(ActionGroupModel actionGroup)
        {
            if (actionGroup != null)
            {
                Debug.WriteLine("Toggling simulation state");
                actionGroup.IsSimulating = !actionGroup.IsSimulating;
                if (actionGroup.IsSimulating)
                {
                    SimulateActionGroup(actionGroup);
                }
            }
        }

        // Placeholder for your action simulation logic
        private void SimulateActions(ActionGroupModel actionGroup)
        {
            // Implement logic to simulate the actions in actionGroup
            Debug.WriteLine($"Simulating actions for {actionGroup.ActionName}");
        }

        // Placeholder for your popup opening logic
        private void OpenPopup(ActionGroupModel actionGroup)
        {
            // Implement logic to open a popup with details of actionGroup
            // This might involve navigation to a new page or showing a modal
            Debug.WriteLine($"Opening popup for {actionGroup.ActionName}");
        }
    }

    public class ActionGroupModel : INotifyPropertyChanged
    {
        private bool _isSimulating;

        public string ActionName { get; set; }
        public string ActionArrayFormatted { get; set; }

        public bool IsSimulating
        {
            get => _isSimulating;
            set
            {
                if (_isSimulating != value)
                {
                    _isSimulating = value;
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
}
