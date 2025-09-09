using System;
using System.IO;
using System.Threading.Tasks;
using NAudio.Wave;
using NWaves.FeatureExtractors;
using NWaves.FeatureExtractors.Options;
using NAudio.Wave.SampleProviders;
using System.Collections.Generic;
using System.Diagnostics;

namespace CSimple.Services
{
    public class AudioCaptureService
    {
        #region Events

#pragma warning disable CS0067 // Event is never used
        public event Action<string> DebugMessageLogged;
#pragma warning restore CS0067

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
            _pcAudioDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CSimple", "Resources", "PCAudio");
            _webcamAudioDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CSimple", "Resources", "WebcamAudio");

            Directory.CreateDirectory(_pcAudioDirectory);
            Directory.CreateDirectory(_webcamAudioDirectory);

            Debug.Print($"Audio audio directories initialized: {_pcAudioDirectory}, {_webcamAudioDirectory}");
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
                        Debug.Print($"PC audio recording saved to: {_tempPcFilePath}");
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
                                Debug.Print("PC audio recording discarded");
                            }
                            catch (Exception ex)
                            {
                                Debug.Print($"Error deleting temporary audio file: {ex.Message}");
                            }
                        }
                    }

                    // Reset audio level
                    PCLevelChanged?.Invoke(0);
                };

                _loopbackCapture.StartRecording();
                Debug.Print("Recording PC audio...");
            }
            catch (Exception ex)
            {
                Debug.Print($"Error starting PC audio recording: {ex.Message}");
            }
        }

        public void StopPCAudioRecording()
        {
            _loopbackCapture?.StopRecording();
            Debug.Print("Stopped recording PC audio.");
        }

        public void StartWebcamAudioRecording(bool saveRecording = true)
        {
            try
            {
                _saveWebcamAudio = saveRecording;
                _tempWebcamFilePath = Path.Combine(_webcamAudioDirectory, $"WebcamAudio_{DateTime.Now:yyyyMMdd_HHmmss}.wav");

                Debug.Print($"Starting webcam audio recording to: {_tempWebcamFilePath}");
                Debug.Print($"Save recording: {saveRecording}");
                Debug.Print($"Webcam audio directory: {_webcamAudioDirectory}");

                // Ensure directory exists
                if (!Directory.Exists(_webcamAudioDirectory))
                {
                    Directory.CreateDirectory(_webcamAudioDirectory);
                    Debug.Print($"Created webcam audio directory: {_webcamAudioDirectory}");
                }

                _waveIn = new WaveInEvent();

                var deviceNumber = FindWebcamAudioDevice();
                if (deviceNumber == -1)
                {
                    Debug.Print("ERROR: Webcam audio device not found. Cannot start recording.");
                    return;
                }

                Debug.Print($"Using audio device {deviceNumber} for webcam audio recording");

                _waveIn.DeviceNumber = deviceNumber;
                _waveIn.WaveFormat = new WaveFormat(44100, 1);

                try
                {
                    _writer = new WaveFileWriter(_tempWebcamFilePath, _waveIn.WaveFormat);
                    Debug.Print($"Created WaveFileWriter successfully for {_tempWebcamFilePath}");
                }
                catch (Exception writerEx)
                {
                    Debug.Print($"ERROR: Failed to create WaveFileWriter: {writerEx.Message}");
                    _waveIn.Dispose();
                    return;
                }

                _waveIn.DataAvailable += (s, a) =>
                {
                    try
                    {
                        _writer.Write(a.Buffer, 0, a.BytesRecorded);

                        // Calculate audio level
                        float level = CalculateLevel(a.Buffer, a.BytesRecorded);
                        WebcamLevelChanged?.Invoke(level);
                    }
                    catch (Exception dataEx)
                    {
                        Debug.Print($"ERROR in DataAvailable handler: {dataEx.Message}");
                    }
                };

                _waveIn.RecordingStopped += (s, a) =>
                {
                    try
                    {
                        Debug.Print("Webcam audio recording stopped");

                        _writer?.Dispose();
                        _writer = null;
                        _waveIn?.Dispose();

                        if (_saveWebcamAudio && File.Exists(_tempWebcamFilePath))
                        {
                            var fileInfo = new FileInfo(_tempWebcamFilePath);
                            Debug.Print($"Webcam audio recording saved to: {_tempWebcamFilePath} (Size: {fileInfo.Length} bytes)");
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
                                    Debug.Print("Webcam audio recording discarded");
                                }
                                catch (Exception ex)
                                {
                                    Debug.Print($"Error deleting temporary audio file: {ex.Message}");
                                }
                            }
                        }

                        // Reset audio level
                        WebcamLevelChanged?.Invoke(0);
                    }
                    catch (Exception stoppedEx)
                    {
                        Debug.Print($"ERROR in RecordingStopped handler: {stoppedEx.Message}");
                    }
                };

                try
                {
                    _waveIn.StartRecording();
                    Debug.Print("Webcam audio recording started successfully");
                }
                catch (Exception startEx)
                {
                    Debug.Print($"ERROR: Failed to start recording: {startEx.Message}");
                    _writer?.Dispose();
                    _waveIn?.Dispose();
                    throw;
                }
            }
            catch (Exception ex)
            {
                Debug.Print($"ERROR starting webcam audio recording: {ex.Message}");
                Debug.Print($"Stack trace: {ex.StackTrace}");
            }
        }

        public void StopWebcamAudioRecording()
        {
            _waveIn?.StopRecording();
            Debug.Print("Stopped recording webcam audio.");
        }

        private int FindWebcamAudioDevice()
        {
            Debug.Print($"Searching for webcam audio device among {WaveIn.DeviceCount} available devices:");

            // List all available devices for debugging
            for (int i = 0; i < WaveIn.DeviceCount; i++)
            {
                var deviceInfo = WaveIn.GetCapabilities(i);
                Debug.Print($"Device {i}: {deviceInfo.ProductName} (Channels: {deviceInfo.Channels})");
            }

            // First, try to find devices with common webcam-related names
            string[] webcamKeywords = { "webcam", "camera", "usb", "microphone", "mic" };

            for (int i = 0; i < WaveIn.DeviceCount; i++)
            {
                var deviceInfo = WaveIn.GetCapabilities(i);
                string productName = deviceInfo.ProductName.ToLower();

                foreach (string keyword in webcamKeywords)
                {
                    if (productName.Contains(keyword))
                    {
                        Debug.Print($"Found potential webcam audio device: {deviceInfo.ProductName} (Device {i})");
                        return i;
                    }
                }
            }

            // If no webcam-specific device found, try to use the default microphone device (usually device 0)
            if (WaveIn.DeviceCount > 0)
            {
                var defaultDevice = WaveIn.GetCapabilities(0);
                Debug.Print($"No webcam-specific device found, using default microphone: {defaultDevice.ProductName} (Device 0)");
                return 0;
            }

            Debug.Print("No audio input devices found.");
            return -1;
        }

        /// <summary>
        /// Diagnostic method to list all available audio input devices
        /// </summary>
        public void ListAvailableAudioDevices()
        {
            Debug.Print("=== Available Audio Input Devices ===");
            Debug.Print($"Total devices found: {WaveIn.DeviceCount}");

            for (int i = 0; i < WaveIn.DeviceCount; i++)
            {
                try
                {
                    var deviceInfo = WaveIn.GetCapabilities(i);
                    Debug.Print($"Device {i}:");
                    Debug.Print($"  Name: {deviceInfo.ProductName}");
                    Debug.Print($"  Channels: {deviceInfo.Channels}");
                    // Note: ManufacturerId, ProductId, and DriverVersion may not be available in all NAudio versions
                    Debug.Print("---");
                }
                catch (Exception ex)
                {
                    Debug.Print($"Device {i}: Error getting device info - {ex.Message}");
                }
            }
            Debug.Print("=== End of Audio Device List ===");
        }

        /// <summary>
        /// Test method to verify webcam audio device functionality
        /// </summary>
        public bool TestWebcamAudioDevice()
        {
            try
            {
                Debug.Print("Testing webcam audio device...");

                var deviceNumber = FindWebcamAudioDevice();
                if (deviceNumber == -1)
                {
                    Debug.Print("TEST FAILED: No webcam audio device found");
                    return false;
                }

                using var testWaveIn = new WaveInEvent();
                testWaveIn.DeviceNumber = deviceNumber;
                testWaveIn.WaveFormat = new WaveFormat(44100, 1);

                bool dataReceived = false;
                testWaveIn.DataAvailable += (s, a) =>
                {
                    if (a.BytesRecorded > 0)
                    {
                        dataReceived = true;
                        Debug.Print($"TEST: Received {a.BytesRecorded} bytes of audio data");
                    }
                };

                testWaveIn.StartRecording();
                System.Threading.Thread.Sleep(1000); // Record for 1 second
                testWaveIn.StopRecording();

                if (dataReceived)
                {
                    Debug.Print("TEST PASSED: Webcam audio device is functional");
                    return true;
                }
                else
                {
                    Debug.Print("TEST FAILED: No audio data received from webcam device");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.Print($"TEST FAILED: Error testing webcam audio device - {ex.Message}");
                return false;
            }
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

                Debug.Print($"MFCCs saved to: {mfccFilePath}");
            }
            catch (Exception ex)
            {
                Debug.Print($"Error extracting MFCCs: {ex.Message}");
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
    }
}
