using System;
using System.IO;
using System.Threading.Tasks;
using NAudio.Wave;
using NWaves.FeatureExtractors;
using NWaves.FeatureExtractors.Options;
using NAudio.Wave.SampleProviders;
using System.Collections.Generic;

namespace CSimple.Services
{
    public class AudioCaptureService
    {
        #region Events
        public event Action<string> DebugMessageLogged;
        public event Action<string> FileCaptured;
        public event Action<float> PCLevelChanged;
        public event Action<float> WebcamLevelChanged;
        #endregion

        #region Properties
        private WaveInEvent _waveIn;
        private WaveFileWriter _writer;
        private WasapiLoopbackCapture _loopbackCapture;
        private WaveFileWriter _loopbackWriter;
        private string _pcAudioDirectory;
        private string _webcamAudioDirectory;
        private bool _savePCAudio;
        private bool _saveWebcamAudio;
        private string _tempPcFilePath;
        private string _tempWebcamFilePath;
        #endregion

        #region Voice Processing Features
        private const float NOISE_GATE_THRESHOLD = 0.05f;
        private const float VOICE_BOOST_FACTOR = 2.0f;
        private readonly Queue<float> _levelHistory = new Queue<float>();
        private const int LEVEL_HISTORY_SIZE = 20;
        #endregion

        public AudioCaptureService()
        {
            InitDirectories();
        }

        private void InitDirectories()
        {
            // Create directories for audio captures
            _pcAudioDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CSimple", "AudioCaptures", "PC");
            _webcamAudioDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CSimple", "AudioCaptures", "Webcam");

            Directory.CreateDirectory(_pcAudioDirectory);
            Directory.CreateDirectory(_webcamAudioDirectory);

            LogDebug($"Audio directories initialized: {_pcAudioDirectory}, {_webcamAudioDirectory}");
        }

        public void StartPCAudioRecording(bool saveRecording = true)
        {
            try
            {
                _savePCAudio = saveRecording;
                _tempPcFilePath = Path.Combine(_pcAudioDirectory, $"PCAudio_{DateTime.Now:yyyyMMdd_HHmmss}.wav");

                _loopbackCapture = new WasapiLoopbackCapture();
                _loopbackWriter = new WaveFileWriter(_tempPcFilePath, _loopbackCapture.WaveFormat);

                _loopbackCapture.DataAvailable += (s, a) =>
                {
                    _loopbackWriter.Write(a.Buffer, 0, a.BytesRecorded);

                    // Calculate audio level
                    float level = CalculateLevel(a.Buffer, a.BytesRecorded);
                    PCLevelChanged?.Invoke(level);
                };

                _loopbackCapture.RecordingStopped += (s, a) =>
                {
                    _loopbackWriter?.Dispose();
                    _loopbackWriter = null;
                    _loopbackCapture.Dispose();

                    if (_savePCAudio)
                    {
                        LogDebug($"PC audio recording saved to: {_tempPcFilePath}");
                        FileCaptured?.Invoke(_tempPcFilePath);
                        ExtractMFCCs(_tempPcFilePath);
                    }
                    else
                    {
                        // Delete the file if we're not supposed to save it
                        if (File.Exists(_tempPcFilePath))
                        {
                            try
                            {
                                File.Delete(_tempPcFilePath);
                                LogDebug("PC audio recording discarded");
                            }
                            catch (Exception ex)
                            {
                                LogDebug($"Error deleting temporary audio file: {ex.Message}");
                            }
                        }
                    }

                    // Reset audio level
                    PCLevelChanged?.Invoke(0);
                };

                _loopbackCapture.StartRecording();
                LogDebug("Recording PC audio...");
            }
            catch (Exception ex)
            {
                LogDebug($"Error starting PC audio recording: {ex.Message}");
            }
        }

        public void StopPCAudioRecording()
        {
            _loopbackCapture?.StopRecording();
            LogDebug("Stopped recording PC audio.");
        }

