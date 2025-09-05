using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CSimple.ViewModels;

namespace CSimple.Services
{
    public class AudioStepContentService : IDisposable
    {
        private readonly AudioPlaybackService _audioPlaybackService;
        private readonly WindowsTtsService _ttsService;
        private bool _disposed;

        public event Action PlaybackStarted;
        public event Action PlaybackStopped;
        public event Action<Exception> PlaybackError;

        public bool IsPlaying => _audioPlaybackService?.IsPlaying == true || _ttsService?.IsSpeaking == true;

        public AudioStepContentService()
        {
            _audioPlaybackService = new AudioPlaybackService();
            _audioPlaybackService.PlaybackStarted += OnAudioPlaybackStarted;
            _audioPlaybackService.PlaybackStopped += OnAudioPlaybackStopped;
            _audioPlaybackService.PlaybackError += OnAudioPlaybackError;

            // Initialize TTS service with better error handling
            try
            {
                _ttsService = new WindowsTtsService();
                _ttsService.SpeechStarted += OnTtsStarted;
                _ttsService.SpeechCompleted += OnTtsStopped;
                _ttsService.SpeechError += OnTtsError;
                Debug.WriteLine("[AudioStepContentService] TTS service initialized successfully");
            }
            catch (PlatformNotSupportedException ex)
            {
                Debug.WriteLine($"[AudioStepContentService] TTS not supported on this platform: {ex.Message}");
                _ttsService = null;
            }
            catch (InvalidOperationException ex)
            {
                Debug.WriteLine($"[AudioStepContentService] TTS service unavailable: {ex.Message}");
                _ttsService = null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AudioStepContentService] Failed to initialize TTS service: {ex.Message}");
                Debug.WriteLine($"[AudioStepContentService] TTS Exception details: {ex}");
                _ttsService = null;
                // Continue without TTS - audio playback will still work
            }
        }

        public async Task<bool> PlayStepContentAsync(string stepContent, string stepContentType, NodeViewModel selectedNode)
        {
            try
            {
                // Check if we have valid content to play
                if (string.IsNullOrEmpty(stepContent))
                {
                    Debug.WriteLine("No step content to play");
                    return false;
                }

                // Handle text content with TTS (for Action model nodes)
                if (stepContentType?.ToLowerInvariant() == "text")
                {
                    // Check if this is a placeholder message that shouldn't be read aloud
                    if (IsPlaceholderContent(stepContent))
                    {
                        Debug.WriteLine($"[AudioStepContentService] Skipping TTS for placeholder content: {stepContent.Substring(0, Math.Min(stepContent.Length, 100))}...");
                        return false;
                    }

                    if (_ttsService != null)
                    {
                        Debug.WriteLine($"[AudioStepContentService] Reading text aloud using TTS: {stepContent.Substring(0, Math.Min(stepContent.Length, 100))}...");

                        // For Action model nodes, announce the action being performed
                        string textToSpeak = stepContent;
                        if (selectedNode?.Classification?.ToLowerInvariant() == "action")
                        {
                            textToSpeak = $"Performing action: {stepContent}";
                            Debug.WriteLine($"[AudioStepContentService] Action model node detected - announcing action");
                        }

                        try
                        {
                            bool success = await _ttsService.SpeakTextAsync(textToSpeak);
                            if (!success)
                            {
                                Debug.WriteLine("Failed to start text-to-speech");
                            }
                            return success;
                        }
                        catch (Exception ttsEx)
                        {
                            Debug.WriteLine($"[AudioStepContentService] TTS error: {ttsEx.Message}");
                            PlaybackError?.Invoke(ttsEx);
                            return false;
                        }
                    }
                    else
                    {
                        Debug.WriteLine("TTS service not available - cannot read text aloud");
                        return false;
                    }
                }

                // Check if the step content is an audio file path
                if (stepContentType?.ToLowerInvariant() == "audio")
                {
                    string audioFilePath = stepContent;

                    // If the direct path doesn't exist, try to find the source audio file
                    if (!File.Exists(audioFilePath))
                    {
                        Debug.WriteLine($"Direct audio file path doesn't exist: {audioFilePath}");

                        // Try to find actual audio files in the same directory
                        if (selectedNode != null && selectedNode.DataType?.ToLower() == "audio")
                        {
                            audioFilePath = FindAlternativeAudioFile(audioFilePath);
                        }
                    }

                    if (File.Exists(audioFilePath))
                    {
                        Debug.WriteLine($"Playing audio file: {audioFilePath}");
                        bool success = await _audioPlaybackService.PlayAudioAsync(audioFilePath);
                        if (!success)
                        {
                            Debug.WriteLine("Failed to start audio playback");
                        }
                        return success;
                    }
                    else
                    {
                        Debug.WriteLine($"Audio file not found: {audioFilePath}");
                        return false;
                    }
                }

                // Unsupported content type
                Debug.WriteLine($"Step content type '{stepContentType}' is not supported for playback. Supported types: 'text', 'audio'");
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error playing audio: {ex.Message}");
                PlaybackError?.Invoke(ex);
                return false;
            }
        }

