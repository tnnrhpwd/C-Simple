using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using System;
using System.Runtime.InteropServices;
using System.Linq;
using CSimple.Models;
using CSimple.Services;

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
            // Initialize collection with sample data
            // ActionGroups = new ObservableCollection<ActionGroup>
            // {
            //     new ActionGroup { ActionName = "Increase Volume", ActionArray = new string[] { "VolumeUp", "VolumeUp", "VolumeUp" } },
            //     new ActionGroup { ActionName = "Mute Volume", ActionArray = new string[] { "Mute" } },
            //     new ActionGroup { ActionName = "Decrease Volume", ActionArray = new string[] { "VolumeDown", "VolumeDown", "VolumeDown" } },
            //     new ActionGroup { ActionName = "Move Mouse", ActionArray = new string[] { "MouseMove:100,200", "MouseMove:200,400" } }
            // };

            _fileService = new FileService();

            // Load existing action groups from file
            LoadActionGroups();

            // Initialize commands
            SaveActionCommand = new Command(SaveAction);
            SimulateActionGroupCommand = new Command<ActionGroup>(SimulateActionGroup);
            SaveToFileCommand = new Command(async () => await SaveActionGroupsToFile());
            LoadFromFileCommand = new Command(async () => await LoadActionGroupsFromFile());

            DebugOutput = "Ready";
            BindingContext = this;
        }
        private void SaveAction()
        {
            string actionName = ActionNameEntry.Text?.Trim();
            string actionArrayText = ActionArrayEntry.Text?.Trim();

            if (!string.IsNullOrEmpty(actionName) && !string.IsNullOrEmpty(actionArrayText))
            {
                var actions = actionArrayText.Split(',').Select(a => a.Trim()).ToArray();
                ActionGroups.Add(new ActionGroup { ActionName = actionName, ActionArray = actions });
                DebugOutput = $"Saved Action Group: {actionName}";
            }
            else
            {
                DebugOutput = "Please enter both Action Name and Action Array.";
            }
        }

    private void SimulateActionGroup(ActionGroup actionGroup)
        {
            DebugOutput = $"Simulating Actions for: {actionGroup.ActionName}";

            foreach (var action in actionGroup.ActionArray)
            {
                if (action.StartsWith("MouseMove"))
                {
                    var coordinates = action.Replace("MouseMove:", "").Split(',');
                    if (coordinates.Length == 2 &&
                        int.TryParse(coordinates[0], out int x) &&
                        int.TryParse(coordinates[1], out int y))
                    {
                        MoveMouse(x, y);
                    }
                }
                else
                {
                    ExecuteWindowsCommand(action);
                }
            }

            DebugOutput = $"Completed Simulation for: {actionGroup.ActionName}";
        }
        private async void LoadActionGroups()
        {
            ActionGroups = await _fileService.LoadActionGroupsAsync(); //Cannot implicitly convert type 'System.Collections.ObjectModel.ObservableCollection<CSimple.Models.ActionGroup>' to 'System.Collections.ObjectModel.ObservableCollection<CSimple.Pages.ActionGroup>'CS0029
            DebugOutput = "Action Groups Loaded";
        }

        private async Task SaveActionGroupsToFile()
        {
            await _fileService.SaveActionGroupsAsync(ActionGroups); // Argument 1: cannot convert from 'System.Collections.ObjectModel.ObservableCollection<CSimple.Pages.ActionGroup>' to 'System.Collections.ObjectModel.ObservableCollection<CSimple.Models.ActionGroup>'CS1503
            DebugOutput = "Action Groups Saved to File";
        }

        private async Task LoadActionGroupsFromFile()
        {
            ActionGroups = await _fileService.LoadActionGroupsAsync(); //Cannot implicitly convert type 'System.Collections.ObjectModel.ObservableCollection<CSimple.Models.ActionGroup>' to 'System.Collections.ObjectModel.ObservableCollection<CSimple.Pages.ActionGroup>'CS0029
            DebugOutput = "Action Groups Loaded from File";
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