using CSimple.Models;
using CSimple.Services.AppModeService;
using Microsoft.Maui.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CSimple.Services
{
    public interface IModelDownloadService
    {
        Task<bool> DownloadModelAsync(
            NeuralNetworkModel model,
            Func<string, Task<HuggingFaceModelDetails>> getModelDetails,
            Func<NeuralNetworkModel, Task<(string formattedSize, long totalBytes)>> getModelDownloadSize,
            Func<string, string, string, string, Task<bool>> showConfirmation,
            Action<string> updateCurrentStatus,
            Action<bool> setIsLoading,
            Action updateAllModelsDownloadButtonText,
            Action notifyDownloadStatusChanged);

        Task<bool> DeleteModelAsync(
            NeuralNetworkModel model,
            Action<string> updateCurrentStatus,
            Action<bool> setIsLoading,
            Action refreshDownloadedModelsList,
            Action notifyDownloadStatusChanged);

        void StopModelDownload(NeuralNetworkModel model);
    }

    public class ModelDownloadService : IModelDownloadService
    {
        private readonly HuggingFaceService _huggingFaceService;
        private readonly ITrayService _trayService;
        private readonly Dictionary<string, CancellationTokenSource> _downloadCancellationTokens = new();
        private readonly object _downloadCancellationLock = new object();

        public ModelDownloadService(HuggingFaceService huggingFaceService, ITrayService trayService)
        {
            _huggingFaceService = huggingFaceService;
            _trayService = trayService;
        }

        public async Task<bool> DownloadModelAsync(
            NeuralNetworkModel model,
            Func<string, Task<HuggingFaceModelDetails>> getModelDetails,
            Func<NeuralNetworkModel, Task<(string formattedSize, long totalBytes)>> getModelDownloadSize,
            Func<string, string, string, string, Task<bool>> showConfirmation,
            Action<string> updateCurrentStatus,
            Action<bool> setIsLoading,
            Action updateAllModelsDownloadButtonText,
            Action notifyDownloadStatusChanged)
        {
            var modelId = model.HuggingFaceModelId ?? model.Id;
            CancellationTokenSource cancellationTokenSource = null;

            try
            {
                // Show confirmation dialog before starting download
                updateCurrentStatus("Fetching model information...");
                setIsLoading(true);

                // Get model details and file size information
                var modelDetails = await getModelDetails(modelId);
                var (formattedSize, totalBytes) = await getModelDownloadSize(model);

                // Prepare confirmation message
                string modelInfo = $"Model: {model.Name ?? modelId}";
                if (!string.IsNullOrEmpty(model.Description))
                {
                    modelInfo += $"\nDescription: {model.Description}";
                }
                if (modelDetails != null)
                {
                    if (!string.IsNullOrEmpty(modelDetails.Author))
                    {
                        modelInfo += $"\nAuthor: {modelDetails.Author}";
                    }
                    if (!string.IsNullOrEmpty(modelDetails.Pipeline_tag))
                    {
                        modelInfo += $"\nType: {modelDetails.Pipeline_tag}";
                    }
                    if (modelDetails.Downloads > 0)
                    {
                        modelInfo += $"\nDownloads: {modelDetails.Downloads:N0}";
                    }
                }

                modelInfo += $"\nTotal Download Size: {formattedSize}";

                if (totalBytes > 1024 * 1024 * 1024) // > 1GB
                {
                    modelInfo += "\n\n⚠️ This is a large model that may take significant time to download.";
                }

                // Show confirmation dialog
                bool downloadConfirmed = await showConfirmation(
                    "Confirm Model Download",
                    $"{modelInfo}\n\nAre you sure you want to download this model?",
                    "Download",
                    "Cancel"
                );

                if (!downloadConfirmed)
                {
                    updateCurrentStatus("Download canceled by user");
                    setIsLoading(false);
                    return false;
                }

                // Create cancellation token for this download
                cancellationTokenSource = new CancellationTokenSource();
                lock (_downloadCancellationLock)
                {
                    _downloadCancellationTokens[modelId] = cancellationTokenSource;
                }

                // Proceed with download
                model.IsDownloading = true;
                model.DownloadProgress = 0.0;
                model.DownloadStatus = "Initializing download...";

                updateCurrentStatus($"Downloading {model.Name}...");

                // Update button text to show "Stop Download"
                updateAllModelsDownloadButtonText();

                // Show initial tray notification
                _trayService?.ShowProgress($"Downloading {model.Name}", "Preparing download...", 0.0);

                // Create a marker file for the download
                var markerPath = Path.Combine(@"C:\Users\tanne\Documents\CSimple\Resources\HFModels",
                    modelId.Replace("/", "_") + ".download_marker");

                // Ensure the directory exists
                Directory.CreateDirectory(Path.GetDirectoryName(markerPath));

                // Create progress reporter to connect HuggingFaceService progress to UI
                var progress = new Progress<(double progress, string status)>(report =>
                {
                    // Update model progress on UI thread
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        model.DownloadProgress = report.progress;
                        model.DownloadStatus = report.status;
                        updateCurrentStatus($"Downloading {model.Name}: {report.progress:P0} - {report.status}");

                        // Update tray progress
                        _trayService?.UpdateProgress(report.progress, report.status);
                    });
                });

                // Call the service to download with progress reporting and cancellation support
                await _huggingFaceService.DownloadModelAsync(modelId, markerPath, progress, cancellationTokenSource.Token);

                // Mark as downloaded (only if not cancelled)
                if (!cancellationTokenSource.Token.IsCancellationRequested)
                {
                    model.IsDownloaded = true;
                    model.DownloadProgress = 1.0;
                    model.DownloadStatus = "Download complete";

                    updateCurrentStatus($"Model {model.Name} ready for use");

                    // Show completion notification
                    _trayService?.ShowCompletionNotification("Download Complete", $"{model.Name} is ready to use");
                }

                return true;
            }
            catch (OperationCanceledException)
            {
                // Download was cancelled
                model.DownloadStatus = "Download cancelled";
                updateCurrentStatus($"Download of {model.Name} was cancelled");
                Debug.WriteLine($"Download cancelled for model: {modelId}");
                return false;
            }
            catch (Exception ex)
            {
                model.DownloadStatus = $"Download failed: {ex.Message}";
                updateCurrentStatus($"Failed to download {model.Name}: {ex.Message}");
                Debug.WriteLine($"Error downloading model: {ex.Message}");

                _trayService?.ShowCompletionNotification("Download Failed", $"Failed to download {model.Name}");
                return false;
            }
            finally
            {
                // Clean up
                model.IsDownloading = false;
                setIsLoading(false);

                // Remove cancellation token
                lock (_downloadCancellationLock)
                {
                    _downloadCancellationTokens.Remove(modelId);
                }
                cancellationTokenSource?.Dispose();

                // Hide tray progress
                _trayService?.HideProgress();

                // Trigger UI update for button text
                notifyDownloadStatusChanged();
            }
        }

        public Task<bool> DeleteModelAsync(
            NeuralNetworkModel model,
            Action<string> updateCurrentStatus,
            Action<bool> setIsLoading,
            Action refreshDownloadedModelsList,
            Action notifyDownloadStatusChanged)
        {
            try
            {
                setIsLoading(true);
                updateCurrentStatus($"Removing {model.Name}...");

                var modelId = model.HuggingFaceModelId ?? model.Id;

                // Remove marker file
                var markerPath = Path.Combine(@"C:\Users\tanne\Documents\CSimple\Resources\HFModels",
                    modelId.Replace("/", "_") + ".download_marker");

                if (File.Exists(markerPath))
                {
                    File.Delete(markerPath);
                }

                // Remove model directories (try both naming conventions)
                var cacheDir = @"C:\Users\tanne\Documents\CSimple\Resources\HFModels";

                var possibleDirNames = new[]
                {
                    modelId.Replace("/", "_"),           // org/model -> org_model
                    $"models--{modelId.Replace("/", "--")}"  // org/model -> models--org--model
                };

                foreach (var dirName in possibleDirNames)
                {
                    var modelCacheDir = Path.Combine(cacheDir, dirName);
                    if (Directory.Exists(modelCacheDir))
                    {
                        Directory.Delete(modelCacheDir, true);
                        Debug.WriteLine($"Deleted model directory: {modelCacheDir}");
                    }
                }

                // Refresh downloaded models list from disk to sync with actual state
                refreshDownloadedModelsList();

                updateCurrentStatus($"Successfully removed {model.Name} from device");

                // Trigger UI update for button text
                notifyDownloadStatusChanged();

                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                updateCurrentStatus($"Failed to remove {model.Name}: {ex.Message}");
                Debug.WriteLine($"Error deleting model: {ex.Message}");
                return Task.FromResult(false);
            }
            finally
            {
                setIsLoading(false);
            }
        }

        public void StopModelDownload(NeuralNetworkModel model)
        {
            var modelId = model.HuggingFaceModelId ?? model.Id;

            lock (_downloadCancellationLock)
            {
                if (_downloadCancellationTokens.TryGetValue(modelId, out var tokenSource))
                {
                    Debug.WriteLine($"Stopping download for model: {modelId}");
                    tokenSource.Cancel();
                    tokenSource.Dispose();
                    _downloadCancellationTokens.Remove(modelId);
                }
            }
        }
    }
}
