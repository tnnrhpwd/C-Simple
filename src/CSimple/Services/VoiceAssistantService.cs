using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using CSimple.Models;
using Microsoft.Maui.Dispatching;
using System.Text.RegularExpressions;

namespace CSimple.Services
{
    public class VoiceAssistantService : IDisposable
    {
        #region Events
        public event Action<string> TranscriptionCompleted;
        public event Action<string> CommandRecognized;
        public event Action<string> DebugMessageLogged;
        public event Action<float> AudioLevelChanged;
        public event Action<bool> ListeningStateChanged;
        public event Action<string, bool> ActionExecuted;
        #endregion

        #region Services
        private readonly AudioCaptureService _audioCaptureService;
        private readonly ActionService _actionService;
        private readonly AppModeService.AppModeService _appModeService;
        private readonly InputCaptureService _inputCaptureService;
        #endregion

        #region Properties
        private bool _isListening = false;
        private bool _isProcessing = false;
        private CancellationTokenSource _listeningCts;
        private CancellationTokenSource _processingCts;
        private MemoryStream _audioBuffer;
        private WaveFileWriter _waveWriter;
        private string _currentAudioPath;
        private ISpeechRecognitionService _speechRecognitionService;
        private bool _useOnlineServices => _appModeService?.CurrentMode == AppModeService.AppMode.Online;
        private bool _isEnabled = false;
        private const int SILENCE_THRESHOLD_DB = -50;
        private const int SILENCE_DURATION_MS = 1500;
        private readonly object _lockObject = new object();
        private DateTime _lastSignificantAudioTime = DateTime.MinValue;
        #endregion

        public VoiceAssistantService(
            AudioCaptureService audioCaptureService,
            ActionService actionService,
            AppModeService.AppModeService appModeService,
            InputCaptureService inputCaptureService)
        {
            _audioCaptureService = audioCaptureService;
            _actionService = actionService;
            _appModeService = appModeService;
            _inputCaptureService = inputCaptureService;

            // Subscribe to level changes for voice activity detection
            _audioCaptureService.PCLevelChanged += OnAudioLevelChanged;

            // Initialize the buffer
            _audioBuffer = new MemoryStream();
        }

        public void ToggleEnabled(bool isEnabled)
        {
            _isEnabled = isEnabled;
            if (!_isEnabled && _isListening)
            {
                StopListening();
            }
            Debug.Print($"Voice Assistant {(_isEnabled ? "Enabled" : "Disabled")}");
        }

        public void StartListening()
        {
            if (!_isEnabled || _isListening) return;

            lock (_lockObject)
            {
                if (_isListening) return;

                _isListening = true;
                _listeningCts = new CancellationTokenSource();
                ListeningStateChanged?.Invoke(true);

                Debug.Print("Voice Assistant started listening");

                // Start recording audio
                _audioBuffer = new MemoryStream();
                _currentAudioPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "CSimple", "Resources", "VoiceCommands",
                    $"VoiceCommand_{DateTime.Now:yyyyMMdd_HHmmss}.wav");

                Directory.CreateDirectory(Path.GetDirectoryName(_currentAudioPath));

                // We'll use the PC audio service since it's already configured
                _audioCaptureService.StartPCAudioRecording(false);

                // Start a timer to check for silence and end recording
                Task.Run(() => MonitorAudioForSilence(_listeningCts.Token));
            }
        }

        public void StopListening()
        {
            if (!_isListening) return;

            lock (_lockObject)
            {
                if (!_isListening) return;

                _isListening = false;
                _listeningCts?.Cancel();
                ListeningStateChanged?.Invoke(false);

                Debug.Print("Voice Assistant stopped listening");

                // Stop recording and process the audio
                _audioCaptureService.StopPCAudioRecording();

                if (_audioBuffer.Length > 0)
                {
                    ProcessAudioAsync();
                }
                else
                {
                    Debug.Print("No audio captured to process");
                }
            }
        }

