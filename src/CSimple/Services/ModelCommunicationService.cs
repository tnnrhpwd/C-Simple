using CSimple.Models;
using CSimple.Services.AppModeService;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSimple.Services
{
    /// <summary>
    /// Service responsible for handling communication with AI models.
    /// Extracted from NetPageViewModel to improve maintainability and separation of concerns.
    /// Handles message processing, model execution, error handling, and chat history management.
    /// </summary>
    public class ModelCommunicationService
    {
        private readonly FileService _fileService;
        private readonly PythonBootstrapper _pythonBootstrapper;
        private readonly CSimple.Services.AppModeService.AppModeService _appModeService;
        private readonly PythonEnvironmentService _pythonEnvironmentService;

        // Events for status updates
        public event EventHandler<string> StatusChanged;
        public event EventHandler<string> OutputChanged;
        public event EventHandler<bool> CommunicatingChanged; public ModelCommunicationService(
            FileService fileService,
            PythonBootstrapper pythonBootstrapper,
            CSimple.Services.AppModeService.AppModeService appModeService,
            PythonEnvironmentService pythonEnvironmentService)
        {
            _fileService = fileService;
            _pythonBootstrapper = pythonBootstrapper;
            _appModeService = appModeService;
            _pythonEnvironmentService = pythonEnvironmentService;
        }

        public async Task CommunicateWithModelAsync(
            string message,
            NeuralNetworkModel activeModel,
            ObservableCollection<ChatMessage> chatMessages,
            string pythonExecutablePath,
            string huggingFaceScriptPath,
            Func<string, string, string, Task> showAlert,
            Func<Task> suggestCpuFriendlyModels,
            Func<Task<bool>> installAcceleratePackage,
            Func<string, string> getPerformanceTip,
            Func<string, string, NeuralNetworkModel, Task<string>> executeModelEnhanced)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                OnStatusChanged("Cannot send empty message.");
                return;
            }

            // Add user message to chat history
            var userMessage = new ChatMessage(message, isFromUser: true, includeInHistory: true);
            chatMessages.Add(userMessage);
            Debug.WriteLine($"Added user message to chat. ChatMessages count: {chatMessages.Count}");

            if (activeModel == null)
            {
                await HandleNoActiveModelAsync(showAlert, suggestCpuFriendlyModels);
                return;
            }

            // Check if Python is available
            if (string.IsNullOrEmpty(pythonExecutablePath))
            {
                await HandlePythonNotAvailableAsync(showAlert);
                return;
            }

            OnStatusChanged($"Sending message to {activeModel.Name}...");
            OnOutputChanged($"Processing conversation with {activeModel.Name}...");
            OnCommunicatingChanged(true);

            // Build complete chat history for model context
            string fullChatHistory = BuildChatHistoryForModel(chatMessages);
            Debug.WriteLine($"Built chat history with {chatMessages.Where(m => m.IncludeInHistory && !m.IsProcessing).Count()} included messages. History length: {fullChatHistory.Length} characters");

            // Add processing message to chat history
            var processingMessage = new ChatMessage("Processing your request...", isFromUser: false, modelName: activeModel.Name, includeInHistory: false)
            {
                IsProcessing = true,
                LLMSource = _appModeService.CurrentMode == AppMode.Offline ? "local" : "local"
            };
            chatMessages.Add(processingMessage);

            try
            {
                await ProcessModelCommunicationAsync(
                    activeModel,
                    fullChatHistory,
                    processingMessage,
                    huggingFaceScriptPath,
                    showAlert,
                    installAcceleratePackage,
                    getPerformanceTip,
                    executeModelEnhanced);
            }
            catch (Exception ex)
            {
                await HandleCommunicationErrorAsync(
                    ex,
                    activeModel,
                    processingMessage,
                    showAlert,
                    installAcceleratePackage);
            }
            finally
            {
                OnCommunicatingChanged(false);
            }
        }

        private async Task HandleNoActiveModelAsync(
            Func<string, string, string, Task> showAlert,
            Func<Task> suggestCpuFriendlyModels)
        {
            if (_appModeService.CurrentMode == AppMode.Offline)
            {
                OnStatusChanged("No active models available.");
                OnOutputChanged("In offline mode, local models will be executed on your hardware.\n\n" +
                    "Please activate a model to get started. Models that work well locally include:\n" +
                    "• gpt2 - Fast, lightweight text generation (CPU-friendly)\n" +
                    "• distilgpt2 - Even faster version of GPT-2 (CPU-friendly)\n" +
                    "• microsoft/DialoGPT-small - Good for conversations (CPU-friendly)\n" +
                    "• deepseek-ai/DeepSeek-R1 - Advanced model (requires GPU)\n\n" +
                    "GPU models will attempt to use your graphics card for acceleration.\n" +
                    "Switch to online mode if you prefer to use cloud-based execution.");

                await suggestCpuFriendlyModels();
            }
            else
            {
                OnStatusChanged("No active HuggingFace reference model found.");
                OnOutputChanged("Please activate a HuggingFace model first to communicate with it.");
            }
        }

        private async Task HandlePythonNotAvailableAsync(Func<string, string, string, Task> showAlert)
        {
            OnStatusChanged("Python is not available. Cannot run models.");
            OnOutputChanged("Python 3.8 to 3.11 is required to run HuggingFace models locally. Please install from python.org and restart the application.");

            await showAlert("Python Required",
                "Python is required to run HuggingFace models locally.\n\n" +
                "1. Download Python 3.8 to 3.11 from https://python.org/downloads/\n" +
                "   * We recommend Python 3.10 for best compatibility\n" +
                "   * Avoid Python 3.12+ as it may have compatibility issues\n" +
                "2. During installation, check 'Add Python to PATH'\n" +
                "3. Restart this application after installation", "OK");
        }

        private async Task ProcessModelCommunicationAsync(
            NeuralNetworkModel activeModel,
            string fullChatHistory,
            ChatMessage processingMessage,
            string huggingFaceScriptPath,
            Func<string, string, string, Task> showAlert,
            Func<Task<bool>> installAcceleratePackage,
            Func<string, string> getPerformanceTip,
            Func<string, string, NeuralNetworkModel, Task<string>> executeModelEnhanced)
        {
            // Verify the model exists in our persisted models
            var persistedModels = await _fileService.LoadHuggingFaceModelsAsync();
            var modelInFile = persistedModels.FirstOrDefault(m =>
                m.HuggingFaceModelId == activeModel.HuggingFaceModelId ||
                m.Id == activeModel.Id);

            if (modelInFile == null)
            {
                throw new InvalidOperationException($"Model {activeModel.Name} not found in persisted models file.");
            }

            // Check if the script exists
            if (string.IsNullOrEmpty(huggingFaceScriptPath) || !File.Exists(huggingFaceScriptPath))
            {
                Debug.WriteLine($"Script not found, re-initializing Python environment...");
                await _pythonEnvironmentService.SetupPythonEnvironmentAsync(showAlert);
                huggingFaceScriptPath = _pythonEnvironmentService.HuggingFaceScriptPath;
            }

            // Show progress indicator with performance tip
            OnStatusChanged($"Loading {activeModel.Name} (first run may take longer)...");
            string performanceTip = getPerformanceTip(activeModel.HuggingFaceModelId);
            OnOutputChanged($"Processing conversation with {activeModel.Name}...\n\n{performanceTip}");

            // Execute the model
            string result = await executeModelEnhanced(modelInFile.HuggingFaceModelId, fullChatHistory, activeModel);

            // Determine LLM source and update processing message
            string llmSource = DetermineLLMSource(result);
            UpdateProcessingMessage(processingMessage, result, activeModel, llmSource);
        }

        private async Task HandleCommunicationErrorAsync(
            Exception ex,
            NeuralNetworkModel activeModel,
            ChatMessage processingMessage,
            Func<string, string, string, Task> showAlert,
            Func<Task<bool>> installAcceleratePackage)
        {
            Debug.WriteLine($"Error communicating with model {activeModel.Name}: {ex.Message}");
            string errorMessage = ex.Message;
            string errorResponse = "";

            // Handle specific error types
            if (errorMessage.Contains("accelerate") || errorMessage.Contains("FP8 quantized"))
            {
                errorResponse = await HandleAccelerateError(activeModel, installAcceleratePackage);
            }
            else if (errorMessage.Contains("ModuleNotFoundError") || errorMessage.Contains("ImportError"))
            {
                errorResponse = await HandleMissingPackagesError(showAlert);
            }
            else if (errorMessage.Contains("malicious code") || errorMessage.Contains("double-check"))
            {
                errorResponse = HandleSecurityWarning(activeModel);
            }
            else if (errorMessage.Contains("not found in persisted models"))
            {
                errorResponse = "Error: The model may have been removed from the persisted models file.\n\n" +
                    "Please re-import the model from HuggingFace.";
                OnStatusChanged("Model reference lost - please re-import");
            }
            else
            {
                errorResponse = $"Error processing message with {activeModel.Name}: {errorMessage}";
                OnStatusChanged("Model execution failed");
            }

            OnOutputChanged(errorResponse);

            // Update processing message with error
            string errorLlmSource = _appModeService.CurrentMode == AppMode.Offline ? "local" : "local";
            processingMessage.Content = errorResponse;
            processingMessage.IsProcessing = false;
            processingMessage.IncludeInHistory = true;
            processingMessage.LLMSource = errorLlmSource;
            processingMessage.OnPropertyChanged(nameof(processingMessage.ModelDisplayNameWithSourcePrefixed));
        }

        private async Task<string> HandleAccelerateError(NeuralNetworkModel activeModel, Func<Task<bool>> installAcceleratePackage)
        {
            string errorResponse = $"Error: {activeModel.Name} requires additional packages.\n\n" +
                "This model needs 'accelerate' for FP8 quantization support.\n" +
                "Installing required packages...";

            OnOutputChanged(errorResponse);
            OnStatusChanged("Installing accelerate package...");

            bool installed = await installAcceleratePackage();

            if (installed)
            {
                errorResponse += "\n\nPackages installed successfully. Please try sending your message again.";
                OnStatusChanged("Ready - accelerate package installed");
            }
            else
            {
                errorResponse += "\n\nFailed to install accelerate automatically. Please install manually with:\npip install accelerate";
                OnStatusChanged("Manual package installation required");
            }

            return errorResponse;
        }

        private async Task<string> HandleMissingPackagesError(Func<string, string, string, Task> showAlert)
        {
            string errorResponse = "Error: Missing Python packages.\n\n" +
                "Installing required packages: transformers, torch, accelerate...";

            OnOutputChanged(errorResponse);

            await showAlert("Python Packages Required",
                "Required packages are missing. Installing them now...\n\n" +
                "This will install: transformers, torch, accelerate",
                "OK");

            OnStatusChanged("Installing required packages...");
            bool installed = await _pythonBootstrapper.InstallRequiredPackagesAsync();

            if (installed)
            {
                errorResponse += "\n\nPackages installed successfully. Please try sending your message again.";
                OnStatusChanged("Ready - all packages installed");
            }
            else
            {
                errorResponse += "\n\nFailed to install packages automatically. Please install manually.";
                OnStatusChanged("Manual package installation required");
            }

            return errorResponse;
        }

        private string HandleSecurityWarning(NeuralNetworkModel activeModel)
        {
            OnStatusChanged("Model blocked due to security warning");
            return $"Security Warning: {activeModel.Name} downloaded new code files.\n\n" +
                "This is normal for some models but requires acknowledgment for security.\n" +
                "The model execution was blocked for safety. You can try again if you trust the model source.";
        }

        private string DetermineLLMSource(string result)
        {
            if (_appModeService.CurrentMode == AppMode.Offline)
            {
                return "local";
            }
            else
            {
                if (result != null && (result.Contains("used HuggingFace API") ||
                                     result.Contains("api fallback") ||
                                     result.Contains("Falling back to HuggingFace API")))
                {
                    return "api";
                }
                return "local";
            }
        }

        private void UpdateProcessingMessage(ChatMessage processingMessage, string result, NeuralNetworkModel activeModel, string llmSource)
        {
            if (!string.IsNullOrEmpty(result))
            {
                processingMessage.Content = result;
                processingMessage.IsProcessing = false;
                processingMessage.IncludeInHistory = true;
                processingMessage.LLMSource = llmSource;
                OnOutputChanged($"Response from {activeModel.Name}:\n{result}");
                OnStatusChanged($"✓ Response received from {activeModel.Name}");
            }
            else
            {
                processingMessage.Content = "No response received. The model may have executed successfully but produced no output.";
                processingMessage.IsProcessing = false;
                processingMessage.IncludeInHistory = true;
                processingMessage.LLMSource = llmSource;
                OnOutputChanged($"No response received from {activeModel.Name}. The model may have executed successfully but produced no output.");
                OnStatusChanged($"Model {activeModel.Name} completed but returned no output");
            }

            processingMessage.OnPropertyChanged(nameof(processingMessage.ModelDisplayNameWithSourcePrefixed));
        }

        private string BuildChatHistoryForModel(ObservableCollection<ChatMessage> chatMessages)
        {
            if (chatMessages.Count == 0)
                return string.Empty;

            var historyBuilder = new StringBuilder();
            var allMessages = chatMessages
                .Where(msg => msg.IncludeInHistory && !msg.IsProcessing)
                .ToList();

            Debug.WriteLine($"BuildChatHistoryForModel: Total ChatMessages: {chatMessages.Count}, Filtered messages: {allMessages.Count}");

            const int maxMessages = 20;
            if (allMessages.Count > maxMessages)
            {
                allMessages = allMessages.Skip(allMessages.Count - maxMessages).ToList();
                historyBuilder.AppendLine("(Conversation history truncated to recent exchanges)");
                historyBuilder.AppendLine();
            }

            if (allMessages.Any())
            {
                for (int i = 0; i < allMessages.Count; i++)
                {
                    var msg = allMessages[i];
                    historyBuilder.AppendLine(msg.Content);

                    if (i < allMessages.Count - 1)
                    {
                        historyBuilder.AppendLine();
                    }
                }
            }

            return historyBuilder.ToString();
        }

        private void OnStatusChanged(string status)
        {
            StatusChanged?.Invoke(this, status);
        }

        private void OnOutputChanged(string output)
        {
            OutputChanged?.Invoke(this, output);
        }

        private void OnCommunicatingChanged(bool isCommunicating)
        {
            CommunicatingChanged?.Invoke(this, isCommunicating);
        }
    }
}
