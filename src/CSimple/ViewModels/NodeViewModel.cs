using System;
using System.Collections.Generic; // Added for List
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq; // Added for LINQ
using System.Runtime.CompilerServices;
using System.Text; // Added for StringBuilder
using CSimple.Models; // Ensure Models namespace is included for NodeType
using Microsoft.Maui.Graphics; // For PointF

namespace CSimple.ViewModels
{
    /// <summary>
    /// Represents the execution state of a node during pipeline execution
    /// </summary>
    public enum ExecutionState
    {
        /// <summary>
        /// Node is waiting to be executed (default state)
        /// </summary>
        Pending,

        /// <summary>
        /// Node is currently being executed
        /// </summary>
        Running,

        /// <summary>
        /// Node has completed execution
        /// </summary>
        Completed
    }
    public class NodeViewModel : INotifyPropertyChanged
    {
        // Static property to hold current ActionItems for access across nodes
        public static List<ActionItem> CurrentActionItems { get; set; } = new List<ActionItem>();

        private bool _isSelected;
        private PointF _position;
        private SizeF _size;
        private string _dataType;
        private string _selectedEnsembleMethod;
        private ObservableCollection<string> _availableEnsembleMethods = new ObservableCollection<string>();
        private string _classification; // Added for text model classification
        private string _goalText = ""; // Added for Goal classification text
        private string _planText = ""; // Added for Plan classification text  
        private string _actionText = ""; // Added for Action classification text
        private string _appendText = ""; // Added for general text appending to all text model nodes
        private string _textToAudioPrompt = ""; // Added for text-to-audio prompt functionality
        private string _modelPath; // Added for model path storage
        private string _originalName; // Added to store name before classification suffix
        private string _saveFilePath; // Added for file node save path
        private ExecutionState _executionState = ExecutionState.Pending; // Added for execution state tracking
        private bool _readAloudOnCompletion = false; // Added for TTS toggle functionality

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

