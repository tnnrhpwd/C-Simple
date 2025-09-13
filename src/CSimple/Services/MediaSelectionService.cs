using Microsoft.Maui.Storage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CSimple.Models;

namespace CSimple.Services
{
    public interface IMediaSelectionService
    {
        Task<(string imagePath, string imageName)> SelectImageAsync();
        Task<(string audioPath, string audioName)> SelectAudioAsync();
        Task<(string filePath, string fileName, string fileType)> SelectFileAsync(bool supportsImages, bool supportsAudio, bool supportsMultimodal, IEnumerable<NeuralNetworkModel> activeModels);
        Task CheckModelCompatibilityForMediaAsync(string mediaType, string fileName, IEnumerable<NeuralNetworkModel> activeModels);

        // Events for UI interaction
        Func<string, string, string, Task> ShowAlert { get; set; }
        Action<string> UpdateStatus { get; set; }
    }

    public class MediaSelectionService : IMediaSelectionService
    {
        public Func<string, string, string, Task> ShowAlert { get; set; } = async (t, m, c) => { await Task.CompletedTask; };
        public Action<string> UpdateStatus { get; set; } = (s) => { };

        public async Task<(string imagePath, string imageName)> SelectImageAsync()
        {
            try
            {
                var result = await FilePicker.Default.PickAsync(new PickOptions
                {
                    PickerTitle = "Select an image",
                    FileTypes = FilePickerFileType.Images
                });

                if (result != null)
                {
                    return (result.FullPath, result.FileName);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error selecting image: {ex}");
                await ShowAlert?.Invoke("Error", "Failed to select image file. Please try again.", "OK");
            }

            return (null, null);
        }

        public async Task<(string audioPath, string audioName)> SelectAudioAsync()
        {
            try
            {
                var result = await FilePicker.Default.PickAsync(new PickOptions
                {
                    PickerTitle = "Select an audio file",
                    FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.iOS, new[] { "public.audio" } },
                        { DevicePlatform.Android, new[] { "audio/*" } },
                        { DevicePlatform.WinUI, new[] { ".mp3", ".wav", ".m4a", ".aac", ".ogg", ".flac", ".wma" } },
                        { DevicePlatform.Tizen, new[] { "audio/*" } },
                        { DevicePlatform.macOS, new[] { "mp3", "wav", "m4a", "aac", "ogg", "flac", "wma" } },
                    })
                });

                if (result != null)
                {
                    return (result.FullPath, result.FileName);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error selecting audio: {ex}");
                await ShowAlert?.Invoke("Error", "Failed to select audio file. Please try again.", "OK");
            }

            return (null, null);
        }

