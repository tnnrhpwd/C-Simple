using CSimple.Models;
using System.Threading.Tasks;

namespace CSimple.Services
{
    public static class ModelDownloadServiceHelper
    {
        public static async Task DownloadModelAsync(
            IModelDownloadService modelDownloadService,
            NeuralNetworkModel model,
            Func<string, Task<HuggingFaceModelDetails>> getModelDetailsAsync,
            Func<NeuralNetworkModel, Task<(string formattedSize, long totalBytes)>> getModelDownloadSizeAsync,
            Func<string, string, string, string, Task<bool>> showConfirmation,
            Action<string> updateStatus,
            Action<bool> updateLoadingState,
            Action updateButtonText,
            Action notifyStatusChanged)
        {
            await modelDownloadService.DownloadModelAsync(
                model,
                getModelDetailsAsync,
                getModelDownloadSizeAsync,
                showConfirmation,
                updateStatus,
                updateLoadingState,
                updateButtonText,
                notifyStatusChanged
            );
        }

        public static async Task DeleteModelAsync(
            IModelDownloadService modelDownloadService,
            NeuralNetworkModel model,
            Action<string> updateStatus,
            Action<bool> updateLoadingState,
            Action refreshDownloadedModelsList,
            Action notifyStatusChanged)
        {
            await modelDownloadService.DeleteModelAsync(
                model,
                updateStatus,
                updateLoadingState,
                refreshDownloadedModelsList,
                notifyStatusChanged
            );
        }
    }
}