        public string SaveFilePath // Added Property for file node routing
        {
            get => _saveFilePath;
            set => SetProperty(ref _saveFilePath, value);
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

        /// <summary>
        /// Current execution state of the node during pipeline execution
        /// </summary>
        public ExecutionState ExecutionState
        {
            get => _executionState;
            set
            {
                if (SetProperty(ref _executionState, value))
                {
                    OnPropertyChanged(nameof(ExecutionBorderColor));
                }
            }
        }

        /// <summary>
        /// Gets the border color based on execution state for visual feedback during pipeline execution
        /// </summary>
        public Color ExecutionBorderColor
        {
            get
            {
                return ExecutionState switch
                {
                    ExecutionState.Pending => Colors.Gray,      // Default state - gray border
                    ExecutionState.Running => Colors.Orange,    // Currently executing - orange border
                    ExecutionState.Completed => Colors.Green,   // Completed - green border
                    _ => Colors.Gray
                };
            }
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

        // --- File Node Properties ---
        public bool IsFileNode => Type == NodeType.File;
        public string SaveFileDisplayPath => string.IsNullOrEmpty(SaveFilePath) ? "No save file" : Path.GetFileName(SaveFilePath);

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

        // Classification-specific text properties
        public string GoalText
        {
            get => _goalText;
            set => SetProperty(ref _goalText, value);
        }

        public string PlanText
        {
            get => _planText;
            set => SetProperty(ref _planText, value);
        }

        public string ActionText
        {
            get => _actionText;
            set => SetProperty(ref _actionText, value);
        }

        public string AppendText
        {
            get => _appendText;
            set => SetProperty(ref _appendText, value);
        }

        public string TextToAudioPrompt
        {
            get => _textToAudioPrompt;
            set => SetProperty(ref _textToAudioPrompt, value);
        }

        /// <summary>
        /// Toggle for automatically reading text content aloud when the node completes execution
        /// </summary>
        public bool ReadAloudOnCompletion
        {
            get => _readAloudOnCompletion;
            set => SetProperty(ref _readAloudOnCompletion, value);
        }

        // Helper property to get the current classification text
        public string CurrentClassificationText
        {
            get
            {
                return Classification switch
                {
                    "Goal" => GoalText,
                    "Plan" => PlanText,
                    "Action" => ActionText,
                    _ => ""
                };
            }
        }
        // --- End Text Model Classification ---


        // Modified Constructor to accept ID as string and handle OriginalName and SaveFilePath
        public NodeViewModel(string id, string name, NodeType type, PointF position, string dataType = "unknown", string originalModelId = null, string modelPath = null, string classification = null, string originalName = null, string saveFilePath = null, string goalText = "", string planText = "", string actionText = "", string appendText = "", string textToAudioPrompt = "", bool readAloudOnCompletion = false)
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
            _saveFilePath = saveFilePath; // Store save file path for file nodes
            _goalText = goalText ?? ""; // Initialize goal text
            _planText = planText ?? ""; // Initialize plan text
            _actionText = actionText ?? ""; // Initialize action text
            _appendText = appendText ?? ""; // Initialize append text
            _textToAudioPrompt = textToAudioPrompt ?? ""; // Initialize text-to-audio prompt
            _readAloudOnCompletion = readAloudOnCompletion; // Initialize read-aloud toggle
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
                // Select the best default method based on data type
                string defaultMethod = dataTypeLower switch
                {
                    "text" => "Averaging (Logits)", // Best for text models
                    "image" => "Averaging (Predictions)", // Best for image models  
                    "audio" => "Averaging (Features)", // Best for audio models
                    _ => "Averaging" // Generic fallback
                };

                // Use the default if available, otherwise use the first one
                SelectedEnsembleMethod = AvailableEnsembleMethods.Contains(defaultMethod)
                    ? defaultMethod
                    : AvailableEnsembleMethods.FirstOrDefault();
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
            return GetStepContent(step, null);
        }

        public (string Type, string Value) GetStepContent(int step, DateTime? actionItemTimestamp) // step is 1-based
        {
            // Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.GetStepContent] Node '{Name}' (Type: {Type}, DataType: {DataType}), Requested Step: {step} (1-based), ActionSteps.Count: {ActionSteps.Count}, ActionItem Timestamp: {actionItemTimestamp?.ToString("yyyy-MM-dd HH:mm:ss.fff") ?? "None"}");

            if (ActionSteps == null)
            {
                // Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.GetStepContent] ActionSteps list is null. Returning null content.");
                return (null, null);
            }

            // Allow Input, Model, and File nodes to have step content
            if (Type != NodeType.Input && Type != NodeType.Model && Type != NodeType.File)
            {
                // Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.GetStepContent] Condition not met: Node is not of Input, Model, or File type (Type: {Type}). Returning null content.");
                return (null, null);
            }

            // Special handling for File nodes - read content from file
            if (Type == NodeType.File)
            {
                return GetFileNodeContent();
            }

            if (step <= 0)
            {
                // Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.GetStepContent] Condition not met: Requested step {step} (1-based) is not a positive integer. Returning null content.");
                return (null, null);
            }

            if (ActionSteps.Count == 0)
            {
                // Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.GetStepContent] ActionSteps is empty. Cannot retrieve content for step {step}. Returning null content.");
                return (null, null);
            }

            if (step > ActionSteps.Count)
            {
                // Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.GetStepContent] Condition not met: Requested step {step} (1-based) is out of bounds for this node's ActionSteps (Count: {ActionSteps.Count}). Returning null content.");
                return (null, null);
            }

            // Adjust step to be 0-indexed for list access
            var stepData = ActionSteps[step - 1];
            // Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.GetStepContent] Accessing ActionSteps[{step - 1}]. Step Data: Type='{stepData.Type}', Supposed File/Content Value='{stepData.Value?.Substring(0, Math.Min(200, stepData.Value?.Length ?? 0))}...'");

            // Add debugging to show all ActionSteps for comparison - but only for the first few times to avoid spam
            if (step <= 3) // Only log this for first few steps to avoid spam
            {
                // Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.GetStepContent] DEBUG: All ActionSteps for node '{Name}':");
                for (int i = 0; i < Math.Min(5, ActionSteps.Count); i++) // Only show first 5 steps
                {
                    var debugStep = ActionSteps[i];
                    // Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}]   ActionSteps[{i}]: Type='{debugStep.Type}', Value='{debugStep.Value?.Substring(0, Math.Min(100, debugStep.Value?.Length ?? 0))}...'");
                }
            }

            // Ensure the stepData.Type matches the node's DataType for relevance
            // This check might be redundant if ActionSteps was already populated with matching types.
            if (!string.Equals(stepData.Type, this.DataType, StringComparison.OrdinalIgnoreCase))
            {
                // Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.GetStepContent] Warning: Step data type '{stepData.Type}' from ActionSteps[{step - 1}] does not match node's DataType '{this.DataType}'. Returning content as is.");
            }

            // For Model nodes, if we have stored output (generated content), return it with cleaning applied
            // without trying to find image/audio files, as the stored content is the actual generated output
            // This prevents issues where generated text like "Image Caption: ..." gets parsed as timestamp data
            if (Type == NodeType.Model && !string.IsNullOrEmpty(stepData.Value))
            {
                // Use GetStepOutput to ensure consistent cleaning logic is applied
                var cleanedOutput = GetStepOutput(step);
                // Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.GetStepContent] Model node has stored output, returning cleaned version: Type='{cleanedOutput.Type}', Value length={cleanedOutput.Value?.Length ?? 0}");
                return cleanedOutput;
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
                // FIXED: Use the ActionItem timestamp for precise file correlation with multi-monitor support
                var imageFiles = FindClosestImageFiles(stepData.Value, stepData.Type, actionItemTimestamp);
                if (imageFiles?.Count > 0)
                {
                    // Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.GetStepContent] Found {imageFiles.Count} corresponding image files");

                    if (imageFiles.Count == 1)
                    {
                        // Single image - return as before for backward compatibility
                        imageFileName = imageFiles[0];
                        // Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.GetStepContent] Returning single image file: {imageFileName}");
                        return (stepData.Type, imageFileName);
                    }
                    else
                    {
                        // Multiple images (multi-monitor) - return as semicolon-separated list
                        string multiImagePaths = string.Join(";", imageFiles);
                        // Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.GetStepContent] Returning multiple image files: {multiImagePaths}");
                        return (stepData.Type, multiImagePaths);
                    }
                }
                else
                {
                    // Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.GetStepContent] No corresponding image files found.");
                }
            }

            // Handle audio nodes similarly to image nodes - get the actual audio file path
            if (DataType?.ToLower() == "audio")
            {
                // FIXED: Use the ActionItem timestamp for precise file correlation
                try
                {
                    string audioSegmentPath = null;
                    if (actionItemTimestamp.HasValue)
                    {
                        // Use the ActionItem timestamp for precise audio file correlation
                        audioSegmentPath = FindClosestAudioFile(actionItemTimestamp.Value);
                    }
                    else
                    {
                        // Fallback: use current time to find the most recent audio files
                        audioSegmentPath = FindClosestAudioFile(DateTime.Now);
                    }

                    if (!string.IsNullOrEmpty(audioSegmentPath))
                    {
                        // Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.GetStepContent] Found corresponding audio file: {audioSegmentPath}");
                        // Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.GetStepContent] Returning audio file path for model input: {audioSegmentPath}");
                        return (stepData.Type, audioSegmentPath); // Return the actual audio file path for model execution
                    }
                    else
                    {
                        // Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.GetStepContent] No corresponding audio file found.");
                    }
                }
                catch (Exception)
                {
                    // Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.GetStepContent] Error getting audio segment: {ex.Message}");
                }
            }

            // Handle text nodes - extract meaningful content from ActionItem data
            if (DataType?.ToLower() == "text")
            {
                string textContent = GetTextContent(stepData, actionItemTimestamp);
                if (!string.IsNullOrEmpty(textContent))
                {
                    // Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.GetStepContent] Generated text content: {textContent}");
                    return (stepData.Type, textContent);
                }
            }

            // Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.GetStepContent] Returning for UI: Type='{stepData.Type}', Supposed File/Content Value='{stepData.Value}'");
            return (stepData.Type, stepData.Value);
        }

        public string FindClosestImageFile(string actionDescription, string actionType)
        {
            return FindClosestImageFile(actionDescription, actionType, null);
        }

        public string FindClosestImageFile(string actionDescription, string actionType, DateTime? targetTimestamp)
        {
            var imageFiles = FindClosestImageFiles(actionDescription, actionType, targetTimestamp);
            return imageFiles?.FirstOrDefault(); // Return the first image for backward compatibility
        }

        /// <summary>
        /// Finds all closest image files for multi-monitor screenshots
        /// </summary>
        public List<string> FindClosestImageFiles(string actionDescription, string actionType, DateTime? targetTimestamp)
        {
            if (string.IsNullOrEmpty(actionDescription) && !targetTimestamp.HasValue)
            {
                // Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.FindClosestImageFiles] Action description and target timestamp are both null or empty.");
                return new List<string>();
            }

            // If we have a target timestamp, use that for correlation. Otherwise, fall back to description parsing
            if (!targetTimestamp.HasValue)
            {
                // If the action description looks like generated text content (e.g., from a model),
                // don't try to parse it as a timestamp-based file search
                if (actionDescription.Contains("Caption:") ||
                    actionDescription.Contains("Generated:") ||
                    actionDescription.Contains("Output:") ||
                    actionDescription.Length > 100 ||
                    (!actionDescription.Contains("_") && !actionDescription.Contains(":")))
                {
                    // Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.FindClosestImageFiles] Action description appears to be generated content, not a timestamp: {actionDescription.Substring(0, Math.Min(50, actionDescription.Length))}...");
                    return new List<string>();
                }
            }

            string directoryPath = "";
            string filePattern = "";

            // FIXED: Determine folder based on the node's name rather than just actionType
            if (this.Name.Contains("Webcam") && this.Name.Contains("Image"))
            {
                // Webcam Image node should look in WebcamImages folder
                directoryPath = @"C:\Users\tanne\Documents\CSimple\Resources\WebcamImages\";
                filePattern = "WebcamImage_*.jpg";
            }
            else if (this.Name.Contains("Screen") && this.Name.Contains("Image"))
            {
                // Screen Image node should look in Screenshots folder
                directoryPath = @"C:\Users\tanne\Documents\CSimple\Resources\Screenshots\";
                filePattern = "ScreenCapture_*.png";
            }
            else if (actionType == "image")
            {
                // Fallback to WebcamImages for generic image requests
                directoryPath = @"C:\Users\tanne\Documents\CSimple\Resources\WebcamImages\";
                filePattern = "WebcamImage_*.jpg";
            }
            else
            {
                // Default fallback to Screenshots for non-image types
                directoryPath = @"C:\Users\tanne\Documents\CSimple\Resources\Screenshots\";
                filePattern = "ScreenCapture_*.png";
            }

            if (!Directory.Exists(directoryPath))
            {
                // Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.FindClosestImageFiles] Directory does not exist: {directoryPath}");
                return new List<string>();
            }

            var closestFiles = new List<string>();
            TimeSpan closestDifference = TimeSpan.MaxValue;
            DateTime closestDateTime = DateTime.MinValue;

            try
            {
                string[] files = Directory.GetFiles(directoryPath, filePattern);
                // Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.FindClosestImageFiles] Found {files.Length} files in {directoryPath}. Target timestamp: {targetTimestamp?.ToString("yyyy-MM-dd HH:mm:ss.fff") ?? "None"}");

                foreach (string filePath in files)
                {
                    string fileName = Path.GetFileNameWithoutExtension(filePath);
                    // Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.FindClosestImageFiles] Processing file: {fileName}");

                    // Handle screenshot file format: ScreenCapture_20250719_122427_898_.DISPLAY1
                    DateTime fileTime = DateTime.MinValue;
                    bool parsed = false;

                    if (fileName.StartsWith("ScreenCapture_"))
                    {
                        // Extract timestamp part from ScreenCapture files
                        // Format: ScreenCapture_20250719_122427_898_.DISPLAY1
                        string[] parts = fileName.Split('_');
                        if (parts.Length >= 4)
                        {
                            string datePart = parts[1]; // 20250719
                            string timePart = parts[2]; // 122427
                            string millisPart = parts[3]; // 898

                            string fullTimestamp = $"{datePart}_{timePart}";
                            if (millisPart.Length >= 3)
                            {
                                fullTimestamp += $"_{millisPart.Substring(0, 3)}";
                                parsed = DateTime.TryParseExact(fullTimestamp, "yyyyMMdd_HHmmss_fff", null, System.Globalization.DateTimeStyles.None, out fileTime);
                            }
                            if (!parsed)
                            {
                                parsed = DateTime.TryParseExact($"{datePart}_{timePart}", "yyyyMMdd_HHmmss", null, System.Globalization.DateTimeStyles.None, out fileTime);
                            }
                        }
                    }
                    else if (fileName.StartsWith("WebcamImage_"))
                    {
                        // Handle WebcamImage format: WebcamImage_20250719_122427
                        string[] parts = fileName.Split('_');
                        if (parts.Length >= 3)
                        {
                            string datePart = parts[1];
                            string timePart = parts[2];
                            parsed = DateTime.TryParseExact($"{datePart}_{timePart}", "yyyyMMdd_HHmmss", null, System.Globalization.DateTimeStyles.None, out fileTime);
                        }
                    }
                    else
                    {
                        // Fallback to original logic for other file formats
                        int lastIndexOfUnderscore = fileName.LastIndexOf('_');
                        if (lastIndexOfUnderscore > 0 && fileName.Length > lastIndexOfUnderscore + 1)
                        {
                            string timestampPart = fileName.Substring(lastIndexOfUnderscore + 1);

                            // Remove the DISPLAY suffix if present
                            int displayIndex = timestampPart.IndexOf("DISPLAY");
                            if (displayIndex > 0)
                            {
                                timestampPart = timestampPart.Substring(0, displayIndex);
                            }

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
                        }
                    }

                    if (parsed)
                    {
                        // Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.FindClosestImageFiles] Parsed file timestamp: {fileTime:yyyy-MM-dd HH:mm:ss.fff} (Local Time)");

                        if (targetTimestamp.HasValue)
                        {
                            // Convert UTC ActionItem timestamp to local time for comparison with file timestamps
                            DateTime localTargetTime = targetTimestamp.Value.Kind == DateTimeKind.Utc
                                ? targetTimestamp.Value.ToLocalTime()
                                : targetTimestamp.Value;

                            // Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.FindClosestImageFiles] Target timestamp: {targetTimestamp.Value:yyyy-MM-dd HH:mm:ss.fff} UTC -> {localTargetTime:yyyy-MM-dd HH:mm:ss.fff} Local");

                            // Find the file with the closest timestamp to the target
                            TimeSpan difference = TimeSpan.FromTicks(Math.Abs(fileTime.Ticks - localTargetTime.Ticks));

                            if (difference < closestDifference)
                            {
                                // Found a closer timestamp - start a new collection
                                closestDifference = difference;
                                closestDateTime = fileTime;
                                closestFiles.Clear();
                                closestFiles.Add(filePath);
                                // Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.FindClosestImageFiles] New closest timestamp {fileName}: FileTime={fileTime:yyyy-MM-dd HH:mm:ss.fff}, LocalTargetTime={localTargetTime:yyyy-MM-dd HH:mm:ss.fff}, Difference={difference.TotalMilliseconds}ms");
                            }
                            else if (difference == closestDifference && fileTime == closestDateTime)
                            {
                                // Same timestamp - add to collection (multi-monitor case)
                                closestFiles.Add(filePath);
                                // Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.FindClosestImageFiles] Same timestamp {fileName}: Adding to collection (multi-monitor)");
                            }
                        }
                        else
                        {
                            // Fallback: use most recent file if no target timestamp
                            TimeSpan difference = TimeSpan.FromTicks(Math.Abs(fileTime.Ticks - DateTime.Now.Ticks));
                            if (difference < closestDifference)
                            {
                                closestDifference = difference;
                                closestDateTime = fileTime;
                                closestFiles.Clear();
                                closestFiles.Add(filePath);
                            }
                            else if (difference == closestDifference && fileTime == closestDateTime)
                            {
                                closestFiles.Add(filePath);
                            }
                        }
                    }
                    else
                    {
                        // Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.FindClosestImageFiles] Could not parse timestamp from file: {fileName}");
                    }
                }
            }
            catch (Exception)
            {
                // Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.FindClosestImageFiles] Error finding closest image files: {ex.Message}");
                return new List<string>();
            }

            if (closestFiles.Count > 0)
            {
                // Sort the files by display number for consistent ordering
                closestFiles = closestFiles.OrderBy(f => f).ToList();
                // Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.FindClosestImageFiles] Found {closestFiles.Count} closest image files with difference: {closestDifference.TotalMilliseconds}ms");
                foreach (var file in closestFiles)
                {
                    // Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.FindClosestImageFiles]   - {Path.GetFileName(file)}");
                }
            }
            else
            {
                // Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.FindClosestImageFiles] No suitable image files found.");
            }

            return closestFiles;
        }


        public List<(string Type, string Value)> ActionSteps { get; set; } = new();

        // Add this property to store the full path to the audio file
        public string AudioFilePath { get; set; }

        // Method to extract a segment of the audio file
        public string GetAudioSegment(DateTime startTime, DateTime endTime)
        {
            if (string.IsNullOrEmpty(AudioFilePath))
            {
                // Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.GetAudioSegment] Audio file path is not set. Attempting to find closest audio file by timestamp.");
                // Implement logic to find the audio file with the closest timestamp
                string audioFilePath = FindClosestAudioFile(startTime);

                if (string.IsNullOrEmpty(audioFilePath))
                {
                    // Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.GetAudioSegment] No audio file found with a timestamp close to the action review timestamp.");
                    return null;
                }

                // Simulate audio segment extraction from the found file
                string segmentPath2 = SimulateAudioSegmentExtraction(audioFilePath, startTime, endTime);
                return segmentPath2;
            }

            if (Type != NodeType.Input || DataType?.ToLower() != "audio")
            {
                // Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.GetAudioSegment] Node is not an audio input node.");
                return null;
            }

            if (startTime == DateTime.MinValue || endTime == DateTime.MinValue)
            {
                // Handle the case where specific time ranges aren't provided
                // Just find the most recent audio file for this node type
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.GetAudioSegment] No specific time range provided, finding most recent audio file.");

                // Use FindClosestAudioFile with current time to get the most recent file
                string mostRecentFile = FindClosestAudioFile(DateTime.Now);
                if (!string.IsNullOrEmpty(mostRecentFile))
                {
                    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.GetAudioSegment] Found most recent audio file: {mostRecentFile}");
                    return mostRecentFile;
                }

                // If no specific file found, try SimulateAudioSegmentExtraction with a fallback directory
                string fallbackDirectoryPath = GetFallbackAudioDirectoryPath();
                if (!string.IsNullOrEmpty(fallbackDirectoryPath))
                {
                    // Create a dummy path for the directory so SimulateAudioSegmentExtraction can find files
                    string dummyPath = Path.Combine(fallbackDirectoryPath, "dummy.wav");
                    return SimulateAudioSegmentExtraction(dummyPath, startTime, endTime);
                }

                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.GetAudioSegment] No audio files found for node type.");
                return null;
            }

