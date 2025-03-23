using System;
using System.IO;
using System.Threading.Tasks;
using NAudio.Wave;
using NWaves.FeatureExtractors;
using NWaves.FeatureExtractors.Options;

namespace CSimple.Services
{
    public class AudioCaptureService
    {
        #region Events
        public event Action<string> DebugMessageLogged;
        public event Action<string> FileCaptured;
        #endregion

        #region Properties
        private WaveInEvent _waveIn;
        private WaveFileWriter _writer;
        private WasapiLoopbackCapture _loopbackCapture;
        private WaveFileWriter _loopbackWriter;
        private string _pcAudioDirectory;
        private string _webcamAudioDirectory;
        #endregion

        public AudioCaptureService()
        {
            InitDirectories();
        }

        private void InitDirectories()
        {
            string baseDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CSimple", "Resources");
            _pcAudioDirectory = Path.Combine(baseDirectory, "PCAudio");
            _webcamAudioDirectory = Path.Combine(baseDirectory, "WebcamAudio");

            Directory.CreateDirectory(_pcAudioDirectory);
            Directory.CreateDirectory(_webcamAudioDirectory);
        }

        public void StartPCAudioRecording()
        {
            try
            {
                string filePath = Path.Combine(_pcAudioDirectory, $"PCAudio_{DateTime.Now:yyyyMMdd_HHmmss}.wav");

                _loopbackCapture = new WasapiLoopbackCapture();
                _loopbackWriter = new WaveFileWriter(filePath, _loopbackCapture.WaveFormat);

                _loopbackCapture.DataAvailable += (s, a) =>
                {
                    _loopbackWriter.Write(a.Buffer, 0, a.BytesRecorded);
                };

                _loopbackCapture.RecordingStopped += (s, a) =>
                {
                    _loopbackWriter?.Dispose();
                    _loopbackWriter = null;
                    _loopbackCapture.Dispose();
                    LogDebug($"PC audio recording saved to: {filePath}");
                    FileCaptured?.Invoke(filePath);
                    ExtractMFCCs(filePath);
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

        public void StartWebcamAudioRecording()
        {
            try
            {
                string filePath = Path.Combine(_webcamAudioDirectory, $"WebcamAudio_{DateTime.Now:yyyyMMdd_HHmmss}.wav");

                _waveIn = new WaveInEvent();

                var deviceNumber = FindWebcamAudioDevice();
                if (deviceNumber == -1)
                {
                    LogDebug("Webcam audio device not found.");
                    return;
                }

                _waveIn.DeviceNumber = deviceNumber;
                _waveIn.WaveFormat = new WaveFormat(44100, 1);
                _writer = new WaveFileWriter(filePath, _waveIn.WaveFormat);

                _waveIn.DataAvailable += (s, a) =>
                {
                    _writer.Write(a.Buffer, 0, a.BytesRecorded);
                };

                _waveIn.RecordingStopped += (s, a) =>
                {
                    _writer?.Dispose();
                    _writer = null;
                    _waveIn.Dispose();
                    LogDebug($"Webcam audio recording saved to: {filePath}");
                    FileCaptured?.Invoke(filePath);
                    ExtractMFCCs(filePath);
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

        private void LogDebug(string message)
        {
            DebugMessageLogged?.Invoke(message);
        }
    }
}
