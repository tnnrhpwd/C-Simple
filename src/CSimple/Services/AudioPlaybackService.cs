using System;
using System.IO;
using System.Threading.Tasks;
using NAudio.Wave;
using System.Diagnostics;

namespace CSimple.Services
{
    public class AudioPlaybackService : IDisposable
    {
        private WaveOutEvent _waveOut;
        private AudioFileReader _audioFileReader;
        private bool _isPlaying;
        private bool _disposed;

        public event Action PlaybackStarted;
        public event Action PlaybackStopped;
        public event Action<Exception> PlaybackError;

        public bool IsPlaying => _isPlaying && _waveOut?.PlaybackState == PlaybackState.Playing;

        public async Task<bool> PlayAudioAsync(string filePath)
        {
            try
            {
                // Stop any currently playing audio
                await StopAudioAsync();

                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                {
                    Debug.WriteLine($"[AudioPlaybackService] Audio file not found: {filePath}");
                    return false;
                }

                Debug.WriteLine($"[AudioPlaybackService] Starting playback of: {filePath}");

                // Initialize audio components
                _audioFileReader = new AudioFileReader(filePath);
                _waveOut = new WaveOutEvent();

                // Wire up events
                _waveOut.PlaybackStopped += OnPlaybackStopped;

                // Initialize and start playback
                _waveOut.Init(_audioFileReader);
                _waveOut.Play();

                _isPlaying = true;
                PlaybackStarted?.Invoke();

                Debug.WriteLine($"[AudioPlaybackService] Playback started successfully");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AudioPlaybackService] Error starting playback: {ex.Message}");
                PlaybackError?.Invoke(ex);
                await StopAudioAsync();
                return false;
            }
        }

        public async Task StopAudioAsync()
        {
            try
            {
                if (_waveOut != null)
                {
                    Debug.WriteLine($"[AudioPlaybackService] Stopping playback");

                    _waveOut.PlaybackStopped -= OnPlaybackStopped;
                    _waveOut.Stop();
                    _waveOut.Dispose();
                    _waveOut = null;
                }

                if (_audioFileReader != null)
                {
                    _audioFileReader.Dispose();
                    _audioFileReader = null;
                }

                _isPlaying = false;
                PlaybackStopped?.Invoke();

                Debug.WriteLine($"[AudioPlaybackService] Playback stopped");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AudioPlaybackService] Error stopping playback: {ex.Message}");
                PlaybackError?.Invoke(ex);
            }

            await Task.CompletedTask;
        }

        private void OnPlaybackStopped(object sender, StoppedEventArgs e)
        {
            Debug.WriteLine($"[AudioPlaybackService] Playback stopped event received");

            _isPlaying = false;
            PlaybackStopped?.Invoke();

            if (e.Exception != null)
            {
                Debug.WriteLine($"[AudioPlaybackService] Playback stopped due to error: {e.Exception.Message}");
                PlaybackError?.Invoke(e.Exception);
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                StopAudioAsync().Wait();
                _disposed = true;
            }
        }
    }
}