        public void StartWebcamAudioRecording(bool saveRecording = true)
        {
            try
            {
                _saveWebcamAudio = saveRecording;
                _tempWebcamFilePath = Path.Combine(_webcamAudioDirectory, $"WebcamAudio_{DateTime.Now:yyyyMMdd_HHmmss}.wav");

                _waveIn = new WaveInEvent();

                var deviceNumber = FindWebcamAudioDevice();
                if (deviceNumber == -1)
                {
                    LogDebug("Webcam audio device not found.");
                    return;
                }

                _waveIn.DeviceNumber = deviceNumber;
                _waveIn.WaveFormat = new WaveFormat(44100, 1);
                _writer = new WaveFileWriter(_tempWebcamFilePath, _waveIn.WaveFormat);

                _waveIn.DataAvailable += (s, a) =>
                {
                    _writer.Write(a.Buffer, 0, a.BytesRecorded);

                    // Calculate audio level
                    float level = CalculateLevel(a.Buffer, a.BytesRecorded);
                    WebcamLevelChanged?.Invoke(level);
                };

                _waveIn.RecordingStopped += (s, a) =>
                {
                    _writer?.Dispose();
                    _writer = null;
                    _waveIn.Dispose();

                    if (_saveWebcamAudio)
                    {
                        LogDebug($"Webcam audio recording saved to: {_tempWebcamFilePath}");
                        FileCaptured?.Invoke(_tempWebcamFilePath);
                        ExtractMFCCs(_tempWebcamFilePath);
                    }
                    else
                    {
                        // Delete the file if we're not supposed to save it
                        if (File.Exists(_tempWebcamFilePath))
                        {
                            try
                            {
                                File.Delete(_tempWebcamFilePath);
                                LogDebug("Webcam audio recording discarded");
                            }
                            catch (Exception ex)
                            {
                                LogDebug($"Error deleting temporary audio file: {ex.Message}");
                            }
                        }
                    }

                    // Reset audio level
                    WebcamLevelChanged?.Invoke(0);
                };

                _waveIn.StartRecording();
                LogDebug("Recording webcam audio...");
            }
            catch (Exception ex)
            {
                LogDebug($"Error starting webcam audio recording: {ex.Message}");
            }
        }

        public void StopWebcamAudioRecording()
        {
            _waveIn?.StopRecording();
            LogDebug("Stopped recording webcam audio.");
        }

        private int FindWebcamAudioDevice()
        {
            for (int i = 0; i < WaveIn.DeviceCount; i++)
            {
                var deviceInfo = WaveIn.GetCapabilities(i);
                if (deviceInfo.ProductName.Contains("Webcam"))
                {
                    return i;
                }
            }
            return -1;
        }

        private void ExtractMFCCs(string filePath)
        {
            try
            {
                using var waveFile = new WaveFileReader(filePath);
                var samples = new float[waveFile.SampleCount];
                int sampleIndex = 0;
                var buffer = new byte[waveFile.WaveFormat.SampleRate * waveFile.WaveFormat.BlockAlign];
                int samplesRead;
                while ((samplesRead = waveFile.Read(buffer, 0, buffer.Length)) > 0)
                {
                    for (int i = 0; i < samplesRead; i += waveFile.WaveFormat.BlockAlign)
                    {
                        samples[sampleIndex++] = BitConverter.ToSingle(buffer, i);
                    }
                }

                int sampleRate = waveFile.WaveFormat.SampleRate;
                int featureCount = 13;
                int frameSize = 512;
                int hopSize = 256;

                var mfccExtractor = new MfccExtractor(new MfccOptions
                {
                    SamplingRate = sampleRate,
                    FeatureCount = featureCount,
                    FrameSize = frameSize,
                    HopSize = hopSize
                });

                var mfccs = mfccExtractor.ComputeFrom(samples);

                string mfccFilePath = Path.Combine(Path.GetDirectoryName(filePath), Path.GetFileNameWithoutExtension(filePath) + "_MFCCs.csv");

                using (var writer = new StreamWriter(mfccFilePath))
                {
                    foreach (var vector in mfccs)
                    {
                        writer.WriteLine(string.Join(",", vector));
                    }
                }

                LogDebug($"MFCCs saved to: {mfccFilePath}");
            }
            catch (Exception ex)
            {
                LogDebug($"Error extracting MFCCs: {ex.Message}");
            }
        }

        // Calculate audio level from raw buffer data
        private float CalculateLevel(byte[] buffer, int bytesRecorded)
        {
            // Simple RMS calculation
            float sum = 0;
            int sampleCount = bytesRecorded / 2; // 16-bit samples

            for (int i = 0; i < bytesRecorded; i += 2)
            {
                short sample = (short)((buffer[i + 1] << 8) | buffer[i]);
                float normalized = sample / 32768f;
                sum += normalized * normalized;
            }

            float rms = (float)Math.Sqrt(sum / sampleCount);
            return Math.Min(1.0f, rms * 5.0f); // Scale up a bit for better visualization
        }

        private void LogDebug(string message)
        {
            DebugMessageLogged?.Invoke(message);
        }
    }
}