        private async Task MonitorAudioForSilence(CancellationToken token)
        {
            try
            {
                _lastSignificantAudioTime = DateTime.Now;

                while (!token.IsCancellationRequested)
                {
                    // If we've had silence for the specified duration, stop listening
                    if ((DateTime.Now - _lastSignificantAudioTime).TotalMilliseconds > SILENCE_DURATION_MS)
                    {
                        Debug.Print("Silence detected, stopping listening");
                        StopListening();
                        break;
                    }

                    await Task.Delay(100, token); // Check every 100ms
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
            }
            catch (Exception ex)
            {
                Debug.Print($"Error in silence detection: {ex.Message}");
            }
        }

        private void OnAudioLevelChanged(float level)
        {
            // Convert linear level to dB for threshold comparison
            float dbLevel = level > 0 ? 20 * (float)Math.Log10(level) : -100;

            // Pass the level to subscribers
            AudioLevelChanged?.Invoke(level);

            // Update last activity time if the audio is above threshold
            if (dbLevel > SILENCE_THRESHOLD_DB)
            {
                _lastSignificantAudioTime = DateTime.Now;
            }

            // Also capture the audio for processing
            if (_isListening)
            {
                // Audio data capture would happen inside AudioCaptureService
                // We're using the events to detect when to stop listening
            }
        }

        private async Task ProcessAudioAsync()
        {
            if (_isProcessing) return;

            try
            {
                _isProcessing = true;
                _processingCts = new CancellationTokenSource();

                Debug.Print("Processing audio...");

                // Process the audio
                string recognizedText = await RecognizeSpeechAsync(_currentAudioPath);

                if (!string.IsNullOrEmpty(recognizedText))
                {
                    Debug.Print($"Recognized: {recognizedText}");
                    TranscriptionCompleted?.Invoke(recognizedText);

                    // Process the command
                    await ProcessCommandAsync(recognizedText);
                }
                else
                {
                    Debug.Print("No speech recognized");
                }
            }
            catch (Exception ex)
            {
                Debug.Print($"Error processing audio: {ex.Message}");
            }
            finally
            {
                _isProcessing = false;
                _processingCts = null;
            }
        }

        private async Task<string> RecognizeSpeechAsync(string audioPath)
        {
            try
            {
                // Determine which speech recognition service to use
                if (_speechRecognitionService == null)
                {
                    _speechRecognitionService = _useOnlineServices
                        ? new OnlineSpeechRecognitionService()
                        : new LocalSpeechRecognitionService();
                }

                // Recognize speech
                return await _speechRecognitionService.RecognizeSpeechAsync(audioPath);
            }
            catch (Exception ex)
            {
                Debug.Print($"Speech recognition error: {ex.Message}");
                return string.Empty;
            }
        }

        private async Task ProcessCommandAsync(string text)
        {
            try
            {
                Debug.Print($"Processing command: {text}");
                CommandRecognized?.Invoke(text);

                // Basic command recognition - in a more advanced implementation,
                // you would use a proper NLU service
                string command = text.ToLower().Trim();

                // Command matching using regex patterns for flexibility
                if (MatchesPattern(command, @"(open|launch|start|run|show)\s+(the\s+)?(?<app>.+)"))
                {
                    // Launch application command
                    string appName = ExtractNamedGroup(command, @"(open|launch|start|run|show)\s+(the\s+)?(?<app>.+)", "app");
                    await ExecuteActionAsync($"launch {appName}", "app-launch");
                }
                else if (MatchesPattern(command, @"(record|capture|start recording|begin recording)(\s+a)?(\s+new)?\s+(action|step|task|macro)"))
                {
                    // Record new action command
                    await ExecuteActionAsync("navigate to record page", "navigate-observe");
                }
                else if (MatchesPattern(command, @"(click|press|tap)(\s+on)?(\s+the)?\s+(?<element>.+)"))
                {
                    // Click on element command
                    string element = ExtractNamedGroup(command, @"(click|press|tap)(\s+on)?(\s+the)?\s+(?<element>.+)", "element");
                    await ExecuteActionAsync($"click {element}", "mouse-click");
                }
                else if (MatchesPattern(command, @"(type|enter|input)(\s+the)?(\s+text)?(\s+"")?(?<text>[^""]+)("")?"))
                {
                    // Type text command
                    string textToType = ExtractNamedGroup(command, @"(type|enter|input)(\s+the)?(\s+text)?(\s+"")?(?<text>[^""]+)("")?", "text");
                    await ExecuteActionAsync($"type {textToType}", "keyboard-input");
                }
                else if (MatchesPattern(command, @"(scroll|move)\s+(up|down|left|right)"))
                {
                    // Scroll command
                    string direction = ExtractNamedGroup(command, @"(scroll|move)\s+(?<direction>up|down|left|right)", "direction");
                    await ExecuteActionAsync($"scroll {direction}", "mouse-scroll");
                }
                else if (MatchesPattern(command, @"(create|make|add)(\s+a)?(\s+new)?\s+(goal|task|objective)"))
                {
                    // Create goal command
                    await ExecuteActionAsync("navigate to goal page", "navigate-goal");
                }
                else if (MatchesPattern(command, @"(search|find|look up|google|search for|search google for)\s+(?<query>.+)"))
                {
                    // Search command
                    string query = ExtractNamedGroup(command, @"(search|find|look up|google|search for|search google for)\s+(?<query>.+)", "query");
                    await ExecuteActionAsync($"search for {query}", "web-search");
                }
                else if (MatchesPattern(command, @"(stop|cancel|halt|terminate|end)(\s+listening|\s+recording|\s+assistant)?"))
                {
                    // Stop command
                    ToggleEnabled(false);
                    ActionExecuted?.Invoke("Voice assistant disabled", true);
                }
                else
                {
                    Debug.Print("Command not recognized");
                    ActionExecuted?.Invoke("Command not recognized", false);
                }
            }
            catch (Exception ex)
            {
                Debug.Print($"Error processing command: {ex.Message}");
                ActionExecuted?.Invoke($"Error: {ex.Message}", false);
            }
        }

        private bool MatchesPattern(string input, string pattern)
        {
            return Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase);
        }

