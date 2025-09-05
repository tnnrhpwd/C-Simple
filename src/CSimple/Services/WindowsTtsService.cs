using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

#if WINDOWS
using Windows.Media.SpeechSynthesis;
using Windows.Storage.Streams;
using Windows.Media.Playback;
using Windows.Media.Core;
#endif

namespace CSimple.Services
{
    /// <summary>
    /// Simple Windows Text-to-Speech service using Windows Runtime Speech APIs
    /// Provides an easy way to read text aloud without requiring complex neural network models
    /// </summary>
    public class WindowsTtsService : IDisposable
    {
#if WINDOWS
        private readonly SpeechSynthesizer _synthesizer;
        private readonly MediaPlayer _mediaPlayer;
#endif
        private bool _disposed;

        public event Action SpeechStarted;
        public event Action SpeechCompleted;
        public event Action<Exception> SpeechError;

        public bool IsSpeaking { get; private set; }

        public WindowsTtsService()
        {
            try
            {
#if WINDOWS
                try
                {
                    _synthesizer = new SpeechSynthesizer();
                    Debug.WriteLine("[WindowsTtsService] SpeechSynthesizer initialized successfully");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[WindowsTtsService] Failed to initialize SpeechSynthesizer: {ex.Message}");
                    throw new InvalidOperationException("Failed to initialize Speech Synthesizer", ex);
                }

                try
                {
                    _mediaPlayer = new MediaPlayer();
                    _mediaPlayer.MediaEnded += OnMediaEnded;
                    _mediaPlayer.MediaOpened += OnMediaOpened;
                    _mediaPlayer.MediaFailed += OnMediaFailed;
                    Debug.WriteLine("[WindowsTtsService] MediaPlayer initialized successfully");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[WindowsTtsService] Failed to initialize MediaPlayer: {ex.Message}");

                    // Clean up synthesizer if media player fails
                    try
                    {
                        _synthesizer?.Dispose();
                    }
                    catch (Exception cleanupEx)
                    {
                        Debug.WriteLine($"[WindowsTtsService] Error during cleanup: {cleanupEx.Message}");
                    }

                    throw new InvalidOperationException("Failed to initialize Media Player", ex);
                }

                Debug.WriteLine("[WindowsTtsService] Initialized with Windows Runtime Speech Synthesis");
#else
                Debug.WriteLine("[WindowsTtsService] Warning: Windows TTS is only available on Windows platform");
                throw new PlatformNotSupportedException("Windows TTS is only available on Windows platform");
#endif
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WindowsTtsService] Failed to initialize: {ex.Message}");
                Debug.WriteLine($"[WindowsTtsService] Exception details: {ex}");
                throw new InvalidOperationException("Windows Speech Synthesis is not available on this system", ex);
            }
        }

        /// <summary>
        /// Speaks the provided text asynchronously
        /// </summary>
        /// <param name="text">Text to speak</param>
        /// <returns>True if speech started successfully</returns>
        public async Task<bool> SpeakTextAsync(string text)
        {
            if (_disposed)
            {
                Debug.WriteLine("[WindowsTtsService] Service is disposed");
                return false;
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                Debug.WriteLine("[WindowsTtsService] No text provided to speak");
                return false;
            }

#if WINDOWS
            try
            {
                // Stop any current speech
                if (IsSpeaking)
                {
                    await StopSpeechAsync();
                }

                Debug.WriteLine($"[WindowsTtsService] Speaking text: {text.Substring(0, Math.Min(text.Length, 100))}...");

                // Generate speech stream
                SpeechSynthesisStream speechStream = null;
                try
                {
                    speechStream = await _synthesizer.SynthesizeTextToStreamAsync(text);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[WindowsTtsService] Error synthesizing text: {ex.Message}");
                    SpeechError?.Invoke(ex);
                    return false;
                }

                if (speechStream == null)
                {
                    Debug.WriteLine("[WindowsTtsService] Failed to generate speech stream");
                    return false;
                }

                // Create media source and play
                MediaSource mediaSource = null;
                try
                {
                    mediaSource = MediaSource.CreateFromStream(speechStream, speechStream.ContentType);
                    _mediaPlayer.Source = mediaSource;

                    IsSpeaking = true;
                    _mediaPlayer.Play();

                    return true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[WindowsTtsService] Error setting up media playback: {ex.Message}");
                    IsSpeaking = false;

                    // Clean up resources
                    try
                    {
                        speechStream?.Dispose();
                        mediaSource?.Dispose();
                    }
                    catch (Exception cleanupEx)
                    {
                        Debug.WriteLine($"[WindowsTtsService] Error during cleanup: {cleanupEx.Message}");
                    }

                    SpeechError?.Invoke(ex);
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WindowsTtsService] Unexpected error speaking text: {ex.Message}");
                Debug.WriteLine($"[WindowsTtsService] Exception details: {ex}");
                IsSpeaking = false;

                // Safely invoke error event
                try
                {
                    SpeechError?.Invoke(ex);
                }
                catch (Exception eventEx)
                {
                    Debug.WriteLine($"[WindowsTtsService] Error invoking SpeechError event: {eventEx.Message}");
                }

                return false;
            }
#else
            Debug.WriteLine("[WindowsTtsService] Windows TTS not available on this platform");
            return false;
#endif
        }

