using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Maui.Controls;

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

        private bool _isSimulating;
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
            ToggleSimulateActionGroupCommand = new Command(ToggleSimulateActionGroup);

            // Initialize with some sample data or fetch from your data source
            ActionGroups = new ObservableCollection<ActionGroupModel>
            {
                new ActionGroupModel { ActionName = "Sample1", ActionArrayFormatted = "Action1, Action2" },
                new ActionGroupModel { ActionName = "Sample2", ActionArrayFormatted = "Action3, Action4" }
            };
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void OnRowTapped(ActionGroupModel actionGroup)
        {
            if (actionGroup != null)
            {
                // Logic to show details in a popup or a new page
                // Example: OpenPopup(actionGroup);
            }
        }

        private void SimulateActionGroup(ActionGroupModel actionGroup)
        {
            if (actionGroup != null)
            {
                // Logic to simulate the action group
                // Example: SimulateActions(actionGroup);
            }
        }

        private void ToggleSimulateActionGroup()
        {
            IsSimulating = !IsSimulating;
        }

        // Placeholder for your action simulation logic
        private void SimulateActions(ActionGroupModel actionGroup)
        {
            // Implement action simulation logic here
        }

        // Placeholder for your popup opening logic
        private void OpenPopup(ActionGroupModel actionGroup)
        {
            // Implement logic to open a popup with details of actionGroup
            // This might involve navigation to a new page or showing a modal
        }
    }

    public class ActionGroupModel
    {
        public string ActionName { get; set; }
        public string ActionArrayFormatted { get; set; }
    }
}
