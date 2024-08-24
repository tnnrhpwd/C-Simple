using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using System;
using System.Runtime.InteropServices;
using System.Linq;
using CSimple.Models;
using CSimple.Services;
using System.Threading.Tasks;

namespace CSimple.Pages
{
    public partial class ActionPage : ContentPage
    {
        public ObservableCollection<ActionGroup> ActionGroups { get; set; }
        public ICommand SaveActionCommand { get; set; }
        public ICommand SimulateActionGroupCommand { get; set; }
        public ICommand SaveToFileCommand { get; set; }
        public ICommand LoadFromFileCommand { get; set; }

        private string _debugOutput;
        public string DebugOutput
        {
            get => _debugOutput;
            set
            {
                _debugOutput = value;
                OnPropertyChanged(nameof(DebugOutput));
            }
        }
        private readonly FileService _fileService;

        public ActionPage()
        {
            InitializeComponent();

            _fileService = new FileService();

            // Initialize commands
            SaveActionCommand = new Command(SaveAction);
            SimulateActionGroupCommand = new Command<ActionGroup>(SimulateActionGroup);
            SaveToFileCommand = new Command(async () => await SaveActionGroupsToFile());
            LoadFromFileCommand = new Command(async () => await LoadActionGroupsFromFile());

            // Initialize ActionGroups collection
            ActionGroups = new ObservableCollection<ActionGroup>();

            // Load existing action groups from file asynchronously
            _ = LoadActionGroups(); // Ignore the returned task since we only need to ensure it's running

            DebugOutput = "Ready";
            BindingContext = this;
        }

        private async Task LoadActionGroups()
        {
            try
            {
                var actionGroups = await _fileService.LoadActionGroupsAsync();
                ActionGroups.Clear();
                foreach (var actionGroup in actionGroups)
                {
                    ActionGroups.Add(actionGroup);
                }
                DebugOutput = "Action Groups Loaded";
            }
            catch (Exception ex)
            {
                DebugOutput = $"Error loading action groups: {ex.Message}";
            }
        }

        private void SaveAction()
        {
            string actionName = ActionNameEntry.Text?.Trim();
            string actionArrayText = ActionArrayEntry.Text?.Trim();

            if (!string.IsNullOrEmpty(actionName) && !string.IsNullOrEmpty(actionArrayText))
            {
                var actions = actionArrayText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(a => a.Trim()).ToArray();
                ActionGroups.Add(new ActionGroup { ActionName = actionName, ActionArray = actions });
                DebugOutput = $"Saved Action Group: {actionName}";

                // Trigger save to file after adding new action group
                SaveActionGroupsToFile().ConfigureAwait(false);
            }
            else
            {
                DebugOutput = "Please enter both Action Name and Action Array.";
            }
        }

private void SimulateActionGroup(ActionGroup actionGroup)
{
    DebugOutput = $"Simulating Actions for: {actionGroup.ActionName}";
    
    try
    {
        foreach (var action in actionGroup.ActionArray)
        {
            DebugOutput = $"Processing Action: {action}";
            
            if (action.StartsWith("MouseMove"))
            {
                var coordinates = action.Replace("MouseMove:", "").Trim();
                var coordPairs = coordinates.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                if (coordPairs.Length == 2 &&
                    int.TryParse(coordPairs[0], out int x) &&
                    int.TryParse(coordPairs[1], out int y))
                {
                    DebugOutput = $"Moving Mouse to X: {x}, Y: {y}";
                    MoveMouse(x, y);
                }
                else
                {
                    DebugOutput = $"Invalid MouseMove coordinates: {coordinates}";
                }
            }
            else if (action.StartsWith("KeyPress"))
            {
                var key = action.Replace("KeyPress:", "").Trim();
                if (Enum.TryParse(key, out VirtualKey virtualKey))
                {
                    DebugOutput = $"Simulating KeyPress: {virtualKey}";
                    SimulateKeyPress(virtualKey);
                }
                else
                {
                    DebugOutput = $"Invalid KeyPress command: {key}";
                }
            }
            else
            {
                DebugOutput = $"Executing Command: {action}";
                ExecuteWindowsCommand(action);
            }
        }
        
        DebugOutput = $"Completed Simulation for: {actionGroup.ActionName}";
    }
    catch (Exception ex)
    {
        DebugOutput = $"Error during simulation: {ex.Message}";
    }
}



        private async Task SaveActionGroupsToFile()
        {
            try
            {
                var actionGroupsToSave = ActionGroups.Cast<object>().ToList();
                await _fileService.SaveActionGroupsAsync(actionGroupsToSave);
                DebugOutput = "Action Groups Saved to File";
            }
            catch (Exception ex)
            {
                DebugOutput = $"Error saving action groups: {ex.Message}";
            }
        }

        private async Task LoadActionGroupsFromFile()
        {
            try
            {
                var loadedActionGroups = await _fileService.LoadActionGroupsAsync();
                ActionGroups = new ObservableCollection<ActionGroup>(loadedActionGroups);
                DebugOutput = "Action Groups Loaded from File";
            }
            catch (Exception ex)
            {
                DebugOutput = $"Error loading action groups from file: {ex.Message}";
            }
        }

        // P/Invoke for volume commands
        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        // P/Invoke for mouse commands
        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int X, int Y);

        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

        private const byte VK_VOLUME_MUTE = 0xAD;
        private const byte VK_VOLUME_DOWN = 0xAE;
        private const byte VK_VOLUME_UP = 0xAF;
        private const uint MOUSEEVENTF_MOVE = 0x0001;

        private void ExecuteWindowsCommand(string command)
        {
            switch (command.ToLower())
            {
                case "mute":
                    SendVolumeCommand(VK_VOLUME_MUTE);
                    break;
                case "volumeup":
                    SendVolumeCommand(VK_VOLUME_UP);
                    break;
                case "volumedown":
                    SendVolumeCommand(VK_VOLUME_DOWN);
                    break;
                default:
                    DebugOutput = $"Unknown Command: {command}";
                    break;
            }
        }

        public enum VirtualKey
        {
            F5 = 0x74,
            // Add other key codes here
            WindowsKeyLeft = 0x5B,
            WindowsKeyRight = 0x5C
            // Add more as needed
        }

        private const uint KEYEVENTF_KEYUP = 0x0002;

        private void SimulateKeyPress(VirtualKey key)
        {
            // Simulate key down
            keybd_event((byte)key, 0, 0, UIntPtr.Zero);
            // Simulate key up
            keybd_event((byte)key, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            DebugOutput = $"Executed KeyPress: {key}";
        }

        private void SendVolumeCommand(byte volumeCommand)
        {
            keybd_event(volumeCommand, 0, 0, UIntPtr.Zero);
            keybd_event(volumeCommand, 0, 0x0002, UIntPtr.Zero); // Key up
            DebugOutput = $"Executed Volume Command: {(volumeCommand == VK_VOLUME_MUTE ? "Mute" : volumeCommand == VK_VOLUME_DOWN ? "Volume Down" : "Volume Up")}";
        }

        private void MoveMouse(int x, int y)
        {
            SetCursorPos(x, y);
            mouse_event(MOUSEEVENTF_MOVE, (uint)x, (uint)y, 0, UIntPtr.Zero);
            DebugOutput = $"Moved Mouse to Position: {x},{y}";
        }
    }
}