        /// <summary>
        /// Stops current speech
        /// </summary>
        public async Task StopSpeechAsync()
        {
            if (_disposed) return;

#if WINDOWS
            try
            {
                if (IsSpeaking)
                {
                    Debug.WriteLine("[WindowsTtsService] Stopping speech");
                    _mediaPlayer.Pause();
                    IsSpeaking = false;

                    // Wait a brief moment for stop to complete
                    await Task.Delay(100);

                    SpeechCompleted?.Invoke();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WindowsTtsService] Error stopping speech: {ex.Message}");
                SpeechError?.Invoke(ex);
            }
#endif
        }

        /// <summary>
        /// Sets the speech rate (speed) - Note: Windows Runtime doesn't support rate adjustment
        /// </summary>
        /// <param name="rate">Rate from -10 (slowest) to 10 (fastest), 0 is normal</param>
        public void SetSpeechRate(int rate)
        {
            Debug.WriteLine($"[WindowsTtsService] Speech rate adjustment not supported in Windows Runtime TTS");
        }

        /// <summary>
        /// Sets the speech volume - Note: Use system volume control instead
        /// </summary>
        /// <param name="volume">Volume from 0 (silent) to 100 (loudest)</param>
        public void SetSpeechVolume(int volume)
        {
#if WINDOWS
            try
            {
                // Windows Runtime MediaPlayer volume is 0.0 to 1.0
                double normalizedVolume = Math.Max(0.0, Math.Min(1.0, volume / 100.0));
                _mediaPlayer.Volume = normalizedVolume;
                Debug.WriteLine($"[WindowsTtsService] Speech volume set to: {volume}% ({normalizedVolume})");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WindowsTtsService] Error setting speech volume: {ex.Message}");
            }
#endif
        }

        /// <summary>
        /// Gets available voice names on the system
        /// </summary>
        public string[] GetAvailableVoices()
        {
#if WINDOWS
            try
            {
                var voices = new List<string>();
                var installedVoices = SpeechSynthesizer.AllVoices;

                foreach (var voice in installedVoices)
                {
                    voices.Add(voice.DisplayName);
                }

                return voices.ToArray();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WindowsTtsService] Error getting available voices: {ex.Message}");
                return new string[0];
            }
#else
            return new string[0];
#endif
        }

        /// <summary>
        /// Sets the voice to use for speech
        /// </summary>
        /// <param name="voiceName">Display name of the voice to use</param>
        public bool SetVoice(string voiceName)
        {
#if WINDOWS
            if (_disposed || string.IsNullOrEmpty(voiceName)) return false;

            try
            {
                var voices = SpeechSynthesizer.AllVoices;
                var selectedVoice = voices.FirstOrDefault(v => v.DisplayName == voiceName);

                if (selectedVoice != null)
                {
                    _synthesizer.Voice = selectedVoice;
                    Debug.WriteLine($"[WindowsTtsService] Voice set to: {voiceName}");
                    return true;
                }
                else
                {
                    Debug.WriteLine($"[WindowsTtsService] Voice '{voiceName}' not found");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WindowsTtsService] Error setting voice to '{voiceName}': {ex.Message}");
                return false;
            }
#else
            return false;
#endif
        }

        private void OnMediaOpened(MediaPlayer sender, object args)
        {
            try
            {
                Debug.WriteLine("[WindowsTtsService] Speech started");

                // Safely invoke the event
                try
                {
                    SpeechStarted?.Invoke();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[WindowsTtsService] Error in SpeechStarted event: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WindowsTtsService] Error in OnMediaOpened: {ex.Message}");
            }
        }

        private void OnMediaEnded(MediaPlayer sender, object args)
        {
            try
            {
                Debug.WriteLine("[WindowsTtsService] Speech completed");
                IsSpeaking = false;

                // Safely invoke the event
                try
                {
                    SpeechCompleted?.Invoke();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[WindowsTtsService] Error in SpeechCompleted event: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WindowsTtsService] Error in OnMediaEnded: {ex.Message}");
            }
        }

        private void OnMediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
        {
            try
            {
                Debug.WriteLine($"[WindowsTtsService] Speech failed: {args.ErrorMessage}");
                IsSpeaking = false;

                // Safely invoke the event
                try
                {
                    SpeechError?.Invoke(new Exception(args.ErrorMessage));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[WindowsTtsService] Error in SpeechError event: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WindowsTtsService] Error in OnMediaFailed: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                try
                {
#if WINDOWS
                    if (_mediaPlayer != null)
                    {
                        if (IsSpeaking)
                        {
                            _mediaPlayer.Pause();
                        }

                        _mediaPlayer.MediaEnded -= OnMediaEnded;
                        _mediaPlayer.MediaOpened -= OnMediaOpened;
                        _mediaPlayer.MediaFailed -= OnMediaFailed;
                        _mediaPlayer.Dispose();
                    }

                    _synthesizer?.Dispose();
#endif
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[WindowsTtsService] Error during disposal: {ex.Message}");
                }

                _disposed = true;
                IsSpeaking = false;
            }
        }
    }
}
