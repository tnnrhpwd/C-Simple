using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using System;
using System.Runtime.InteropServices;
using System.Linq;
using CSimple.Models;
using CSimple.Services;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Globalization;

namespace CSimple.Pages
{
    public partial class ActionPage : ContentPage
    {
        public ObservableCollection<ActionGroup> ActionGroups { get; set; }
        public ICommand SaveActionCommand { get; set; }
        public ICommand SimulateActionGroupCommand { get; set; }
        public ICommand SaveToFileCommand { get; set; }
        public ICommand LoadFromFileCommand { get; set; }
        public ICommand RowTappedCommand { get; }

        private void DebugOutput(string message)
        {
            Debug.WriteLine(message);
        }
        private readonly FileService _fileService;

        public ActionPage()
        {
            InitializeComponent();

            _fileService = new FileService();

            // Initialize commands
            SaveActionCommand = new Command(SaveAction);
            SimulateActionGroupCommand = new Command<ActionGroup>(async (actionGroup) => await SimulateActionGroupAsync(actionGroup));
            SaveToFileCommand = new Command(async () => await SaveActionGroupsToFile());
            LoadFromFileCommand = new Command(async () => await LoadActionGroupsFromFile());

            RowTappedCommand = new Command<ActionGroup>(OnRowTapped);
            // BindingContext = new ActionPageViewModel();
            // Initialize ActionGroups collection
            ActionGroups = new ObservableCollection<ActionGroup>();

            // Load existing action groups from file asynchronously
            _ = LoadActionGroups(); // Ignore the returned task since we only need to ensure it's running

            DebugOutput("Ready");
            BindingContext = this;
        }
        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await LoadActionGroups();
        }
        private async void OnRowTapped(ActionGroup actionGroup)
        {
            var actionDetailPage = new ActionDetailPage(actionGroup);
            await Navigation.PushModalAsync(actionDetailPage);
        }
        public class ActionPageViewModel
        {
            public ICommand SimulateActionGroupCommand { get; }
            public ICommand RowTappedCommand { get; }
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
                DebugOutput("Action Groups Loaded");
            }
            catch (Exception ex)
            {
                DebugOutput($"Error loading action groups: {ex.Message}");
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
                DebugOutput($"Saved Action Group: {actionName}");

                // Trigger save to file after adding new action group
                SaveActionGroupsToFile().ConfigureAwait(false);
            }
            else
            {
                DebugOutput("Please enter both Action Name and Action Array.");
            }
        }

        private async Task SimulateActionGroupAsync(ActionGroup actionGroup)
        {
            DebugOutput($"Simulating Actions for: {actionGroup.ActionName}");

            try
            {
                DateTime? previousActionTime = null;

                foreach (var action in actionGroup.ActionArray)
                {
                    DebugOutput($"Processing Action: {action}");

                    var actionParts = action.Split(' ');

                    if (actionParts.Length >= 2)
                    {
                        string time = actionParts[0];
                        DateTime currentActionTime = DateTime.ParseExact(time, "HH:mm:ss.fff", CultureInfo.InvariantCulture);

                        // Delay based on the difference between current and previous action times
                        if (previousActionTime.HasValue)
                        {
                            TimeSpan delay = currentActionTime - previousActionTime.Value;
                            DebugOutput($"Waiting for {delay.TotalMilliseconds} ms before next action.");
                            await Task.Delay(delay);
                        }

                        previousActionTime = currentActionTime;

                        int keyCode = int.Parse(actionParts[1]);

                        if (actionParts.Length == 4)
                        {
                            int x = int.Parse(actionParts[2]);
                            int y = int.Parse(actionParts[3]);

                            DebugOutput($"Simulating Mouse Action at {time} with KeyCode: {keyCode} at X: {x}, Y: {y}");
                            InputSimulator.MoveMouse(x, y);

                            if (keyCode == 512)
                            {
                                DebugOutput("Mouse moved.");
                            }
                            else if (keyCode == (int)WM_LBUTTONDOWN)
                            {
                                InputSimulator.SimulateMouseClick(MouseButton.Left);
                            }
                            else if (keyCode == (int)WM_RBUTTONDOWN)
                            {
                                InputSimulator.SimulateMouseClick(MouseButton.Right);
                            }
                            else
                            {
                                DebugOutput($"Unhandled mouse event key code: {keyCode}");
                            }
                        }
                        else if (actionParts.Length == 2)
                        {
                            DebugOutput($"Simulating KeyPress at {time} with KeyCode: {keyCode}");

                            if (Enum.IsDefined(typeof(VirtualKey), keyCode))
                            {
                                VirtualKey virtualKey = (VirtualKey)keyCode;
                                InputSimulator.SimulateKeyPress(virtualKey);
                            }
                            else
                            {
                                DebugOutput($"Invalid KeyPress key code: {keyCode}");
                            }
                        }
                        else
                        {
                            DebugOutput($"Invalid action format: {action}");
                        }
                    }
                    else
                    {
                        DebugOutput($"Invalid action format: {action}");
                    }
                }

                DebugOutput($"Completed Simulation for: {actionGroup.ActionName}");
            }
            catch (Exception ex)
            {
                DebugOutput($"Error during simulation: {ex.Message}");
            }
        }


        private async Task SaveActionGroupsToFile()
        {
            try
            {
                await _fileService.SaveActionGroupsAsync(ActionGroups);
                DebugOutput("Action Groups Saved to File");
            }
            catch (Exception ex)
            {
                DebugOutput($"Error saving action groups: {ex.Message}");
            }
        }

        private async Task LoadActionGroupsFromFile()
        {
            try
            {
                var loadedActionGroups = await _fileService.LoadActionGroupsAsync();
                ActionGroups = new ObservableCollection<ActionGroup>(loadedActionGroups);
                DebugOutput("Action Groups Loaded from File");
            }
            catch (Exception ex)
            {
                DebugOutput($"Error loading action groups from file: {ex.Message}");
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
        private const int WH_KEYBOARD_LL = 13;
        private const int WH_MOUSE_LL = 14;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_MOUSEMOVE = 0x0200;
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
                    DebugOutput($"Unknown Command: {command}");
                    break;
            }
        }

        private const uint KEYEVENTF_KEYUP = 0x0002;

        private void SimulateKeyPress(VirtualKey key)
        {
            // Simulate key down
            keybd_event((byte)key, 0, 0, UIntPtr.Zero);
            // Simulate key up
            keybd_event((byte)key, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            DebugOutput($"Executed KeyPress: {key}");
        }

        private void SendVolumeCommand(byte volumeCommand)
        {
            keybd_event(volumeCommand, 0, 0, UIntPtr.Zero);
            keybd_event(volumeCommand, 0, 0x0002, UIntPtr.Zero); // Key up
            DebugOutput($"Executed Volume Command: {(volumeCommand == VK_VOLUME_MUTE ? "Mute" : volumeCommand == VK_VOLUME_DOWN ? "Volume Down" : "Volume Up")}");
        }

        private void MoveMouse(int x, int y)
        {
            SetCursorPos(x, y);
            mouse_event(MOUSEEVENTF_MOVE, (uint)x, (uint)y, 0, UIntPtr.Zero);
            DebugOutput($"Moved Mouse to Position: {x},{y}");
        }
    }
}
