using System;
using System.Collections.Generic; // Added for List
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq; // Added for LINQ
using System.Runtime.CompilerServices;
using CSimple.Models; // Ensure Models namespace is included for NodeType
using Microsoft.Maui.Graphics; // For PointF

namespace CSimple.ViewModels
{
    public class NodeViewModel : INotifyPropertyChanged
    {
        private string _id;
        private string _name;
        private NodeType _type;
        private bool _isSelected;
        private PointF _position;
        private SizeF _size;
        private string _dataType;
        private string _selectedEnsembleMethod;
        private ObservableCollection<string> _availableEnsembleMethods = new ObservableCollection<string>();
        private string _classification; // Added for text model classification
        private string _modelPath; // Added for model path storage
        private string _originalName; // Added to store name before classification suffix

        public string Id { get; }
        public string Name { get; set; } // Allow setting name if needed
        public NodeType Type { get; }
        public string OriginalModelId { get; set; } // Store the original HuggingFace ID if applicable
        public string ModelPath // Added Property
        {
            get => _modelPath;
            set => SetProperty(ref _modelPath, value);
        }
        public string OriginalName // Added Property
        {
            get => _originalName;
            set => SetProperty(ref _originalName, value);
        }

        public PointF Position
        {
            get => _position;
            set => SetProperty(ref _position, value);
        }

        public SizeF Size
        {
            get => _size;
            set => SetProperty(ref _size, value);
        }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        // Property to determine the data type (e.g., "text", "image", "audio")
        public string DataType
        {
            get => _dataType;
            set
            {
                if (SetProperty(ref _dataType, value))
                {
                    UpdateAvailableEnsembleMethods(); // Update methods when data type changes
                    OnPropertyChanged(nameof(IsTextModel)); // Notify IsTextModel change
                }
            }
        }

        // Expose EnsembleInputCount for UI binding (e.g., border thickness)
        public int EnsembleInputCount { get; set; } = 0; // Initialize to 0

        // --- Ensemble Properties --- 
        public bool IsModel => Type == NodeType.Model;

        public ObservableCollection<string> AvailableEnsembleMethods
        {
            get => _availableEnsembleMethods;
            private set => SetProperty(ref _availableEnsembleMethods, value); // Private set for internal control
        }

        public string SelectedEnsembleMethod
        {
            get => _selectedEnsembleMethod;
            set => SetProperty(ref _selectedEnsembleMethod, value);
        }
        // --- End Ensemble Properties ---


        // --- Text Model Classification ---
        public bool IsTextModel => IsModel && (DataType?.Equals("text", StringComparison.OrdinalIgnoreCase) ?? false);

        public string Classification
        {
            get => _classification;
            set
            {
                if (SetProperty(ref _classification, value))
                {
                    // Update the node name when classification changes
                    UpdateNodeNameBasedOnClassification();
                }
            }
        }
        // --- End Text Model Classification ---


        // Modified Constructor to accept ID as string and handle OriginalName
        public NodeViewModel(string id, string name, NodeType type, PointF position, string dataType = "unknown", string originalModelId = null, string modelPath = null, string classification = null, string originalName = null)
        {
            Id = id ?? Guid.NewGuid().ToString(); // Use provided ID or generate new
            Type = type;
            Position = position;
            Size = new SizeF(120, 60); // Default size, adjust as needed
            DataType = dataType; // Set data type on creation
            OriginalModelId = originalModelId;
            ModelPath = modelPath; // Store model path
            _classification = classification; // Store initial classification without triggering update yet
            _originalName = originalName ?? name; // Store original name or use initial name
            Name = name; // Set initial name (might be updated immediately if classification exists)

            // Update name based on initial classification if provided
            UpdateNodeNameBasedOnClassification();

            // Initial population of ensemble methods
            UpdateAvailableEnsembleMethods();
        }

        // Helper method to update the node's display name
        private void UpdateNodeNameBasedOnClassification()
        {
            if (IsTextModel && !string.IsNullOrEmpty(Classification) && Classification != "None")
            {
                // Use OriginalName if available, otherwise use current Name before suffix
                string baseName = !string.IsNullOrEmpty(OriginalName) ? OriginalName : Name.Split(" [")[0];
                Name = $"{baseName} [{Classification}]";
            }
            else
            {
                // Revert to original name if classification is removed or node is not a text model
                Name = OriginalName ?? Name.Split(" [")[0]; // Fallback if OriginalName is somehow null
            }
            OnPropertyChanged(nameof(Name)); // Notify that Name has changed
        }


