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
                _synthesizer = new SpeechSynthesizer();
                _mediaPlayer = new MediaPlayer();
                _mediaPlayer.MediaEnded += OnMediaEnded;
                _mediaPlayer.MediaOpened += OnMediaOpened;
                _mediaPlayer.MediaFailed += OnMediaFailed;

                Debug.WriteLine("[WindowsTtsService] Initialized with Windows Runtime Speech Synthesis");
#else
                Debug.WriteLine("[WindowsTtsService] Warning: Windows TTS is only available on Windows platform");
#endif
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WindowsTtsService] Failed to initialize: {ex.Message}");
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
                var speechStream = await _synthesizer.SynthesizeTextToStreamAsync(text);

                // Create media source and play
                var mediaSource = MediaSource.CreateFromStream(speechStream, speechStream.ContentType);
                _mediaPlayer.Source = mediaSource;

                IsSpeaking = true;
                _mediaPlayer.Play();

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WindowsTtsService] Error speaking text: {ex.Message}");
                SpeechError?.Invoke(ex);
                IsSpeaking = false;
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
            Debug.WriteLine("[WindowsTtsService] Speech started");
            SpeechStarted?.Invoke();
        }

        private void OnMediaEnded(MediaPlayer sender, object args)
        {
            Debug.WriteLine("[WindowsTtsService] Speech completed");
            IsSpeaking = false;
            SpeechCompleted?.Invoke();
        }

        private void OnMediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
        {
            Debug.WriteLine($"[WindowsTtsService] Speech failed: {args.ErrorMessage}");
            IsSpeaking = false;
            SpeechError?.Invoke(new Exception(args.ErrorMessage));
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
