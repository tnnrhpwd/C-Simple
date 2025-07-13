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
        private bool _disposed;

        public event Action PlaybackStarted;
        public event Action PlaybackStopped;
        public event Action<Exception> PlaybackError;

        public bool IsPlaying => _audioPlaybackService?.IsPlaying == true;

        public AudioStepContentService()
        {
            _audioPlaybackService = new AudioPlaybackService();
            _audioPlaybackService.PlaybackStarted += OnAudioPlaybackStarted;
            _audioPlaybackService.PlaybackStopped += OnAudioPlaybackStopped;
            _audioPlaybackService.PlaybackError += OnAudioPlaybackError;
        }

        public async Task<bool> PlayStepContentAsync(string stepContent, string stepContentType, NodeViewModel selectedNode)
        {
            try
            {
                // Check if we have valid audio content to play
                if (string.IsNullOrEmpty(stepContent))
                {
                    Debug.WriteLine("No step content to play");
                    return false;
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
                else
                {
                    Debug.WriteLine($"Step content is not audio type. Type: {stepContentType}, Content: {stepContent}");
                    return false;
                }
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
                Debug.WriteLine("Stopping audio playback");
                await _audioPlaybackService.StopAudioAsync();
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

            // More robust check for audio playability
            if (string.IsNullOrEmpty(stepContent))
            {
                reason = "StepContent is null or empty";
            }
            else if (string.IsNullOrEmpty(stepContentType))
            {
                reason = "StepContentType is null or empty";
            }
            else if (stepContentType.ToLowerInvariant() != "audio")
            {
                reason = $"StepContentType is '{stepContentType}', not 'audio'";
            }
            else
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

            Debug.WriteLine($"[CanPlayStepContent] Result: {result}, Reason: {reason}, StepContent: '{stepContent}', StepContentType: '{stepContentType}'");
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
                _disposed = true;
            }
        }
    }
}