        private void UpdateAvailableEnsembleMethods()
        {
            AvailableEnsembleMethods.Clear();
            if (!IsModel) return; // Only models have ensemble methods

            var methods = new List<string>();
            string dataTypeLower = DataType?.ToLowerInvariant() ?? "unknown";

            switch (dataTypeLower)
            {
                case "text":
                    methods.Add("Averaging (Logits)");
                    methods.Add("Majority Voting");
                    methods.Add("Weighted Averaging");
                    methods.Add("Stacking (Meta-Learner)");
                    methods.Add("Concatenation (Features)");
                    break;
                case "image":
                    methods.Add("Averaging (Predictions)");
                    methods.Add("Majority Voting");
                    methods.Add("Weighted Averaging");
                    methods.Add("Stacking (Meta-Learner)");
                    methods.Add("Feature Fusion (Late)");
                    methods.Add("Non-Maximum Suppression (Detection)");
                    break;
                case "audio":
                    methods.Add("Averaging (Features)");
                    methods.Add("Majority Voting");
                    methods.Add("Weighted Averaging");
                    methods.Add("Stacking (Meta-Learner)");
                    methods.Add("Feature Fusion (Intermediate)");
                    break;
                default: // Multimodal or Unknown - Offer generic options
                    methods.Add("Averaging");
                    methods.Add("Majority Voting");
                    methods.Add("Weighted Averaging");
                    methods.Add("Stacking");
                    break;
            }

            foreach (var method in methods.OrderBy(m => m))
            {
                AvailableEnsembleMethods.Add(method);
            }

            // Set a default if none is selected or the previous one is invalid
            if (string.IsNullOrEmpty(SelectedEnsembleMethod) || !AvailableEnsembleMethods.Contains(SelectedEnsembleMethod))
            {
                SelectedEnsembleMethod = AvailableEnsembleMethods.FirstOrDefault();
            }
            else
            {
                // Ensure property changed is raised even if the list content changes but selection remains
                OnPropertyChanged(nameof(SelectedEnsembleMethod));
            }
            OnPropertyChanged(nameof(AvailableEnsembleMethods)); // Notify collection changed
        }


        // INotifyPropertyChanged implementation
        public event PropertyChangedEventHandler PropertyChanged;
        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(storage, value)) return false;
            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public (string Type, string Value) GetStepContent(int step) // step is 1-based
        {
            Debug.WriteLine($"[NodeViewModel.GetStepContent] Node '{Name}' (Type: {Type}, DataType: {DataType}), Requested Step: {step} (1-based), ActionSteps.Count: {ActionSteps.Count}");

            if (ActionSteps == null)
            {
                Debug.WriteLine($"[NodeViewModel.GetStepContent] ActionSteps list is null. Returning null content.");
                return (null, null);
            }

            if (Type != NodeType.Input)
            {
                Debug.WriteLine($"[NodeViewModel.GetStepContent] Condition not met: Node is not of Input type (Type: {Type}). Returning null content.");
                return (null, null);
            }

            if (step <= 0)
            {
                Debug.WriteLine($"[NodeViewModel.GetStepContent] Condition not met: Requested step {step} (1-based) is not a positive integer. Returning null content.");
                return (null, null);
            }

            if (ActionSteps.Count == 0)
            {
                Debug.WriteLine($"[NodeViewModel.GetStepContent] ActionSteps is empty. Cannot retrieve content for step {step}. Returning null content.");
                return (null, null);
            }

            if (step > ActionSteps.Count)
            {
                Debug.WriteLine($"[NodeViewModel.GetStepContent] Condition not met: Requested step {step} (1-based) is out of bounds for this node's ActionSteps (Count: {ActionSteps.Count}). Returning null content.");
                return (null, null);
            }

            // Adjust step to be 0-indexed for list access
            var stepData = ActionSteps[step - 1];
            Debug.WriteLine($"[NodeViewModel.GetStepContent] Accessing ActionSteps[{step - 1}]. Step Data: Type='{stepData.Type}', Supposed File/Content Value='{stepData.Value}'");

            // Ensure the stepData.Type matches the node's DataType for relevance
            // This check might be redundant if ActionSteps was already populated with matching types.
            if (!string.Equals(stepData.Type, this.DataType, StringComparison.OrdinalIgnoreCase))
            {
                Debug.WriteLine($"[NodeViewModel.GetStepContent] Warning: Step data type '{stepData.Type}' from ActionSteps[{step - 1}] does not match node's DataType '{this.DataType}'. Returning content as is.");
            }

            // Return based on the Type field within the ActionStep tuple
            // This assumes ActionSteps[i].Type correctly identifies "Text", "Image", "Audio"
            // And ActionSteps[i].Value is the corresponding content.
            // The switch statement here is a bit redundant if stepData.Type is already what we need.
            // Let's simplify to directly use stepData.Type and stepData.Value if they are correctly populated.

            // We should ensure that the ActionSteps are populated correctly for this node's DataType.
            // For example, an "Image" node should only have "Image" type steps.
            // The current logic returns the stepData as is.

            string imageFileName = null;
            if (DataType?.ToLower() == "image")
            {
                // Attempt to find the corresponding image file name based on timestamp
                imageFileName = FindClosestImageFile(stepData.Value, stepData.Type);
                if (!string.IsNullOrEmpty(imageFileName))
                {
                    Debug.WriteLine($"[NodeViewModel.GetStepContent] Found corresponding image file: {imageFileName}");
                }
                else
                {
                    Debug.WriteLine("[NodeViewModel.GetStepContent] No corresponding image file found.");
                }
            }

            Debug.WriteLine($"[NodeViewModel.GetStepContent] Returning for UI: Type='{stepData.Type}', Supposed File/Content Value='{stepData.Value}'");
            return (stepData.Type, stepData.Value);
        }