        public async Task<bool> StopAudioAsync()
        {
            try
            {
                Debug.WriteLine("Stopping audio playback and TTS");

                // Stop audio playback
                await _audioPlaybackService.StopAudioAsync();

                // Stop TTS if available
                if (_ttsService != null)
                {
                    await _ttsService.StopSpeechAsync();
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error stopping audio: {ex.Message}");
                PlaybackError?.Invoke(ex);
                return false;
            }
        }

        public bool CanPlayStepContent(string stepContent, string stepContentType)
        {
            bool result = false;
            string reason = "";

            // Check for valid content
            if (string.IsNullOrEmpty(stepContent))
            {
                reason = "StepContent is null or empty";
            }
            else if (string.IsNullOrEmpty(stepContentType))
            {
                reason = "StepContentType is null or empty";
            }
            else if (stepContentType.ToLowerInvariant() == "text")
            {
                // Check if this is placeholder content that shouldn't be played
                if (IsPlaceholderContent(stepContent))
                {
                    result = false;
                    reason = "Placeholder content - not generated output";
                }
                // Text content can be played via TTS if service is available
                else if (_ttsService != null)
                {
                    result = true;
                    reason = "Text content can be read aloud via TTS";
                }
                else
                {
                    result = false;
                    reason = "TTS service not available for text content";
                }
            }
            else if (stepContentType.ToLowerInvariant() == "audio")
            {
                // Check if it's a valid audio file path
                if (File.Exists(stepContent))
                {
                    result = true;
                    reason = "Direct audio file exists";
                }
                else
                {
                    // For audio segments that don't exist, check if we can find an alternative in the directory
                    try
                    {
                        string alternativeFile = FindAlternativeAudioFile(stepContent);
                        if (!string.IsNullOrEmpty(alternativeFile) && File.Exists(alternativeFile))
                        {
                            result = true;
                            reason = "Alternative audio file found in directory";
                        }
                        else
                        {
                            result = false;
                            reason = "No audio files found in directory";
                        }
                    }
                    catch
                    {
                        // Fallback: if we have audio content type, assume it might be playable
                        result = true;
                        reason = "Audio content type with unknown file status";
                    }
                }
            }
            else
            {
                result = false;
                reason = $"Unsupported content type: '{stepContentType}'. Supported types: 'text', 'audio'";
            }

            // Debug.WriteLine($"[CanPlayStepContent] Result: {result}, Reason: {reason}, StepContent: '{stepContent}', StepContentType: '{stepContentType}'");
            return result;
        }

        private string FindAlternativeAudioFile(string originalPath)
        {
            try
            {
                string directory = Path.GetDirectoryName(originalPath);
                if (Directory.Exists(directory))
                {
                    // Look for actual audio files (not segments) in the directory
                    var audioFiles = Directory.GetFiles(directory, "*.wav")
                        .Concat(Directory.GetFiles(directory, "*.mp3"))
                        .Concat(Directory.GetFiles(directory, "*.aac"))
                        .Where(f => !Path.GetFileName(f).StartsWith("Segment_"))
                        .OrderByDescending(f => File.GetCreationTime(f))
                        .ToList();

                    if (audioFiles.Any())
                    {
                        string alternativeFile = audioFiles.First();
                        Debug.WriteLine($"Using latest audio file from directory: {alternativeFile}");
                        return alternativeFile;
                    }
                    else
                    {
                        Debug.WriteLine($"No non-segment audio files found in directory: {directory}");
                    }
                }
                else
                {
                    Debug.WriteLine($"Directory does not exist: {directory}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error finding alternative audio file: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Determines if the content is a placeholder message that shouldn't be read aloud via TTS
        /// </summary>
        private bool IsPlaceholderContent(string content)
        {
            if (string.IsNullOrEmpty(content))
                return false;

            // Check for common placeholder patterns
            return content.Contains("No output generated yet") ||
                   content.Contains("Use 'Generate' or 'Run All Models'") ||
                   content.StartsWith("Model:") && content.Contains("No output generated");
        }

        private void OnAudioPlaybackStarted()
        {
            Debug.WriteLine("Audio playback started via AudioStepContentService");
            PlaybackStarted?.Invoke();
        }

        private void OnAudioPlaybackStopped()
        {
            Debug.WriteLine("Audio playback stopped via AudioStepContentService");
            PlaybackStopped?.Invoke();
        }

        private void OnAudioPlaybackError(Exception ex)
        {
            Debug.WriteLine($"Audio playback error via AudioStepContentService: {ex.Message}");
            PlaybackError?.Invoke(ex);
        }

        private void OnTtsStarted()
        {
            try
            {
                Debug.WriteLine("TTS started via AudioStepContentService");
                PlaybackStarted?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AudioStepContentService] Error in OnTtsStarted: {ex.Message}");
            }
        }

        private void OnTtsStopped()
        {
            try
            {
                Debug.WriteLine("TTS completed via AudioStepContentService");
                PlaybackStopped?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AudioStepContentService] Error in OnTtsStopped: {ex.Message}");
            }
        }

        private void OnTtsError(Exception ex)
        {
            try
            {
                Debug.WriteLine($"TTS error via AudioStepContentService: {ex.Message}");
                PlaybackError?.Invoke(ex);
            }
            catch (Exception handlerEx)
            {
                Debug.WriteLine($"[AudioStepContentService] Error in OnTtsError handler: {handlerEx.Message}");
            }
        }

        public async Task<bool> SaveAudioAsync(string stepContent, string stepContentType, NodeViewModel selectedNode)
        {
            try
            {
                // Check if we have valid audio content to save
                if (string.IsNullOrEmpty(stepContent) || stepContentType?.ToLowerInvariant() != "audio")
                {
                    Debug.WriteLine("No valid audio content to save");
                    return false;
                }

                string sourceAudioPath = stepContent;

                // If the direct path doesn't exist, try to find the source audio file
                if (!File.Exists(sourceAudioPath))
                {
                    sourceAudioPath = FindAlternativeAudioFile(sourceAudioPath);
                    if (string.IsNullOrEmpty(sourceAudioPath) || !File.Exists(sourceAudioPath))
                    {
                        Debug.WriteLine($"Source audio file not found: {stepContent}");
                        return false;
                    }
                }

                // Determine the save path
                string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string audioSaveDir = Path.Combine(documentsPath, "CSimple", "Audio", "Saved");
                Directory.CreateDirectory(audioSaveDir);

                // Create a meaningful filename
                string nodeName = selectedNode?.Name ?? "Unknown";
                string safeNodeName = string.Join("_", nodeName.Split(Path.GetInvalidFileNameChars()));
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string extension = Path.GetExtension(sourceAudioPath);
                string fileName = $"{safeNodeName}_{timestamp}{extension}";
                string destinationPath = Path.Combine(audioSaveDir, fileName);

                // Copy the audio file to the save location
                await Task.Run(() => File.Copy(sourceAudioPath, destinationPath, true));

                Debug.WriteLine($"Audio saved successfully to: {destinationPath}");

                // You might want to show a success message to the user here
                // This could be done by raising an event or returning the saved path

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving audio: {ex.Message}");
                return false;
            }
        }

        public bool CanSaveAudio(string stepContent, string stepContentType)
        {
            // Can save audio if we can play it (same validation logic)
            if (string.IsNullOrEmpty(stepContent) || string.IsNullOrEmpty(stepContentType))
            {
                return false;
            }

            if (stepContentType.ToLowerInvariant() != "audio")
            {
                return false;
            }

            // Check if audio file exists directly or can be found
            if (File.Exists(stepContent))
            {
                return true;
            }

            // Check if we can find an alternative audio file
            try
            {
                string alternativeFile = FindAlternativeAudioFile(stepContent);
                return !string.IsNullOrEmpty(alternativeFile) && File.Exists(alternativeFile);
            }
            catch
            {
                return false;
            }
        }

        public string GetAudioInfo(string stepContent, string stepContentType)
        {
            try
            {
                if (string.IsNullOrEmpty(stepContent) || stepContentType?.ToLowerInvariant() != "audio")
                {
                    return null;
                }

                string audioFilePath = stepContent;

                // If the direct path doesn't exist, try to find the source audio file
                if (!File.Exists(audioFilePath))
                {
                    audioFilePath = FindAlternativeAudioFile(audioFilePath);
                    if (string.IsNullOrEmpty(audioFilePath))
                    {
                        return null;
                    }
                }

                if (File.Exists(audioFilePath))
                {
                    var fileInfo = new FileInfo(audioFilePath);
                    var extension = fileInfo.Extension.ToUpperInvariant().TrimStart('.');
                    var sizeKB = Math.Round(fileInfo.Length / 1024.0, 1);

                    // For now, return basic file info. You could enhance this with actual audio duration
                    // using a library like NAudio or MediaFoundation
                    return $"{extension} • {sizeKB} KB • {fileInfo.LastWriteTime:MMM d, yyyy}";
                }

                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting audio info: {ex.Message}");
                return null;
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                if (_audioPlaybackService != null)
                {
                    _audioPlaybackService.PlaybackStarted -= OnAudioPlaybackStarted;
                    _audioPlaybackService.PlaybackStopped -= OnAudioPlaybackStopped;
                    _audioPlaybackService.PlaybackError -= OnAudioPlaybackError;
                    _audioPlaybackService.Dispose();
                }

                if (_ttsService != null)
                {
                    _ttsService.SpeechStarted -= OnTtsStarted;
                    _ttsService.SpeechCompleted -= OnTtsStopped;
                    _ttsService.SpeechError -= OnTtsError;
                    _ttsService.Dispose();
                }

                _disposed = true;
            }
        }
    }
}
