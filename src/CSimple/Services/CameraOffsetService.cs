using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace CSimple.Services
{
    public interface ICameraOffsetService
    {
        float CameraOffsetX { get; set; }
        float CameraOffsetY { get; set; }

        Task SaveCameraOffsetAsync(string pipelineName);
        Task LoadCameraOffsetAsync(string pipelineName);
        void UpdateCameraOffset(float x, float y);
    }

    public class CameraOffsetService : ICameraOffsetService
    {
        private float _cameraOffsetX = 0f;
        private float _cameraOffsetY = 0f;

        public float CameraOffsetX
        {
            get => _cameraOffsetX;
            set => _cameraOffsetX = value;
        }

        public float CameraOffsetY
        {
            get => _cameraOffsetY;
            set => _cameraOffsetY = value;
        }

        public async Task SaveCameraOffsetAsync(string pipelineName)
        {
            try
            {
                // Save camera offset to the same directory as pipelines (MyDocuments instead of ApplicationData)
                string pipelineDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CSimple", "Resources", "Pipelines");
                if (!Directory.Exists(pipelineDir))
                {
                    Directory.CreateDirectory(pipelineDir);
                }

                string offsetFile = Path.Combine(pipelineDir, $"{pipelineName}_cameraOffset.json");
                var offsetData = new { X = CameraOffsetX, Y = CameraOffsetY };
                string json = JsonSerializer.Serialize(offsetData);

                await File.WriteAllTextAsync(offsetFile, json);
                Debug.WriteLine($"💾 [SaveCameraOffsetAsync] Saved camera offset: ({CameraOffsetX}, {CameraOffsetY}) for pipeline: {pipelineName} to {offsetFile}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"⚠️ [SaveCameraOffsetAsync] Error saving camera offset: {ex.Message}");
            }
        }

        public async Task LoadCameraOffsetAsync(string pipelineName)
        {
            try
            {
                string pipelineDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CSimple", "Resources", "Pipelines");
                string offsetFile = Path.Combine(pipelineDir, $"{pipelineName}_cameraOffset.json");

                if (File.Exists(offsetFile))
                {
                    string json = await File.ReadAllTextAsync(offsetFile);
                    var offsetData = JsonSerializer.Deserialize<dynamic>(json);

                    if (offsetData != null)
                    {
                        var element = (JsonElement)offsetData;
                        if (element.TryGetProperty("X", out var xElement) && element.TryGetProperty("Y", out var yElement))
                        {
                            CameraOffsetX = xElement.GetSingle();
                            CameraOffsetY = yElement.GetSingle();
                            Debug.WriteLine($"📖 [LoadCameraOffsetAsync] Loaded camera offset: ({CameraOffsetX}, {CameraOffsetY}) for pipeline: {pipelineName}");
                        }
                    }
                }
                else
                {
                    // Set default values if no saved offset exists
                    CameraOffsetX = 0f;
                    CameraOffsetY = 0f;
                    Debug.WriteLine($"📂 [LoadCameraOffsetAsync] No saved camera offset found for pipeline: {pipelineName}, using defaults");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"⚠️ [LoadCameraOffsetAsync] Error loading camera offset: {ex.Message}");
                // Set default values on error
                CameraOffsetX = 0f;
                CameraOffsetY = 0f;
            }
        }

        public void UpdateCameraOffset(float x, float y)
        {
            CameraOffsetX = x;
            CameraOffsetY = y;
        }
    }
}