        public string FindClosestImageFile(string actionDescription, string actionType)
        {
            if (string.IsNullOrEmpty(actionDescription))
            {
                Debug.WriteLine("[NodeViewModel.FindClosestImageFile] Action description is null or empty.");
                return null;
            }

            string directoryPath = "";
            string filePattern = "";

            if (actionType == "image")
            {
                directoryPath = @"C:\Users\tanne\Documents\CSimple\Resources\WebcamImages\";
                filePattern = "WebcamImage_*.jpg";
            }
            else
            {
                directoryPath = @"C:\Users\tanne\Documents\CSimple\Resources\Screenshots\";
                filePattern = "ScreenCapture_*.png";
            }

            if (!Directory.Exists(directoryPath))
            {
                Debug.WriteLine($"[NodeViewModel.FindClosestImageFile] Directory does not exist: {directoryPath}");
                return null;
            }

            string closestFile = null;
            DateTime closestFileTime = DateTime.MinValue;

            try
            {
                string[] files = Directory.GetFiles(directoryPath, filePattern);

                foreach (string filePath in files)
                {
                    string fileName = Path.GetFileNameWithoutExtension(filePath);
                    int lastIndexOfUnderscore = fileName.LastIndexOf('_');

                    // Ensure fileName is long enough and contains at least one underscore
                    if (lastIndexOfUnderscore > 0 && fileName.Length > lastIndexOfUnderscore + 1)
                    {
                        string timestampPart = fileName.Substring(lastIndexOfUnderscore + 1); // Extract timestamp part

                        // Remove the DISPLAY suffix if present
                        int displayIndex = timestampPart.IndexOf("DISPLAY");
                        if (displayIndex > 0)
                        {
                            timestampPart = timestampPart.Substring(0, displayIndex);
                        }

                        // Try parsing with different timestamp lengths
                        DateTime fileTime = DateTime.MinValue;
                        bool parsed = false;

                        if (timestampPart.Length >= 15)
                        {
                            parsed = DateTime.TryParseExact(timestampPart.Substring(0, 15), "yyyyMMdd_HHmmss_", null, System.Globalization.DateTimeStyles.None, out fileTime);
                        }
                        if (!parsed && timestampPart.Length >= 14)
                        {
                            parsed = DateTime.TryParseExact(timestampPart.Substring(0, 14), "yyyyMMdd_HHmmss", null, System.Globalization.DateTimeStyles.None, out fileTime);
                        }
                        if (!parsed && timestampPart.Length >= 6)
                        {
                            parsed = DateTime.TryParseExact(timestampPart, "HHmmss", null, System.Globalization.DateTimeStyles.None, out fileTime);
                        }

                        if (parsed)
                        {
                            // Check if the file time is more recent than the current closest file
                            if (fileTime > closestFileTime)
                            {
                                closestFileTime = fileTime;
                                closestFile = filePath;
                            }
                        }
                        else
                        {
                            Debug.WriteLine($"[NodeViewModel.FindClosestImageFile] Could not parse timestamp: {timestampPart}");
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"[NodeViewModel.FindClosestImageFile] Filename is too short or does not contain underscore: {fileName}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NodeViewModel.FindClosestImageFile] Error finding closest image file: {ex.Message}");
                return null;
            }

            if (closestFile != null)
            {
                Debug.WriteLine($"[NodeViewModel.FindClosestImageFile] Found closest image file: {closestFile}");
            }
            else
            {
                Debug.WriteLine("[NodeViewModel.FindClosestImageFile] No suitable image file found.");
            }

            return closestFile;
        }


        public List<(string Type, string Value)> ActionSteps { get; set; } = new();

        // Add this property to store the full path to the audio file
        public string AudioFilePath { get; set; }

        // Method to extract a segment of the audio file
        public string GetAudioSegment(DateTime startTime, DateTime endTime)
        {
            if (string.IsNullOrEmpty(AudioFilePath))
            {
                Debug.WriteLine("[NodeViewModel.GetAudioSegment] Audio file path is not set. Attempting to find closest audio file by timestamp.");
                // Implement logic to find the audio file with the closest timestamp
                string audioFilePath = FindClosestAudioFile(startTime);

                if (string.IsNullOrEmpty(audioFilePath))
                {
                    Debug.WriteLine("[NodeViewModel.GetAudioSegment] No audio file found with a timestamp close to the action review timestamp.");
                    return null;
                }

                // Simulate audio segment extraction from the found file
                string segmentPath2 = SimulateAudioSegmentExtraction(audioFilePath, startTime, endTime);
                return segmentPath2;
            }

            if (Type != NodeType.Input || DataType?.ToLower() != "audio")
            {
                Debug.WriteLine("[NodeViewModel.GetAudioSegment] Node is not an audio input node.");
                return null;
            }

            if (startTime == DateTime.MinValue || endTime == DateTime.MinValue)
            {
                Debug.WriteLine("[NodeViewModel.GetAudioSegment] Invalid start or end time.");
                return null;
            }

            // Simulate audio segment extraction from the found file
            string segmentPath = SimulateAudioSegmentExtraction(AudioFilePath, startTime, endTime);
            return segmentPath;
        }

        private string FindClosestAudioFile(DateTime targetTime)
        {
            string directoryPath = @"C:\Users\tanne\Documents\CSimple\Resources\PCAudio\";

            if (!Directory.Exists(directoryPath))
            {
                Debug.WriteLine($"[NodeViewModel.FindClosestAudioFile] Directory does not exist: {directoryPath}");
                return null;
            }

            string closestFile = null;
            TimeSpan closestDifference = TimeSpan.MaxValue;

            try
            {
                string[] files = Directory.GetFiles(directoryPath, "PCAudio_*.wav");

                foreach (string filePath in files)
                {
                    string fileName = Path.GetFileNameWithoutExtension(filePath);
                    string timestampPart = fileName.Substring("PCAudio_".Length); // Extract timestamp part

                    if (DateTime.TryParseExact(timestampPart, "yyyyMMdd_HHmmss", null, System.Globalization.DateTimeStyles.None, out DateTime fileTime))
                    {
                        TimeSpan difference = TimeSpan.FromTicks(Math.Abs(fileTime.Ticks - targetTime.Ticks));

                        if (difference < closestDifference)
                        {
                            closestDifference = difference;
                            closestFile = filePath;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NodeViewModel.FindClosestAudioFile] Error finding closest audio file: {ex.Message}");
                return null;
            }

            if (closestFile != null)
            {
                Debug.WriteLine($"[NodeViewModel.FindClosestAudioFile] Found closest audio file: {closestFile}, difference: {closestDifference}");
            }
            else
            {
                Debug.WriteLine("[NodeViewModel.FindClosestAudioFile] No suitable audio file found.");
            }

            return closestFile;
        }

        // Simulate audio segment extraction
        private string SimulateAudioSegmentExtraction(string fullPath, DateTime startTime, DateTime endTime)
        {
            // This is a placeholder; replace with actual audio processing code
            // For example, use NAudio library to read and write audio segments
            // Ensure NAudio is installed: Install-Package NAudio

            // Simulate creating a segment file path
            string segmentFileName = $"Segment_{startTime.Ticks}_{endTime.Ticks}.wav";
            string segmentPath = Path.Combine(Path.GetDirectoryName(fullPath), segmentFileName);

            Debug.WriteLine($"[NodeViewModel.SimulateAudioSegmentExtraction] Simulated audio segment extraction from {fullPath} to {segmentPath} (Start: {startTime}, End: {endTime})");
            return segmentPath;
        }
    }
}
