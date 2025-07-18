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

        public (string Type, string Value) GetStepContent(int step, DateTime? actionTimestamp) // step is 1-based
        {
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.GetStepContent] Node '{Name}' (Type: {Type}, DataType: {DataType}), Requested Step: {step} (1-based), ActionSteps.Count: {ActionSteps.Count}, ActionTimestamp: {actionTimestamp}");

            if (ActionSteps == null)
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.GetStepContent] ActionSteps list is null. Returning null content.");
                return (null, null);
            }

            // Allow both Input and Model nodes to have step content
            if (Type != NodeType.Input && Type != NodeType.Model)
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.GetStepContent] Condition not met: Node is not of Input or Model type (Type: {Type}). Returning null content.");
                return (null, null);
            }

            if (step <= 0)
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.GetStepContent] Condition not met: Requested step {step} (1-based) is not a positive integer. Returning null content.");
                return (null, null);
            }

            if (ActionSteps.Count == 0)
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.GetStepContent] ActionSteps is empty. Cannot retrieve content for step {step}. Returning null content.");
                return (null, null);
            }

            if (step > ActionSteps.Count)
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.GetStepContent] Condition not met: Requested step {step} (1-based) is out of bounds for this node's ActionSteps (Count: {ActionSteps.Count}). Returning null content.");
                return (null, null);
            }

            // Adjust step to be 0-indexed for list access
            var stepData = ActionSteps[step - 1];
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.GetStepContent] Accessing ActionSteps[{step - 1}]. Step Data: Type='{stepData.Type}', Supposed File/Content Value='{stepData.Value}'");

            // Ensure the stepData.Type matches the node's DataType for relevance
            // This check might be redundant if ActionSteps was already populated with matching types.
            if (!string.Equals(stepData.Type, this.DataType, StringComparison.OrdinalIgnoreCase))
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.GetStepContent] Warning: Step data type '{stepData.Type}' from ActionSteps[{step - 1}] does not match node's DataType '{this.DataType}'. Returning content as is.");
            }

            // For Model nodes, if we have stored output (generated content), return it directly
            //without trying to find image/audio files, as the stored content is the actual generated output
            // This prevents issues where generated text like "Image Caption: ..." gets parsed as timestamp data
            if (Type == NodeType.Model && !string.IsNullOrEmpty(stepData.Value))
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.GetStepContent] Model node has stored output, returning directly: Type='{stepData.Type}', Value length={stepData.Value.Length}");
                return (stepData.Type, stepData.Value);
            }

            // For Input nodes, attempt to find the actual resource file based on timestamp
            if (Type == NodeType.Input && actionTimestamp.HasValue)
            {
                string resourceFile = null;

                if (DataType?.ToLower() == "image")
                {
                    // Find the closest image file based on the action timestamp
                    resourceFile = FindClosestImageFileByTimestamp(actionTimestamp.Value);
                    if (!string.IsNullOrEmpty(resourceFile))
                    {
                        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.GetStepContent] Found closest image file: {resourceFile}");
                        return (stepData.Type, resourceFile);
                    }
                }
                else if (DataType?.ToLower() == "audio")
                {
                    // Find the closest audio file based on the action timestamp
                    resourceFile = FindClosestAudioFileByTimestamp(actionTimestamp.Value);
                    if (!string.IsNullOrEmpty(resourceFile))
                    {
                        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.GetStepContent] Found closest audio file: {resourceFile}");
                        return (stepData.Type, resourceFile);
                    }
                }
                else if (DataType?.ToLower() == "text")
                {
                    // For text nodes, we can try to find timestamped text files or just return the action description
                    resourceFile = FindClosestTextFileByTimestamp(actionTimestamp.Value);
                    if (!string.IsNullOrEmpty(resourceFile))
                    {
                        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.GetStepContent] Found closest text file: {resourceFile}");
                        return (stepData.Type, resourceFile);
                    }
                }

                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.GetStepContent] No resource file found for {DataType} at timestamp {actionTimestamp}, falling back to action description");
            }

            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.GetStepContent] Returning action description: Type='{stepData.Type}', Value='{stepData.Value}'");
            return (stepData.Type, stepData.Value);
        }

        /// <summary>
        /// Finds the closest image file based on action timestamp
        /// </summary>
        private string FindClosestImageFileByTimestamp(DateTime actionTimestamp)
        {
            EnsureResourceDirectoriesExist();

            string baseDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CSimple", "Resources");
            var imagePaths = new[]
            {
                (Path.Combine(baseDirectory, "WebcamImages"), "WebcamImage_*.jpg"),
                (Path.Combine(baseDirectory, "Screenshots"), "ScreenCapture_*.png")
            };

            return FindClosestFileByTimestamp(imagePaths, actionTimestamp);
        }

        /// <summary>
        /// Finds the closest audio file based on action timestamp
        /// </summary>
        private string FindClosestAudioFileByTimestamp(DateTime actionTimestamp)
        {
            EnsureResourceDirectoriesExist();

            string baseDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CSimple", "Resources");
            var audioPaths = new[]
            {
                (Path.Combine(baseDirectory, "PCAudio"), "PCAudio_*.wav"),
                (Path.Combine(baseDirectory, "WebcamAudio"), "WebcamAudio_*.wav")
            };

            return FindClosestFileByTimestamp(audioPaths, actionTimestamp);
        }

        /// <summary>
        /// Finds the closest text file based on action timestamp
        /// </summary>
        private string FindClosestTextFileByTimestamp(DateTime actionTimestamp)
        {
            string baseDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CSimple", "Resources");
            var textPaths = new[]
            {
                (Path.Combine(baseDirectory, "TextLogs"), "TextLog_*.txt"),
                (Path.Combine(baseDirectory, "Notes"), "Note_*.txt"),
                (Path.Combine(baseDirectory, "Screenshots"), "*.txt") // Sometimes OCR results are saved as text files
            };

            return FindClosestFileByTimestamp(textPaths, actionTimestamp);
        }

        /// <summary>
        /// Generic method to find the closest file by timestamp from multiple directories
        /// </summary>
        private string FindClosestFileByTimestamp((string directory, string pattern)[] searchPaths, DateTime targetTimestamp)
        {
            string closestFile = null;
            TimeSpan closestDifference = TimeSpan.MaxValue;

            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.FindClosestFileByTimestamp] Looking for files closest to {targetTimestamp}");

            foreach (var (directory, pattern) in searchPaths)
            {
                if (!Directory.Exists(directory))
                {
                    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.FindClosestFileByTimestamp] Directory does not exist: {directory}");
                    continue;
                }

                try
                {
                    string[] files = Directory.GetFiles(directory, pattern);
                    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.FindClosestFileByTimestamp] Found {files.Length} files in {directory}");

                    foreach (string filePath in files)
                    {
                        DateTime fileTime = ExtractTimestampFromFilename(filePath);
                        if (fileTime != DateTime.MinValue)
                        {
                            TimeSpan difference = TimeSpan.FromTicks(Math.Abs(fileTime.Ticks - targetTimestamp.Ticks));
                            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.FindClosestFileByTimestamp] File: {Path.GetFileName(filePath)}, Time: {fileTime}, Difference: {difference.TotalSeconds:F2}s");

                            if (difference < closestDifference)
                            {
                                closestDifference = difference;
                                closestFile = filePath;
                            }
                        }
                        else
                        {
                            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.FindClosestFileByTimestamp] Could not extract timestamp from: {Path.GetFileName(filePath)}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.FindClosestFileByTimestamp] Error searching {directory}: {ex.Message}");
                }
            }

            if (closestFile != null)
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.FindClosestFileByTimestamp] Found closest file: {closestFile}, difference: {closestDifference.TotalSeconds:F2}s");
            }
            else
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.FindClosestFileByTimestamp] No suitable file found for timestamp {targetTimestamp}");
            }

            return closestFile;
        }

        /// <summary>
        /// Extracts timestamp from filename using various patterns
        /// </summary>
        private DateTime ExtractTimestampFromFilename(string filePath)
        {
            string fileName = Path.GetFileNameWithoutExtension(filePath);

            // Try to find timestamp patterns in the filename
            // Pattern 1: ScreenCapture_yyyyMMdd_HHmmss_fff_DISPLAY.png (with milliseconds)
            var match = System.Text.RegularExpressions.Regex.Match(fileName, @"(\d{8}_\d{6}_\d{3})");
            if (match.Success)
            {
                if (DateTime.TryParseExact(match.Groups[1].Value, "yyyyMMdd_HHmmss_fff", null, System.Globalization.DateTimeStyles.None, out DateTime parsedTimeWithMs))
                {
                    return parsedTimeWithMs;
                }
            }

            // Pattern 2: WebcamImage_yyyyMMdd_HHmmss.jpg or PCAudio_yyyyMMdd_HHmmss.wav (standard format)
            match = System.Text.RegularExpressions.Regex.Match(fileName, @"(\d{8}_\d{6})");
            if (match.Success)
            {
                if (DateTime.TryParseExact(match.Groups[1].Value, "yyyyMMdd_HHmmss", null, System.Globalization.DateTimeStyles.None, out DateTime parsedTime))
                {
                    return parsedTime;
                }
            }

            // Pattern 3: filename_HHmmss (assuming today's date)
            match = System.Text.RegularExpressions.Regex.Match(fileName, @"_(\d{6})(?:_|$)");
            if (match.Success)
            {
                if (DateTime.TryParseExact(match.Groups[1].Value, "HHmmss", null, System.Globalization.DateTimeStyles.None, out DateTime timeOnly))
                {
                    // Combine with today's date
                    return DateTime.Today.Add(timeOnly.TimeOfDay);
                }
            }

            // Pattern 4: Use file creation/modification time as fallback
            try
            {
                FileInfo fileInfo = new FileInfo(filePath);
                return fileInfo.CreationTime;
            }
            catch
            {
                return DateTime.MinValue;
            }
        }

        // Legacy method for backward compatibility - will be removed after testing
        public string FindClosestImageFile(string actionDescription, string actionType = "image")
        {
            if (string.IsNullOrEmpty(actionDescription))
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.FindClosestImageFile] Action description is null or empty.");
                return null;
            }

            // If the action description looks like generated text content (e.g., from a model),
            // don't try to parse it as a timestamp-based file search
            if (actionDescription.Contains("Caption:") ||
                actionDescription.Contains("Generated:") ||
                actionDescription.Contains("Output:") ||
                actionDescription.Length > 100 ||
                (!actionDescription.Contains("_") && !actionDescription.Contains(":")))
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.FindClosestImageFile] Action description appears to be generated content, not a timestamp: {actionDescription.Substring(0, Math.Min(50, actionDescription.Length))}...");
                return null;
            }

            string baseDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CSimple", "Resources");
            string directoryPath = "";
            string filePattern = "";

            if (actionType == "image")
            {
                directoryPath = Path.Combine(baseDirectory, "WebcamImages");
                filePattern = "WebcamImage_*.jpg";
            }
            else
            {
                directoryPath = Path.Combine(baseDirectory, "Screenshots");
                filePattern = "ScreenCapture_*.png";
            }

            if (!Directory.Exists(directoryPath))
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.FindClosestImageFile] Directory does not exist: {directoryPath}");
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
                            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.FindClosestImageFile] Could not parse timestamp: {timestampPart}");
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.FindClosestImageFile] Filename is too short or does not contain underscore: {fileName}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.FindClosestImageFile] Error finding closest image file: {ex.Message}");
                return null;
            }

            if (closestFile != null)
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.FindClosestImageFile] Found closest image file: {closestFile}");
            }
            else
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.FindClosestImageFile] No suitable image file found.");
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
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.GetAudioSegment] Audio file path is not set. Attempting to find closest audio file by timestamp.");
                // Implement logic to find the audio file with the closest timestamp
                string audioFilePath = FindClosestAudioFile(startTime);

                if (string.IsNullOrEmpty(audioFilePath))
                {
                    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.GetAudioSegment] No audio file found with a timestamp close to the action review timestamp.");
                    return null;
                }

                // Simulate audio segment extraction from the found file
                string segmentPath2 = SimulateAudioSegmentExtraction(audioFilePath, startTime, endTime);
                return segmentPath2;
            }

            if (Type != NodeType.Input || DataType?.ToLower() != "audio")
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.GetAudioSegment] Node is not an audio input node.");
                return null;
            }

            if (startTime == DateTime.MinValue || endTime == DateTime.MinValue)
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.GetAudioSegment] Invalid start or end time.");
                return null;
            }

            // Simulate audio segment extraction from the found file
            string segmentPath = SimulateAudioSegmentExtraction(AudioFilePath, startTime, endTime);
            return segmentPath;
        }

        private string FindClosestAudioFile(DateTime targetTime)
        {
            string baseDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CSimple", "Resources");
            string directoryPath = Path.Combine(baseDirectory, "PCAudio");

            if (!Directory.Exists(directoryPath))
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.FindClosestAudioFile] Directory does not exist: {directoryPath}");
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
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.FindClosestAudioFile] Error finding closest audio file: {ex.Message}");
                return null;
            }

            if (closestFile != null)
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.FindClosestAudioFile] Found closest audio file: {closestFile}, difference: {closestDifference}");
            }
            else
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.FindClosestAudioFile] No suitable audio file found.");
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

            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.SimulateAudioSegmentExtraction] Simulated audio segment extraction from {fullPath} to {segmentPath} (Start: {startTime}, End: {endTime})");
            return segmentPath;
        }

        /// <summary>
        /// Stores the generated output for a model node at a specific step
        /// </summary>
        /// <param name="step">The step number (1-based)</param>
        /// <param name="outputType">The type of output (e.g., "text", "image", "audio")</param>
        /// <param name="outputValue">The generated output content</param>
        public void SetStepOutput(int step, string outputType, string outputValue)
        {
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.SetStepOutput] Setting output for node '{Name}' at step {step}: Type='{outputType}', Value length={outputValue?.Length ?? 0}");

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

            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.SetStepOutput] Successfully stored output for step {step}. ActionSteps.Count is now {ActionSteps.Count}");
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

            return ActionSteps[step - 1];
        }

        /// <summary>
        /// Ensures that the resource directories exist, creating them if necessary
        /// </summary>
        private void EnsureResourceDirectoriesExist()
        {
            try
            {
                string baseDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CSimple", "Resources");

                var directories = new[]
                {
                    Path.Combine(baseDirectory, "WebcamImages"),
                    Path.Combine(baseDirectory, "Screenshots"),
                    Path.Combine(baseDirectory, "PCAudio"),
                    Path.Combine(baseDirectory, "WebcamAudio"),
                    Path.Combine(baseDirectory, "TextLogs"),
                    Path.Combine(baseDirectory, "Notes")
                };

                foreach (var dir in directories)
                {
                    if (!Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.EnsureResourceDirectoriesExist] Created directory: {dir}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [NodeViewModel.EnsureResourceDirectoriesExist] Error creating directories: {ex.Message}");
            }
        }
    }
}