        private string ExtractNamedGroup(string input, string pattern, string groupName)
        {
            var match = Regex.Match(input, pattern, RegexOptions.IgnoreCase);
            if (match.Success && match.Groups[groupName].Success)
            {
                return match.Groups[groupName].Value.Trim();
            }
            return string.Empty;
        }

        private async Task ExecuteActionAsync(string actionDescription, string actionType)
        {
            Debug.Print($"Executing action: {actionDescription} (Type: {actionType})");

            bool success = false;

            try
            {
                switch (actionType)
                {
                    case "navigate-observe":
                        await Shell.Current.GoToAsync("///observe");
                        success = true;
                        break;

                    case "navigate-goal":
                        await Shell.Current.GoToAsync("///goal");
                        success = true;
                        break;

                    case "mouse-click":
                        // This would use InputSimulator to perform a click
                        // Placeholder for now, would need more context for specific clicks
                        success = true;
                        break;

                    case "keyboard-input":
                        // Extract the text to type from actionDescription
                        string textToType = actionDescription.Substring("type ".Length);
                        // In a real implementation, this would use InputSimulator to type the text
                        // For now, just log it
                        Debug.Print($"Would type: {textToType}");
                        success = true;
                        break;

                    case "app-launch":
                        string appName = actionDescription.Substring("launch ".Length).Trim();
                        // In a real implementation, this would launch the app
                        // For now, just log it
                        Debug.Print($"Would launch app: {appName}");
                        success = true;
                        break;

                    case "web-search":
                        string query = actionDescription.Substring("search for ".Length);
                        // In a real implementation, this would launch the browser and search
                        // For now, just log it
                        Debug.Print($"Would search for: {query}");
                        success = true;
                        break;

                    case "mouse-scroll":
                        string direction = actionDescription.Substring("scroll ".Length);
                        Debug.Print($"Would scroll: {direction}");
                        success = true;
                        break;

                    default:
                        Debug.Print($"Unknown action type: {actionType}");
                        break;
                }

                ActionExecuted?.Invoke(actionDescription, success);
            }
            catch (Exception ex)
            {
                Debug.Print($"Error executing action: {ex.Message}");
                ActionExecuted?.Invoke($"Error: {ex.Message}", false);
            }
        }

        public void Dispose()
        {
            _listeningCts?.Cancel();
            _listeningCts?.Dispose();
            _processingCts?.Cancel();
            _processingCts?.Dispose();
            _waveWriter?.Dispose();
            _audioBuffer?.Dispose();
        }
    }

    // Interface for speech recognition services
    public interface ISpeechRecognitionService
    {
        Task<string> RecognizeSpeechAsync(string audioPath);
    }

    // Online speech recognition service (uses cloud APIs)
    public class OnlineSpeechRecognitionService : ISpeechRecognitionService
    {
        public async Task<string> RecognizeSpeechAsync(string audioPath)
        {
            // In a real implementation, this would call a speech recognition API
            // For now, we'll simulate a response
            await Task.Delay(1000); // Simulate API call delay

            // Return a simulated result
            return "This is a simulated online speech recognition result";
        }
    }

    // Local speech recognition service (runs on device)
    public class LocalSpeechRecognitionService : ISpeechRecognitionService
    {
        public async Task<string> RecognizeSpeechAsync(string audioPath)
        {
            // In a real implementation, this would use a local speech recognition library
            // For now, we'll simulate a response
            await Task.Delay(500); // Local processing is usually faster

            // Return a simulated result
            return "This is a simulated local speech recognition result";
        }
    }
}