            // Simulate audio segment extraction from the found file
            string segmentPath = SimulateAudioSegmentExtraction(AudioFilePath, startTime, endTime);
            return segmentPath;
        }

        public string FindClosestAudioFile(DateTime targetTime)
        {
            string directoryPath = "";
            string filePattern = "";

            // FIXED: Determine audio folder based on the node's name
            if (this.Name.Contains("Webcam") && this.Name.Contains("Audio"))
            {
                // Webcam Audio node should look in WebcamAudio folder
                directoryPath = @"C:\Users\tanne\Documents\CSimple\Resources\WebcamAudio\";
                filePattern = "WebcamAudio_*.wav";
            }
            else if (this.Name.Contains("PC") && this.Name.Contains("Audio"))
            {
                // PC Audio node should look in PCAudio folder
                directoryPath = @"C:\Users\tanne\Documents\CSimple\Resources\PCAudio\";
                filePattern = "PCAudio_*.wav";
            }
            else
            {
                // Default fallback to PCAudio for generic audio requests
                directoryPath = @"C:\Users\tanne\Documents\CSimple\Resources\PCAudio\";
                filePattern = "PCAudio_*.wav";
            }

            if (!Directory.Exists(directoryPath))
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.FindClosestAudioFile] Directory does not exist: {directoryPath}");
                return null;
            }

            // Convert UTC timestamp to local time for comparison with file timestamps
            DateTime localTargetTime = targetTime.Kind == DateTimeKind.Utc
                ? targetTime.ToLocalTime()
                : targetTime;

            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.FindClosestAudioFile] Target timestamp: {targetTime:yyyy-MM-dd HH:mm:ss.fff} -> {localTargetTime:yyyy-MM-dd HH:mm:ss.fff} Local");

            string closestFile = null;
            TimeSpan closestDifference = TimeSpan.MaxValue;

            try
            {
                string[] files = Directory.GetFiles(directoryPath, filePattern);
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.FindClosestAudioFile] Found {files.Length} files in {directoryPath}.");

                // Create list with parsed timestamps for better analysis
                var filesWithTimestamps = new List<(string FilePath, DateTime FileTime, string FileName)>();

                foreach (string filePath in files)
                {
                    string fileName = Path.GetFileNameWithoutExtension(filePath);

                    // Extract timestamp part based on file pattern
                    string timestampPart = "";
                    if (fileName.StartsWith("WebcamAudio_"))
                    {
                        timestampPart = fileName.Substring("WebcamAudio_".Length);
                    }
                    else if (fileName.StartsWith("PCAudio_"))
                    {
                        timestampPart = fileName.Substring("PCAudio_".Length);
                    }
                    else
                    {
                        // Generic extraction - find timestamp pattern
                        var parts = fileName.Split('_');
                        if (parts.Length >= 3)
                        {
                            timestampPart = $"{parts[1]}_{parts[2]}";
                        }
                    }

                    if (DateTime.TryParseExact(timestampPart, "yyyyMMdd_HHmmss", null, System.Globalization.DateTimeStyles.None, out DateTime fileTime))
                    {
                        filesWithTimestamps.Add((filePath, fileTime, fileName));
                    }
                    else
                    {
                        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [FindClosestAudioFile] Could not parse timestamp from: {fileName} (timestamp part: {timestampPart})");
                    }
                }

                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [FindClosestAudioFile] Successfully parsed {filesWithTimestamps.Count} file timestamps");

                // If we have files from today (same date as target), prioritize those
                var todayFiles = filesWithTimestamps.Where(f => f.FileTime.Date == localTargetTime.Date).ToList();

                if (todayFiles.Any())
                {
                    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [FindClosestAudioFile] Found {todayFiles.Count} files from target date {localTargetTime.Date:yyyy-MM-dd}");

                    foreach (var todayFile in todayFiles.OrderBy(f => Math.Abs((f.FileTime - localTargetTime).Ticks)))
                    {
                        TimeSpan difference = TimeSpan.FromTicks(Math.Abs(todayFile.FileTime.Ticks - localTargetTime.Ticks));

                        if (difference < closestDifference)
                        {
                            closestDifference = difference;
                            closestFile = todayFile.FilePath;
                            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [FindClosestAudioFile] Today's file {todayFile.FileName} is closest: FileTime={todayFile.FileTime:yyyy-MM-dd HH:mm:ss.fff}, Difference={difference.TotalMilliseconds}ms");
                        }
                    }
                }
                else
                {
                    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [FindClosestAudioFile] No files found from target date {localTargetTime.Date:yyyy-MM-dd}, searching all files");

                    // Search all files for the closest timestamp
                    foreach (var file in filesWithTimestamps)
                    {
                        TimeSpan difference = TimeSpan.FromTicks(Math.Abs(file.FileTime.Ticks - localTargetTime.Ticks));

                        if (difference < closestDifference)
                        {
                            closestDifference = difference;
                            closestFile = file.FilePath;
                            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [FindClosestAudioFile] File {file.FileName} is closest: FileTime={file.FileTime:yyyy-MM-dd HH:mm:ss.fff}, Difference={difference.TotalMilliseconds}ms");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.FindClosestAudioFile] Error finding closest audio file: {ex.Message}");
                return null;
            }

            if (closestFile != null)
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.FindClosestAudioFile] Found closest audio file: {closestFile}, difference: {closestDifference.TotalMilliseconds}ms");
            }
            else
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.FindClosestAudioFile] No suitable audio file found.");
            }

            return closestFile;
        }

        /// <summary>
        /// Gets the fallback audio directory path based on the node's name and type
        /// </summary>
        private string GetFallbackAudioDirectoryPath()
        {
            if (this.Name.Contains("Webcam") && this.Name.Contains("Audio"))
            {
                return @"C:\Users\tanne\Documents\CSimple\Resources\WebcamAudio\";
            }
            else if (this.Name.Contains("PC") && this.Name.Contains("Audio"))
            {
                return @"C:\Users\tanne\Documents\CSimple\Resources\PCAudio\";
            }
            else
            {
                // Default fallback to PCAudio for generic audio requests
                return @"C:\Users\tanne\Documents\CSimple\Resources\PCAudio\";
            }
        }

        /// <summary>
        /// Reads content from file for File nodes
        /// </summary>
        private (string Type, string Value) GetFileNodeContent()
        {
            try
            {
                string filePath = null;

                // Check if this is a special Goals node
                if (this.Name.ToLowerInvariant().Contains("goals"))
                {
                    // Use the goals.json file path
                    var resourcesDir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                        "CSimple",
                        "Resources"
                    );
                    filePath = Path.Combine(resourcesDir, "goals.json");
                }
                // Check if this is a special Plans node
                else if (this.Name.ToLowerInvariant().Contains("plans"))
                {
                    // Use the plans.json file path
                    var resourcesDir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                        "CSimple",
                        "Resources"
                    );
                    filePath = Path.Combine(resourcesDir, "plans.json");
                }
                else if (!string.IsNullOrEmpty(this.ModelPath))
                {
                    // Use ModelPath as file path if specified
                    filePath = this.ModelPath;
                }
                else if (!string.IsNullOrEmpty(this.Name))
                {
                    // Try to construct file path from node name
                    if (Path.HasExtension(this.Name))
                    {
                        // If the node name has an extension, use it in Memory folder
                        var memoryDir = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                            "CSimple",
                            "Resources",
                            "Memory"
                        );
                        filePath = Path.Combine(memoryDir, this.Name);
                    }
                    else
                    {
                        // Add appropriate extension if no extension provided
                        var memoryDir = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                            "CSimple",
                            "Resources",
                            "Memory"
                        );

                        // Use .json extension for Goals and Plans nodes
                        if (this.Name.ToLowerInvariant().Contains("goals") ||
                            this.Name.ToLowerInvariant().Contains("plans"))
                        {
                            filePath = Path.Combine(memoryDir, $"{this.Name}.json");
                        }
                        else
                        {
                            filePath = Path.Combine(memoryDir, $"{this.Name}.txt");
                        }
                    }
                }

                if (string.IsNullOrEmpty(filePath))
                {
                    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [GetFileNodeContent] No file path determined for File node '{this.Name}'");
                    return ("text", "");
                }

                if (!File.Exists(filePath))
                {
                    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [GetFileNodeContent] File not found: {filePath}");
                    return ("text", "");
                }

                // Read file content
                string fileContent = File.ReadAllText(filePath);

                // Determine content type based on file extension
                string contentType = "text"; // Default to text
                string fileExtension = Path.GetExtension(filePath).ToLowerInvariant();

                if (fileExtension == ".json")
                {
                    // For JSON files like goals.json, return as formatted text
                    contentType = "text";

                    // For goals.json, format the JSON in a more readable way for models
                    if (this.Name.ToLowerInvariant().Contains("goals"))
                    {
                        fileContent = FormatGoalsContent(fileContent);
                    }
                    // For plans.json, format the JSON in a more readable way for models
                    else if (this.Name.ToLowerInvariant().Contains("plans"))
                    {
                        fileContent = FormatPlansContent(fileContent);
                    }
                }

                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [GetFileNodeContent] Successfully read {fileContent.Length} characters from {filePath}");
                return (contentType, fileContent);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [GetFileNodeContent] Error reading file content for node '{this.Name}': {ex.Message}");
                return ("text", "");
            }
        }

        /// <summary>
        /// Formats goals JSON content for better model consumption
        /// </summary>
        private string FormatGoalsContent(string jsonContent)
        {
            try
            {
                var goals = System.Text.Json.JsonSerializer.Deserialize<List<dynamic>>(jsonContent);
                if (goals == null || goals.Count == 0)
                {
                    return "No goals currently defined.";
                }

                var formattedGoals = new StringBuilder();
                formattedGoals.AppendLine("Current User Goals:");
                formattedGoals.AppendLine();

                foreach (var goal in goals)
                {
                    // Extract properties from the dynamic object
                    var goalObj = (System.Text.Json.JsonElement)goal;

                    if (goalObj.TryGetProperty("Title", out var titleProp))
                    {
                        formattedGoals.AppendLine($"Goal: {titleProp.GetString()}");
                    }

                    if (goalObj.TryGetProperty("Description", out var descProp) && !string.IsNullOrWhiteSpace(descProp.GetString()))
                    {
                        formattedGoals.AppendLine($"Description: {descProp.GetString()}");
                    }

                    if (goalObj.TryGetProperty("Priority", out var priorityProp))
                    {
                        formattedGoals.AppendLine($"Priority: {priorityProp.GetInt32()}/5");
                    }

                    if (goalObj.TryGetProperty("Deadline", out var deadlineProp))
                    {
                        var deadline = DateTime.Parse(deadlineProp.GetString());
                        formattedGoals.AppendLine($"Deadline: {deadline:yyyy-MM-dd}");
                    }

                    if (goalObj.TryGetProperty("GoalType", out var typeProp) && typeProp.ValueKind != System.Text.Json.JsonValueKind.Null)
                    {
                        formattedGoals.AppendLine($"Type: {typeProp.GetString()}");
                    }

                    formattedGoals.AppendLine();
                }

                return formattedGoals.ToString();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [FormatGoalsContent] Error formatting goals content: {ex.Message}");
                // Return raw JSON if formatting fails
                return jsonContent;
            }
        }

        private string FormatPlansContent(string jsonContent)
        {
            try
            {
                var plans = System.Text.Json.JsonSerializer.Deserialize<List<dynamic>>(jsonContent);
                if (plans == null || plans.Count == 0)
                {
                    return "No plans currently defined.";
                }

                var formattedPlans = new StringBuilder();
                formattedPlans.AppendLine("Current User Plans:");
                formattedPlans.AppendLine();

                foreach (var plan in plans)
                {
                    // Extract properties from the dynamic object
                    var planObj = (System.Text.Json.JsonElement)plan;

                    if (planObj.TryGetProperty("Title", out var titleProp))
                    {
                        formattedPlans.AppendLine($"Plan: {titleProp.GetString()}");
                    }

                    if (planObj.TryGetProperty("Description", out var descProp) && !string.IsNullOrWhiteSpace(descProp.GetString()))
                    {
                        formattedPlans.AppendLine($"Description: {descProp.GetString()}");
                    }

                    if (planObj.TryGetProperty("Priority", out var priorityProp))
                    {
                        formattedPlans.AppendLine($"Priority: {priorityProp.GetInt32()}/5");
                    }

                    if (planObj.TryGetProperty("Deadline", out var deadlineProp))
                    {
                        var deadline = DateTime.Parse(deadlineProp.GetString());
                        formattedPlans.AppendLine($"Deadline: {deadline:yyyy-MM-dd}");
                    }

                    if (planObj.TryGetProperty("PlanType", out var typeProp) && typeProp.ValueKind != System.Text.Json.JsonValueKind.Null)
                    {
                        formattedPlans.AppendLine($"Type: {typeProp.GetString()}");
                    }

                    if (planObj.TryGetProperty("Status", out var statusProp) && statusProp.ValueKind != System.Text.Json.JsonValueKind.Null)
                    {
                        formattedPlans.AppendLine($"Status: {statusProp.GetString()}");
                    }

                    formattedPlans.AppendLine();
                }

                return formattedPlans.ToString();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [FormatPlansContent] Error formatting plans content: {ex.Message}");
                // Return raw JSON if formatting plans content
                return jsonContent;
            }
        }

        /// <summary>
        /// Generates meaningful text content for text-based input nodes
        /// </summary>
        private string GetTextContent((string Type, string Value) stepData, DateTime? actionItemTimestamp)
        {
            try
            {
                // For Keyboard Text nodes, generate meaningful keyboard input representation
                if (this.Name.Contains("Keyboard") && this.Name.Contains("Text"))
                {
                    return GenerateKeyboardTextContent(stepData, actionItemTimestamp);
                }

                // For Mouse Text nodes, generate meaningful mouse data representation  
                if (this.Name.Contains("Mouse") && this.Name.Contains("Text"))
                {
                    return GenerateMouseTextContent(stepData, actionItemTimestamp);
                }

                // Default: return the original value
                return stepData.Value;
            }
            catch (Exception)
            {
                // Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.GetTextContent] Error generating text content: {ex.Message}");
                return stepData.Value;
            }
        }

        /// <summary>
        /// Generates keyboard input text from ActionItem data in the same format as ActionService execution
        /// </summary>
        private string GenerateKeyboardTextContent((string Type, string Value) stepData, DateTime? actionItemTimestamp)
        {
            // Try to extract real ActionItem data for the current step if available
            var actionItems = GetActionItemsFromSteps();
            if (actionItems != null && actionItems.Count > 0)
            {
                // Look for keyboard actions that match the current step timestamp or are relevant to this step
                var keyboardActionItems = actionItems.Where(item =>
                    item.EventType == 0x0100 || item.EventType == 0x0101).ToList(); // WM_KEYDOWN or WM_KEYUP

                if (keyboardActionItems.Count > 0)
                {
                    // If we have a specific timestamp, find the closest keyboard action within a reasonable time window
                    ActionItem keyboardActionItem = null;
                    if (actionItemTimestamp.HasValue)
                    {
                        // Find keyboard actions within a 500ms window of the action timestamp
                        var timeWindow = TimeSpan.FromMilliseconds(500);
                        keyboardActionItem = keyboardActionItems
                            .Where(item => item.Timestamp is DateTime)
                            .Where(item => Math.Abs(((DateTime)item.Timestamp - actionItemTimestamp.Value).TotalMilliseconds) <= timeWindow.TotalMilliseconds)
                            .OrderBy(item => Math.Abs(((DateTime)item.Timestamp - actionItemTimestamp.Value).Ticks))
                            .FirstOrDefault();
                    }

                    // If no timestamp-based match, try to get a step-specific keyboard action
                    if (keyboardActionItem == null && ActionSteps != null && ActionSteps.Count > 0)
                    {
                        // For now, use the first keyboard action as a fallback
                        // TODO: Implement proper step correlation when step indexing is available
                        keyboardActionItem = keyboardActionItems.FirstOrDefault();
                    }

                    if (keyboardActionItem != null)
                    {
                        // Format exactly like ActionService expects it
                        string eventTypeName = keyboardActionItem.EventType == 0x0100 ? "KeyDown" : "KeyUp";
                        string keyName = GetFriendlyKeyName(keyboardActionItem.KeyCode.ToString());

                        // Format like ActionService simulation format
                        return $"EventType: 0x{keyboardActionItem.EventType:X4}, " +
                               $"Action: {eventTypeName}, " +
                               $"KeyCode: {keyboardActionItem.KeyCode}, " +
                               $"Key: {keyName}, " +
                               $"Duration: {keyboardActionItem.Duration}ms, " +
                               $"Timestamp: {keyboardActionItem.Timestamp:yyyy-MM-dd HH:mm:ss.fff}";
                    }
                }
            }

            // Fallback: If we have valid ActionItem data with keyboard events, extract the key information
            if (stepData.Value != null && stepData.Value.Contains("Key "))
            {
                // Parse existing key information from action description
                string keyInfo = stepData.Value;

                if (keyInfo.Contains("Key ") && keyInfo.Contains(" Down"))
                {
                    string keyPart = keyInfo.Replace("Key ", "").Replace(" Down", "");
                    string friendlyKeyName = GetFriendlyKeyName(keyPart);
                    return $"EventType: 0x0100, Action: KeyDown, KeyCode: {keyPart}, Key: {friendlyKeyName}";
                }
                else if (keyInfo.Contains("Key ") && keyInfo.Contains(" Up"))
                {
                    string keyPart = keyInfo.Replace("Key ", "").Replace(" Up", "");
                    string friendlyKeyName = GetFriendlyKeyName(keyPart);
                    return $"EventType: 0x0101, Action: KeyUp, KeyCode: {keyPart}, Key: {friendlyKeyName}";
                }
            }

            // Return empty string if no keyboard input data is found for this step
            return string.Empty;
        }

        /// <summary>
        /// Generates mouse movement/action text from ActionItem data in the same format as ActionService execution
        /// </summary>
        private string GenerateMouseTextContent((string Type, string Value) stepData, DateTime? actionItemTimestamp)
        {
            // Try to extract real ActionItem data for the current step if available
            var actionItems = GetActionItemsFromSteps();
            if (actionItems != null && actionItems.Count > 0)
            {
                // Look for mouse actions that match the current step timestamp or are relevant to this step
                var mouseActionItems = actionItems.Where(item =>
                    item.EventType == 0x0200 || item.EventType == 512 || // Mouse move
                    item.EventType == 0x0201 || item.EventType == 0x0202 || // Left button
                    item.EventType == 0x0204 || item.EventType == 0x0205 || // Right button
                    item.EventType == 0x0207 || item.EventType == 0x0208).ToList();  // Middle button

                if (mouseActionItems.Count > 0)
                {
                    // If we have a specific timestamp, find the closest mouse action within a reasonable time window
                    ActionItem mouseActionItem = null;
                    if (actionItemTimestamp.HasValue)
                    {
                        // Find mouse actions within a 500ms window of the action timestamp
                        var timeWindow = TimeSpan.FromMilliseconds(500);
                        mouseActionItem = mouseActionItems
                            .Where(item => item.Timestamp is DateTime)
                            .Where(item => Math.Abs(((DateTime)item.Timestamp - actionItemTimestamp.Value).TotalMilliseconds) <= timeWindow.TotalMilliseconds)
                            .OrderBy(item => Math.Abs(((DateTime)item.Timestamp - actionItemTimestamp.Value).Ticks))
                            .FirstOrDefault();
                    }

                    // If no timestamp-based match, try to get a step-specific mouse action
                    if (mouseActionItem == null && ActionSteps != null && ActionSteps.Count > 0)
                    {
                        // For now, use the first mouse action as a fallback
                        // TODO: Implement proper step correlation when step indexing is available
                        mouseActionItem = mouseActionItems.FirstOrDefault();
                    }

                    if (mouseActionItem != null)
                    {
                        // Format exactly like ActionService expects it
                        if (mouseActionItem.EventType == 0x0200 || mouseActionItem.EventType == 512)
                        {
                            // Mouse movement - format like ActionService
                            return $"EventType: 0x{mouseActionItem.EventType:X4}, " +
                                   $"Action: MouseMove, " +
                                   $"Coordinates: X={mouseActionItem.Coordinates?.X ?? 0}, Y={mouseActionItem.Coordinates?.Y ?? 0}, " +
                                   $"DeltaX: {mouseActionItem.DeltaX}, " +
                                   $"DeltaY: {mouseActionItem.DeltaY}, " +
                                   $"VelocityX: {mouseActionItem.VelocityX:F2}, " +
                                   $"VelocityY: {mouseActionItem.VelocityY:F2}, " +
                                   $"ButtonStates: L={mouseActionItem.IsLeftButtonDown}, R={mouseActionItem.IsRightButtonDown}, M={mouseActionItem.IsMiddleButtonDown}, " +
                                   $"Timestamp: {mouseActionItem.Timestamp:yyyy-MM-dd HH:mm:ss.fff}";
                        }
                        else
                        {
                            // Mouse button action - format like ActionService
                            string buttonType = GetMouseButtonType(mouseActionItem.EventType);
                            string buttonAction = IsMouseButtonDown(mouseActionItem.EventType) ? "Down" : "Up";

                            return $"EventType: 0x{mouseActionItem.EventType:X4}, " +
                                   $"Action: {buttonType}Button{buttonAction}, " +
                                   $"Coordinates: X={mouseActionItem.Coordinates?.X ?? 0}, Y={mouseActionItem.Coordinates?.Y ?? 0}, " +
                                   $"Duration: {mouseActionItem.Duration}ms, " +
                                   $"ButtonStates: L={mouseActionItem.IsLeftButtonDown}, R={mouseActionItem.IsRightButtonDown}, M={mouseActionItem.IsMiddleButtonDown}, " +
                                   $"Timestamp: {mouseActionItem.Timestamp:yyyy-MM-dd HH:mm:ss.fff}";
                        }
                    }
                }
            }

            // Fallback: If we have valid ActionItem data with mouse events, extract the coordinate information
            if (stepData.Value != null)
            {
                if (stepData.Value.Contains("Mouse Move"))
                {
                    // Extract delta information if available
                    if (stepData.Value.Contains("DeltaX:") && stepData.Value.Contains("DeltaY:"))
                    {
                        try
                        {
                            // Parse delta values from action description
                            string[] parts = stepData.Value.Split(',');
                            string deltaXPart = parts.FirstOrDefault(p => p.Contains("DeltaX:"))?.Trim();
                            string deltaYPart = parts.FirstOrDefault(p => p.Contains("DeltaY:"))?.Trim();

                            if (deltaXPart != null && deltaYPart != null)
                            {
                                string deltaX = deltaXPart.Replace("DeltaX:", "").Trim();
                                string deltaY = deltaYPart.Replace("DeltaY:", "").Trim();
                                return $"EventType: 0x0200, Action: MouseMove, DeltaX: {deltaX}, DeltaY: {deltaY}";
                            }
                        }
                        catch
                        {
                            // Fall through to default
                        }
                    }
                    return "EventType: 0x0200, Action: MouseMove";
                }
                else if (stepData.Value.Contains("Left Click"))
                {
                    return "EventType: 0x0201, Action: LeftButtonDown";
                }
                else if (stepData.Value.Contains("Right Click"))
                {
                    return "EventType: 0x0204, Action: RightButtonDown";
                }
            }

            // Return empty string if no mouse input data is found for this step
            return string.Empty;
        }

        /// <summary>
        /// Helper method to extract ActionItems from steps data (if available)
        /// </summary>
        private List<ActionItem> GetActionItemsFromSteps()
        {
            // Access current ActionItems through static property
            return CurrentActionItems ?? new List<ActionItem>();
        }

        /// <summary>
        /// Helper method to determine mouse button type from EventType
        /// </summary>
        private string GetMouseButtonType(int eventType)
        {
            return eventType switch
            {
                0x0201 or 0x0202 => "Left",     // WM_LBUTTONDOWN or WM_LBUTTONUP
                0x0204 or 0x0205 => "Right",    // WM_RBUTTONDOWN or WM_RBUTTONUP
                0x0207 or 0x0208 => "Middle",   // WM_MBUTTONDOWN or WM_MBUTTONUP
                _ => "Unknown"
            };
        }

        /// <summary>
        /// Helper method to determine if mouse button event is down
        /// </summary>
        private bool IsMouseButtonDown(int eventType)
        {
            return eventType == 0x0201 || eventType == 0x0204 || eventType == 0x0207; // Down events
        }

        /// <summary>
        /// Converts virtual key codes to user-friendly names
        /// </summary>
        private string GetFriendlyKeyName(string keyCode)
        {
            if (int.TryParse(keyCode, out int code))
            {
                // Common key mappings
                switch (code)
                {
                    case 8: return "Backspace";
                    case 9: return "Tab";
                    case 13: return "Enter";
                    case 16: return "Shift";
                    case 17: return "Ctrl";
                    case 18: return "Alt";
                    case 20: return "Caps Lock";
                    case 27: return "Escape";
                    case 32: return "Space";
                    case 37: return "Left Arrow";
                    case 38: return "Up Arrow";
                    case 39: return "Right Arrow";
                    case 40: return "Down Arrow";
                    case 46: return "Delete";
                    case 91: return "Windows Key";

                    // A-Z keys
                    case >= 65 and <= 90: return ((char)code).ToString();

                    // 0-9 keys
                    case >= 48 and <= 57: return ((char)code).ToString();

                    // Function keys
                    case >= 112 and <= 123: return $"F{code - 111}";

                    default: return $"Key({code})";
                }
            }

            return keyCode; // Return as-is if not a number
        }

        // Simulate audio segment extraction
        private string SimulateAudioSegmentExtraction(string fullPath, DateTime startTime, DateTime endTime)
        {
            // Instead of creating fake segment files, return the actual existing audio file
            // The Python script will handle the actual audio processing

            // If the fullPath exists and is valid, return it directly
            if (!string.IsNullOrEmpty(fullPath) && File.Exists(fullPath))
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.SimulateAudioSegmentExtraction] Returning existing audio file: {fullPath}");
                return fullPath;
            }

            // If fullPath doesn't exist, try to find the most recent audio file based on current date
            string directoryPath = GetFallbackAudioDirectoryPath();

            if (!Directory.Exists(directoryPath))
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.SimulateAudioSegmentExtraction] Directory doesn't exist: {directoryPath}");
                return null;
            }

            // Find the most recent .wav file that's not a segment file, prioritizing today's date
            var today = DateTime.Now.Date;
            var audioFiles = Directory.GetFiles(directoryPath, "*.wav")
                .Where(f => !Path.GetFileName(f).StartsWith("Segment_", StringComparison.OrdinalIgnoreCase))
                .Select(f => new { FilePath = f, FileName = Path.GetFileNameWithoutExtension(f), LastWrite = File.GetLastWriteTime(f) })
                .ToList();

            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [SimulateAudioSegmentExtraction] Found {audioFiles.Count} audio files in {directoryPath}");

            // First, try to find files from today based on filename timestamps
            var todayFiles = audioFiles.Where(f => TryExtractDateFromFilename(f.FileName, out DateTime fileDate) && fileDate.Date == today).ToList();

            if (todayFiles.Any())
            {
                // Get the most recent file from today
                var mostRecentTodayFile = todayFiles.OrderByDescending(f => TryExtractDateFromFilename(f.FileName, out DateTime fileDate) ? fileDate : DateTime.MinValue).First();
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.SimulateAudioSegmentExtraction] Found today's audio file: {mostRecentTodayFile.FilePath}");
                return mostRecentTodayFile.FilePath;
            }

            // If no files from today, get the most recent file by last write time
            if (audioFiles.Any())
            {
                var mostRecentFile = audioFiles.OrderByDescending(f => f.LastWrite).First();
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.SimulateAudioSegmentExtraction] Found most recent audio file: {mostRecentFile.FilePath} (LastWrite: {mostRecentFile.LastWrite})");
                return mostRecentFile.FilePath;
            }

            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.SimulateAudioSegmentExtraction] No audio files found in directory: {directoryPath}");
            return null;
        }

        /// <summary>
        /// Tries to extract a DateTime from an audio filename pattern like PCAudio_20250913_120605.wav
        /// </summary>
        private bool TryExtractDateFromFilename(string filename, out DateTime extractedDate)
        {
            extractedDate = DateTime.MinValue;

            try
            {
                // Handle patterns like PCAudio_20250913_120605 or WebcamAudio_20250913_120605
                var parts = filename.Split('_');
                if (parts.Length >= 3)
                {
                    string datePart = parts[1]; // 20250913
                    string timePart = parts[2]; // 120605

                    if (datePart.Length == 8 && timePart.Length >= 6)
                    {
                        return DateTime.TryParseExact($"{datePart}_{timePart.Substring(0, 6)}", "yyyyMMdd_HHmmss", null, System.Globalization.DateTimeStyles.None, out extractedDate);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [TryExtractDateFromFilename] Error parsing filename {filename}: {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// Stores the generated output for a model node at a specific step
        /// </summary>
        /// <param name="step">The step number (1-based)</param>
        /// <param name="outputType">The type of output (e.g., "text", "image", "audio")</param>
        /// <param name="outputValue">The generated output content</param>
        public void SetStepOutput(int step, string outputType, string outputValue)
        {
            // Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.SetStepOutput] Setting output for node '{Name}' at step {step}: Type='{outputType}', Value length={outputValue?.Length ?? 0}");

            if (ActionSteps == null)
            {
                ActionSteps = new List<(string Type, string Value)>();
            }

            // Ensure we have enough slots for the given step (1-based)
            while (ActionSteps.Count < step)
            {
                ActionSteps.Add((null, null));
            }

            // Set the output at the specified step (convert to 0-based index)
            ActionSteps[step - 1] = (outputType, outputValue);

            // Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.SetStepOutput] Successfully stored output for step {step}. ActionSteps.Count is now {ActionSteps.Count}");
        }

        /// <summary>
        /// Gets the stored output for a model node at a specific step
        /// </summary>
        /// <param name="step">The step number (1-based)</param>
        /// <returns>The stored output, or null if not found</returns>
        public (string Type, string Value) GetStepOutput(int step)
        {
            if (ActionSteps == null || step <= 0 || step > ActionSteps.Count)
            {
                return (null, null);
            }

            var (type, value) = ActionSteps[step - 1];

            // Apply cleaning logic to remove concatenated ensemble input from stored content
            if (!string.IsNullOrEmpty(value) && Type == NodeType.Model)
            {
                value = CleanStoredModelOutput(value);
            }

            return (type, value);
        }

        /// <summary>
        /// Cleans stored model output to remove concatenated ensemble input
        /// </summary>
        private string CleanStoredModelOutput(string storedOutput)
        {
            if (string.IsNullOrEmpty(storedOutput))
                return storedOutput;

            // Check if this looks like concatenated ensemble input
            if (storedOutput.Contains("Screen Image:") || storedOutput.Contains("Webcam Image:") ||
                storedOutput.Contains("Goals (File):") || storedOutput.Contains("PC Audio:") ||
                storedOutput.Contains("Webcam Audio:") || storedOutput.Contains("Goal:") ||
                storedOutput.Contains("Blip Image Captioning Base:") || storedOutput.Contains("Gpt2:") ||
                storedOutput.Contains("Whisper Base:"))
            {
                // Split into sentences and look for the actual model output
                var sentences = storedOutput.Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var sentence in sentences)
                {
                    string cleanSentence = sentence.Trim();

                    // Skip sentences that are clearly input data or other model outputs
                    if (cleanSentence.Contains("Screen Image:") || cleanSentence.Contains("Webcam Image:") ||
                        cleanSentence.Contains("Goals (File):") || cleanSentence.Contains("PC Audio:") ||
                        cleanSentence.Contains("Webcam Audio:") || cleanSentence.Contains("Goal:") ||
                        cleanSentence.Contains("Priority:") || cleanSentence.Contains("Deadline:") ||
                        cleanSentence.Contains("Description:") || cleanSentence.Contains("Build an app") ||
                        cleanSentence.Contains("Current User Goals") || cleanSentence.Length < 10 ||
                        cleanSentence.Contains("[Client thread/INFO]") || cleanSentence.Contains("Loading skin images") ||
                        cleanSentence.Contains("Config file") || cleanSentence.Contains("AppData") ||
                        cleanSentence.Contains("Roaming") || cleanSentence.Contains("GEMFILE") ||
                        cleanSentence.Contains("ItemID=") || cleanSentence.Contains("CBE") ||
                        cleanSentence.Contains("FFTs are still in play") || cleanSentence.Contains("RUNABLE TO OBJECTIONED") ||
                        cleanSentence.Contains("Dec ") || cleanSentence.Contains("Nov ") || cleanSentence.Contains("Oct ") ||
                        cleanSentence.Contains("If there were any mistakes") || cleanSentence.Contains("sooner rather than later"))
                    {
                        continue;
                    }

                    // Clean any model prefixes from the sentence
                    string[] prefixesToRemove = {
                        "Gpt2 [Goal]:", "Gpt2 [Plan]:", "Gpt2 [Action]:", "Gpt2:",
                        "Blip Image Captioning Base:", "Whisper Base:",
                        Name + ":"
                    };

                    foreach (var prefix in prefixesToRemove)
                    {
                        if (cleanSentence.StartsWith(prefix))
                        {
                            cleanSentence = cleanSentence.Substring(prefix.Length).Trim();
                            break;
                        }
                    }

                    // If we found a clean sentence that looks like actual generated content, return it
                    if (!string.IsNullOrEmpty(cleanSentence) && cleanSentence.Length > 15 &&
                        !cleanSentence.Contains("Compress PNG") && !cleanSentence.Contains("hello 1124"))
                    {
                        return cleanSentence;
                    }
                }

                // If no clean content found, try to extract content after the last meaningful colon
                var parts = storedOutput.Split(new[] { ": " }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 1)
                {
                    // Take the last meaningful part that doesn't look like input data
                    for (int i = parts.Length - 1; i >= 0; i--)
                    {
                        var part = parts[i].Trim();
                        if (!part.Contains("Goal:") && !part.Contains("Priority:") &&
                            !part.Contains("Description:") && !part.Contains("Deadline:") &&
                            part.Length > 15)
                        {
                            return part;
                        }
                    }
                }

                // Last resort: return a clean message
                return $"Model {Name} output processed (concatenated input filtered)";
            }

            // Remove simple model prefixes for non-concatenated content
            if (storedOutput.Contains(": "))
            {
                var colonIndex = storedOutput.IndexOf(": ");
                var prefix = storedOutput.Substring(0, colonIndex);

                if (prefix.Contains("Gpt") || prefix.Contains("Blip") || prefix.Contains("Image") ||
                    prefix.Contains("[Plan]") || prefix.Contains("[Goal]") || prefix.Contains("[Action]") ||
                    prefix.Contains("Captioning") || prefix.Contains("Base") || prefix.Contains(Name))
                {
                    return storedOutput.Substring(colonIndex + 2).Trim();
                }
            }

            return storedOutput;
        }
    }
}