        public async Task<(string filePath, string fileName, string fileType)> SelectFileAsync(bool supportsImages, bool supportsAudio, bool supportsMultimodal, IEnumerable<NeuralNetworkModel> activeModels)
        {
            try
            {
                // If no models are active, show a message
                if (!supportsImages && !supportsAudio && !supportsMultimodal)
                {
                    await ShowAlert?.Invoke(
                        "No Compatible Models Active",
                        "Please activate an image, audio, or multimodal model before uploading files.\n\n" +
                        "You can activate models from the Model Management section above.",
                        "OK");
                    return (null, null, null);
                }

                // Build dynamic file types based on active models
                var fileTypeDict = new Dictionary<DevicePlatform, IEnumerable<string>>();
                var supportedFormats = new List<string>();
                var pickerTitle = "Select a file";

                if ((supportsImages || supportsMultimodal) && supportsAudio)
                {
                    // Both image/multimodal and audio models are active
                    pickerTitle = "Select an image or audio file";
                    supportedFormats.AddRange(new[] { "Images: JPG, PNG, GIF, BMP, WebP, TIFF", "Audio: MP3, WAV, M4A, AAC, OGG, FLAC, WMA" });
                    if (supportsMultimodal)
                        supportedFormats.Add("Multimodal models support both image and text inputs");

                    fileTypeDict = new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.iOS, new[] { "public.image", "public.audio" } },
                        { DevicePlatform.Android, new[] { "image/*", "audio/*" } },
                        { DevicePlatform.WinUI, new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".tiff", ".mp3", ".wav", ".m4a", ".aac", ".ogg", ".flac", ".wma" } },
                        { DevicePlatform.Tizen, new[] { "image/*", "audio/*" } },
                        { DevicePlatform.macOS, new[] { "jpg", "jpeg", "png", "gif", "bmp", "webp", "tiff", "mp3", "wav", "m4a", "aac", "ogg", "flac", "wma" } },
                    };
                }
                else if (supportsImages || supportsMultimodal)
                {
                    // Only image or multimodal models are active
                    pickerTitle = supportsMultimodal ? "Select an image file (for multimodal model)" : "Select an image file";
                    supportedFormats.Add("Images: JPG, PNG, GIF, BMP, WebP, TIFF");
                    if (supportsMultimodal)
                        supportedFormats.Add("Note: Multimodal models also accept text prompts alongside images");

                    fileTypeDict = new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.iOS, new[] { "public.image" } },
                        { DevicePlatform.Android, new[] { "image/*" } },
                        { DevicePlatform.WinUI, new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".tiff" } },
                        { DevicePlatform.Tizen, new[] { "image/*" } },
                        { DevicePlatform.macOS, new[] { "jpg", "jpeg", "png", "gif", "bmp", "webp", "tiff" } },
                    };
                }
                else if (supportsAudio)
                {
                    // Only audio models are active
                    pickerTitle = "Select an audio file";
                    supportedFormats.Add("Audio: MP3, WAV, M4A, AAC, OGG, FLAC, WMA");

                    fileTypeDict = new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.iOS, new[] { "public.audio" } },
                        { DevicePlatform.Android, new[] { "audio/*" } },
                        { DevicePlatform.WinUI, new[] { ".mp3", ".wav", ".m4a", ".aac", ".ogg", ".flac", ".wma" } },
                        { DevicePlatform.Tizen, new[] { "audio/*" } },
                        { DevicePlatform.macOS, new[] { "mp3", "wav", "m4a", "aac", "ogg", "flac", "wma" } },
                    };
                }

                // Show status message about what files can be selected
                var activeModelNames = new List<string>();
                if (supportsImages)
                {
                    var imageModels = activeModels.Where(m => m.InputType == ModelInputType.Image).Select(m => m.Name);
                    activeModelNames.AddRange(imageModels.Select(name => $"Image: {name}"));
                }
                if (supportsAudio)
                {
                    var audioModels = activeModels.Where(m => m.InputType == ModelInputType.Audio).Select(m => m.Name);
                    activeModelNames.AddRange(audioModels.Select(name => $"Audio: {name}"));
                }

                UpdateStatus?.Invoke($"📁 File picker ready. Supported formats: {string.Join(", ", supportedFormats)}. Active models: {string.Join(", ", activeModelNames)}");

                var result = await FilePicker.Default.PickAsync(new PickOptions
                {
                    PickerTitle = pickerTitle,
                    FileTypes = new FilePickerFileType(fileTypeDict)
                });

                if (result != null)
                {
                    // Determine file type based on extension
                    var extension = Path.GetExtension(result.FileName)?.ToLowerInvariant();
                    var isImage = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".tiff" }.Contains(extension);
                    var isAudio = new[] { ".mp3", ".wav", ".m4a", ".aac", ".ogg", ".flac", ".wma" }.Contains(extension);

                    string detectedFileType = null;

                    if (isImage && supportsImages)
                    {
                        detectedFileType = "image";
                        await CheckModelCompatibilityForMediaAsync("image", result.FileName, activeModels);
                    }
                    else if (isAudio && supportsAudio)
                    {
                        detectedFileType = "audio";
                        await CheckModelCompatibilityForMediaAsync("audio", result.FileName, activeModels);
                    }
                    else
                    {
                        // File type doesn't match active models
                        var expectedTypes = new List<string>();
                        if (supportsImages) expectedTypes.Add("image");
                        if (supportsAudio) expectedTypes.Add("audio");

                        UpdateStatus?.Invoke($"⚠ File '{result.FileName}' selected, but it doesn't match your active model types ({string.Join(" or ", expectedTypes)}).");

                        await ShowAlert?.Invoke(
                            "File Type Mismatch",
                            $"The file '{result.FileName}' doesn't match your currently active models.\n\n" +
                            $"Active models support: {string.Join(" and ", supportedFormats)}\n\n" +
                            $"Please select a compatible file type or activate additional models.",
                            "OK");
                        return (null, null, null);
                    }

                    return (result.FullPath, result.FileName, detectedFileType);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error selecting file: {ex}");
                await ShowAlert?.Invoke("Error", "Failed to select file. Please try again.", "OK");
            }

            return (null, null, null);
        }

        public async Task CheckModelCompatibilityForMediaAsync(string mediaType, string fileName, IEnumerable<NeuralNetworkModel> activeModels)
        {
            try
            {
                bool hasCompatibleModel = false;
                var activeCompatibleModels = new List<string>();

                switch (mediaType.ToLower())
                {
                    case "image":
                        hasCompatibleModel = activeModels.Any(m => m.InputType == ModelInputType.Image);
                        if (hasCompatibleModel)
                        {
                            activeCompatibleModels = activeModels
                                .Where(m => m.InputType == ModelInputType.Image)
                                .Select(m => m.Name)
                                .ToList();
                        }
                        break;
                    case "audio":
                        hasCompatibleModel = activeModels.Any(m => m.InputType == ModelInputType.Audio);
                        if (hasCompatibleModel)
                        {
                            activeCompatibleModels = activeModels
                                .Where(m => m.InputType == ModelInputType.Audio)
                                .Select(m => m.Name)
                                .ToList();
                        }
                        break;
                }

                if (hasCompatibleModel)
                {
                    // Positive feedback - models are ready
                    UpdateStatus?.Invoke($"✓ {fileName} selected. Ready to process with: {string.Join(", ", activeCompatibleModels)}");
                    System.Diagnostics.Debug.WriteLine($"Media compatibility check passed for {mediaType}: {fileName}");
                }
                else
                {
                    // Warning - no compatible models active
                    UpdateStatus?.Invoke($"⚠ {fileName} selected, but no active {mediaType} models found. Activate a {mediaType} model to process this file.");
                    System.Diagnostics.Debug.WriteLine($"Media compatibility warning for {mediaType}: {fileName} - no active {mediaType} models");

                    // Optional: Show a brief informational message
                    await ShowAlert?.Invoke(
                        "Model Needed",
                        $"You've selected an {mediaType} file ({fileName}), but no {mediaType} models are currently active.\n\n" +
                        $"To process this {mediaType}, please activate an {mediaType} model from your available models or search for one on HuggingFace.\n\n" +
                        $"You can still send the message, but the {mediaType} won't be processed.",
                        "OK");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error checking model compatibility for {mediaType}: {ex.Message}");
            }
        }
    }
}
