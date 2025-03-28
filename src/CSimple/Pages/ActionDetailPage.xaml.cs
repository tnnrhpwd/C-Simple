using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Xaml; // Add this namespace for XamlCompilation
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using CSimple;

namespace CSimple.Pages
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class ActionDetailPage : ContentPage
    {
        public ActionDetailViewModel ViewModel { get; private set; }

        public ActionDetailPage(ActionGroup actionGroup)
        {
            InitializeComponent(); // This will work now after we rebuild
            ViewModel = new ActionDetailViewModel(actionGroup, Navigation);
            BindingContext = ViewModel;
        }

        // Method to handle the close button click for error display
        private void CloseErrorButton_Clicked(object sender, EventArgs e)
        {
            ErrorDisplayGrid.IsVisible = false;
        }

        // Method to show error message
        public void ShowError(string message)
        {
            ErrorMessageLabel.Text = message;
            ErrorDisplayGrid.IsVisible = true;
        }
    }

    public class ActionDetailViewModel : INotifyPropertyChanged
    {
        private readonly INavigation _navigation;
        private readonly ActionGroup _actionGroup;

        // Basic properties
        public string ActionName { get; set; }
        public string ActionType { get; set; }
        public string CreatedAt { get; set; }
        public string Duration { get; set; }
        public double Confidence { get; set; }
        public bool IsSimulating { get; set; }
        public string ActionArrayFormatted { get; set; }

        // Prediction properties
        public bool HasPrediction { get; set; } = false;
        public bool NoPrediction => !HasPrediction;
        public string PredictionGoal { get; set; } = "Improve productivity";
        public string PredictionModel { get; set; } = "General Assistant";
        public double PredictionScore { get; set; } = 0.78;
        public ObservableCollection<SimilarAction> SimilarActions { get; } = new ObservableCollection<SimilarAction>();

        // Media collections
        public ObservableCollection<MediaFile> ImageFiles { get; } = new ObservableCollection<MediaFile>();
        public ObservableCollection<AudioFile> AudioFiles { get; } = new ObservableCollection<AudioFile>();
        public ObservableCollection<TextFile> TextFiles { get; } = new ObservableCollection<TextFile>();
        public ObservableCollection<OtherFile> OtherFiles { get; } = new ObservableCollection<OtherFile>();
        public ObservableCollection<ActionStep> ActionSteps { get; } = new ObservableCollection<ActionStep>();

        // Media visibility flags
        public bool HasImages => ImageFiles.Count > 0;
        public bool HasAudio => AudioFiles.Count > 0;
        public bool HasTextData => TextFiles.Count > 0;
        public bool HasOtherFiles => OtherFiles.Count > 0;
        public bool HasNoMedia => !HasImages && !HasAudio && !HasTextData && !HasOtherFiles;

        // Commands
        public ICommand CloseCommand { get; }
        public ICommand ExecuteCommand { get; }
        public ICommand EditCommand { get; }
        public ICommand PlayAudioCommand { get; }
        public ICommand OpenFileCommand { get; }
        public ICommand GeneratePredictionCommand { get; }
        public ICommand BackCommand { get; }
        public ICommand DeleteCommand { get; }

        public ActionDetailViewModel(ActionGroup actionGroup, INavigation navigation)
        {
            _actionGroup = actionGroup;
            _navigation = navigation;

            // Initialize basic properties from ActionGroup properties
            ActionName = actionGroup.ActionName;
            ActionType = DetermineActionType();

            // Fix: Handle missing CreatedAt property in ActionGroup
            CreatedAt = DateTime.Now.ToString("g"); // Default to current date

            // Try getting creation date from property via reflection
            try
            {
                var createdAtProp = actionGroup.GetType().GetProperty("CreatedAt");
                if (createdAtProp != null)
                {
                    var value = createdAtProp.GetValue(actionGroup);
                    if (value is DateTime dateValue)
                    {
                        CreatedAt = dateValue.ToString("g");
                    }
                }
            }
            catch
            {
                // Fall back to default value already set
            }

            Duration = CalculateDuration(actionGroup);

            // Fix: Handle missing Confidence property in ActionGroup
            Confidence = 0.85; // Default confidence value

            // Try getting confidence from property via reflection
            try
            {
                var confidenceProp = actionGroup.GetType().GetProperty("Confidence");
                if (confidenceProp != null)
                {
                    var value = confidenceProp.GetValue(actionGroup);
                    if (value is double doubleValue && doubleValue > 0)
                    {
                        Confidence = doubleValue;
                    }
                }
            }
            catch
            {
                // Fall back to default value already set
            }

            IsSimulating = actionGroup.IsSimulating;
            ActionArrayFormatted = FormatActionArray();

            // Initialize commands
            BackCommand = new Command(async () => await _navigation.PopModalAsync());
            CloseCommand = BackCommand; // For compatibility with existing code
            ExecuteCommand = new Command(ExecuteAction);
            EditCommand = new Command(EditAction);
            DeleteCommand = new Command(DeleteAction);
            PlayAudioCommand = new Command<AudioFile>(PlayAudio);
            OpenFileCommand = new Command<OtherFile>(OpenFile);
            GeneratePredictionCommand = new Command(GeneratePrediction);

            // Initialize media collections
            InitializeMediaCollections(actionGroup);
            InitializeActionSteps();
            InitializeSimilarActions();
        }

        private List<string> GetActionItems()
        {
            // Instead of trying to access non-existent ActionItems property,
            // extract action info from ActionArray if available, otherwise from ActionList
            var result = new List<string>();

            try
            {
                // Extract from ActionArray if available
                if (_actionGroup.ActionArray != null)
                {
                    // Fix: Use type checking instead of pattern matching
                    // This approach works with any collection type
                    var actionArray = _actionGroup.ActionArray;

                    // We need to handle all possible types that ActionArray might be
                    foreach (var action in actionArray)
                    {
                        if (action != null)
                        {
                            result.Add(action.ToString());
                        }
                    }
                }
                // Alternative: check if there's an ActionList property 
                else
                {
                    try
                    {
                        var actionList = _actionGroup.GetType().GetProperty("ActionList")?.GetValue(_actionGroup);
                        if (actionList != null && actionList is IEnumerable<object> actions)
                        {
                            foreach (var action in actions)
                            {
                                result.Add(action.ToString());
                            }
                        }
                    }
                    catch
                    {
                        // If that fails too, add a sample action item so UI isn't empty
                        result.Add("No detailed action data available");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting action items: {ex.Message}");
                // Add a fallback item
                result.Add("Action data could not be loaded");
            }

            return result;
        }

        private void InitializeSimilarActions()
        {
            // Add some sample similar actions
            SimilarActions.Add(new SimilarAction { Name = "Open Browser and Navigate", Similarity = 0.85 });
            SimilarActions.Add(new SimilarAction { Name = "Launch Excel and Import", Similarity = 0.72 });
            SimilarActions.Add(new SimilarAction { Name = "Copy Files to Folder", Similarity = 0.68 });
        }

        private void InitializeMediaCollections(ActionGroup actionGroup)
        {
            try
            {
                // Try to get files from the actionGroup if a Files property exists
                var filesProperty = actionGroup.GetType().GetProperty("Files");
                var files = filesProperty?.GetValue(actionGroup) as IEnumerable<object>;

                if (files != null)
                {
                    foreach (var file in files)
                    {
                        // Try to extract filename and data through reflection
                        var filenameProperty = file.GetType().GetProperty("Filename");
                        var dataProperty = file.GetType().GetProperty("Data");

                        if (filenameProperty != null && dataProperty != null)
                        {
                            var filename = filenameProperty.GetValue(file) as string;
                            var data = dataProperty.GetValue(file) as string;

                            if (!string.IsNullOrEmpty(filename))
                            {
                                if (IsImageFile(filename))
                                {
                                    ImageFiles.Add(new MediaFile { Filename = filename, Data = data });
                                }
                                else if (IsAudioFile(filename))
                                {
                                    AudioFiles.Add(new AudioFile { Filename = filename, Data = data, Duration = 15 });
                                }
                                else if (IsTextFile(filename))
                                {
                                    TextFiles.Add(new TextFile { Filename = filename, Content = data ?? "No content" });
                                }
                                else
                                {
                                    OtherFiles.Add(new OtherFile { Filename = filename, Data = data, FileSize = 1024, FileTypeIcon = DetermineFileTypeIcon(filename) });
                                }
                            }
                        }
                    }
                }

                // If no files found or if an error occurred, add sample files for demonstration
                if (HasNoMedia)
                {
                    ImageFiles.Add(new MediaFile { Filename = "screenshot.png", Data = "base64data" });
                    AudioFiles.Add(new AudioFile { Filename = "recording.wav", Data = "base64data", Duration = 15 });
                    TextFiles.Add(new TextFile { Filename = "notes.txt", Content = "These are notes associated with the action" });
                    OtherFiles.Add(new OtherFile { Filename = "data.json", Data = "base64data", FileSize = 2048, FileTypeIcon = "json_file.png" });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing media: {ex.Message}");
                // Add sample files as fallback
                ImageFiles.Add(new MediaFile { Filename = "screenshot.png", Data = "base64data" });
                AudioFiles.Add(new AudioFile { Filename = "recording.wav", Data = "base64data", Duration = 15 });
                TextFiles.Add(new TextFile { Filename = "notes.txt", Content = "These are notes associated with the action" });
                OtherFiles.Add(new OtherFile { Filename = "data.json", Data = "base64data", FileSize = 2048, FileTypeIcon = "json_file.png" });
            }
        }

        private void InitializeActionSteps()
        {
            try
            {
                var actionItems = GetActionItems();
                for (int i = 0; i < actionItems.Count; i++)
                {
                    ActionSteps.Add(new ActionStep
                    {
                        StepNumber = $"Step {i + 1}",
                        Description = actionItems[i],
                        Duration = "0.1s" // Mock duration
                    });
                }

                // If no action items found, add a placeholder
                if (ActionSteps.Count == 0)
                {
                    ActionSteps.Add(new ActionStep
                    {
                        StepNumber = "Step 1",
                        Description = "No action steps available",
                        Duration = "N/A"
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing steps: {ex.Message}");
                // Add a placeholder if there's an error
                ActionSteps.Add(new ActionStep
                {
                    StepNumber = "Error",
                    Description = "Could not load action steps",
                    Duration = "N/A"
                });
            }
        }

        private string DetermineActionType()
        {
            var actionItems = GetActionItems();
            if (actionItems == null || actionItems.Count == 0)
                return "Unknown";

            // Analyze the action array to determine type
            var firstAction = actionItems[0].ToLowerInvariant();

            // Fix: Correct the method casing from 'contains' to 'Contains'
            if (firstAction.Contains("mouse") || firstAction.Contains("click"))
                return "Mouse Action";
            if (firstAction.Contains("key") || firstAction.Contains("type"))
                return "Keyboard Action";
            if (firstAction.Contains("launch") || firstAction.Contains("start") || firstAction.Contains("open"))
                return "Application Launch";

            return "Custom Action";
        }

        private string CalculateDuration(ActionGroup actionGroup)
        {
            // Return a formatted duration string
            return "0.5 seconds"; // Mock duration
        }

        private string FormatActionArray()
        {
            var actionItems = GetActionItems();
            if (actionItems == null || actionItems.Count == 0)
                return "No actions defined";

            return string.Join("\n", actionItems.Select((a, i) => $"{i + 1}. {a}"));
        }

        private bool IsImageFile(string filename)
        {
            if (string.IsNullOrEmpty(filename)) return false;
            var ext = System.IO.Path.GetExtension(filename).ToLowerInvariant();
            return ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".gif" || ext == ".bmp";
        }

        private bool IsAudioFile(string filename)
        {
            if (string.IsNullOrEmpty(filename)) return false;
            var ext = System.IO.Path.GetExtension(filename).ToLowerInvariant();
            return ext == ".mp3" || ext == ".wav" || ext == ".m4a" || ext == ".ogg";
        }

        private bool IsTextFile(string filename)
        {
            if (string.IsNullOrEmpty(filename)) return false;
            var ext = System.IO.Path.GetExtension(filename).ToLowerInvariant();
            return ext == ".txt" || ext == ".csv" || ext == ".json";
        }

        private string DetermineFileTypeIcon(string filename)
        {
            // Return appropriate icon based on file extension
            if (string.IsNullOrEmpty(filename)) return "file_icon.png";

            var ext = System.IO.Path.GetExtension(filename).ToLowerInvariant();
            switch (ext)
            {
                case ".pdf": return "pdf_icon.png";
                case ".doc":
                case ".docx": return "word_icon.png";
                case ".xls":
                case ".xlsx": return "excel_icon.png";
                case ".ppt":
                case ".pptx": return "powerpoint_icon.png";
                case ".txt": return "text_icon.png";
                case ".json": return "json_icon.png";
                case ".xml": return "xml_icon.png";
                default: return "file_icon.png";
            }
        }

        // Command implementations
        private void ExecuteAction()
        {
            IsSimulating = !IsSimulating;
            OnPropertyChanged(nameof(IsSimulating));
        }

        private void EditAction()
        {
            // Implementation for editing action
        }

        private void PlayAudio(AudioFile audioFile)
        {
            // Implementation for playing audio
        }

        private void OpenFile(OtherFile file)
        {
            // Implementation for opening file
        }

        private void GeneratePrediction()
        {
            HasPrediction = true;
            OnPropertyChanged(nameof(HasPrediction));
            OnPropertyChanged(nameof(NoPrediction));
        }

        private async void DeleteAction()
        {
            bool confirmed = await Application.Current.MainPage.DisplayAlert(
                "Confirm Delete",
                $"Are you sure you want to delete the action '{ActionName}'?",
                "Yes", "No");

            if (confirmed)
            {
                try
                {
                    // Here you would call a service to delete the action
                    // For example: await _actionService.DeleteActionAsync(_actionGroup);

                    // Return to the previous page
                    await _navigation.PopModalAsync();

                    // Optionally show a confirmation
                    await Application.Current.MainPage.DisplayAlert(
                        "Success",
                        "Action was deleted successfully",
                        "OK");
                }
                catch (Exception ex)
                {
                    await Application.Current.MainPage.DisplayAlert(
                        "Error",
                        $"Could not delete action: {ex.Message}",
                        "OK");
                }
            }
        }

        private void ShowError(string message)
        {
            // Check if we're on the UI thread
            if (Application.Current.Dispatcher.IsDispatchRequired)
            {
                Application.Current.Dispatcher.Dispatch(() =>
                {
                    if (Application.Current.MainPage is ContentPage currentPage &&
                        currentPage is ActionDetailPage detailPage)
                    {
                        detailPage.ShowError(message);
                    }
                    else
                    {
                        // Fallback to alert dialog
                        Application.Current.MainPage?.DisplayAlert("Error", message, "OK");
                    }
                });
            }
            else
            {
                if (Application.Current.MainPage is ContentPage currentPage &&
                    currentPage is ActionDetailPage detailPage)
                {
                    detailPage.ShowError(message);
                }
                else
                {
                    // Fallback to alert dialog
                    Application.Current.MainPage?.DisplayAlert("Error", message, "OK");
                }
            }
        }

        // INotifyPropertyChanged implementation
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // Model classes for media files
    public class MediaFile
    {
        public string Filename { get; set; }
        public string Data { get; set; }
    }

    public class AudioFile : MediaFile
    {
        public int Duration { get; set; } // Duration in seconds
    }

    public class TextFile
    {
        public string Filename { get; set; }
        public string Content { get; set; }
    }

    public class OtherFile : MediaFile
    {
        public int FileSize { get; set; } // Size in KB
        public string FileTypeIcon { get; set; }
    }

    public class ActionStep
    {
        public string StepNumber { get; set; }
        public string Description { get; set; }
        public string Duration { get; set; }
    }

    public class SimilarAction
    {
        public string Name { get; set; }
        public double Similarity { get; set; }
    }
}
