using Microsoft.Maui.Storage;
using System;
using System.IO;

namespace CSimple.Services
{
    public interface IAppPathService
    {
        string GetBasePath();
        string GetResourcesPath();
        string GetWebcamImagesPath();
        string GetPCAudioPath();
        string GetHFModelsPath();
        string GetPipelinesPath();
        string GetMemoryFilesPath();
        Task SetBasePath(string newBasePath);
        Task InitializeDirectoriesAsync();
    }

    public class AppPathService : IAppPathService
    {
        private const string BASE_PATH_KEY = "AppBasePath";
        private const string DEFAULT_BASE_FOLDER = "CSimple";

        private string _cachedBasePath;

        public AppPathService()
        {
            // Initialize the base path on startup
            _cachedBasePath = GetStoredBasePath() ?? GetDefaultBasePath();
        }

        /// <summary>
        /// Gets the base application directory path
        /// </summary>
        public string GetBasePath()
        {
            return _cachedBasePath;
        }

        /// <summary>
        /// Gets the Resources folder path
        /// </summary>
        public string GetResourcesPath()
        {
            return Path.Combine(GetBasePath(), "Resources");
        }

        /// <summary>
        /// Gets the WebcamImages folder path
        /// </summary>
        public string GetWebcamImagesPath()
        {
            return Path.Combine(GetResourcesPath(), "WebcamImages");
        }

        /// <summary>
        /// Gets the PCAudio folder path
        /// </summary>
        public string GetPCAudioPath()
        {
            return Path.Combine(GetResourcesPath(), "PCAudio");
        }

        /// <summary>
        /// Gets the HFModels folder path
        /// </summary>
        public string GetHFModelsPath()
        {
            return Path.Combine(GetResourcesPath(), "HFModels");
        }

        /// <summary>
        /// Gets the Pipelines folder path
        /// </summary>
        public string GetPipelinesPath()
        {
            return Path.Combine(GetResourcesPath(), "Pipelines");
        }

        /// <summary>
        /// Gets the MemoryFiles folder path
        /// </summary>
        public string GetMemoryFilesPath()
        {
            return Path.Combine(GetResourcesPath(), "MemoryFiles");
        }

        /// <summary>
        /// Sets a new base path and saves it to preferences
        /// </summary>
        public async Task SetBasePath(string newBasePath)
        {
            if (string.IsNullOrWhiteSpace(newBasePath))
            {
                throw new ArgumentException("Base path cannot be null or empty", nameof(newBasePath));
            }

            // Validate that the path is accessible
            try
            {
                var testPath = Path.Combine(newBasePath, DEFAULT_BASE_FOLDER);
                Directory.CreateDirectory(testPath);

                // If we get here, the path is valid and accessible
                _cachedBasePath = testPath;
                Preferences.Set(BASE_PATH_KEY, testPath);

                // Initialize all required directories with the new path
                await InitializeDirectoriesAsync();

                System.Diagnostics.Debug.WriteLine($"AppPathService: Base path updated to {testPath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AppPathService: Failed to set base path {newBasePath}: {ex.Message}");
                throw new InvalidOperationException($"Cannot access or create directory at {newBasePath}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Creates all required directories if they don't exist
        /// </summary>
        public async Task InitializeDirectoriesAsync()
        {
            try
            {
                var directories = new[]
                {
                    GetBasePath(),
                    GetResourcesPath(),
                    GetWebcamImagesPath(),
                    GetPCAudioPath(),
                    GetHFModelsPath(),
                    GetPipelinesPath(),
                    GetMemoryFilesPath()
                };

                await Task.Run(() =>
                {
                    foreach (var directory in directories)
                    {
                        if (!Directory.Exists(directory))
                        {
                            Directory.CreateDirectory(directory);
                            System.Diagnostics.Debug.WriteLine($"AppPathService: Created directory {directory}");
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AppPathService: Error initializing directories: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Gets the stored base path from preferences
        /// </summary>
        private string GetStoredBasePath()
        {
            return Preferences.Get(BASE_PATH_KEY, null);
        }

        /// <summary>
        /// Gets the default base path (Documents/CSimple)
        /// </summary>
        private string GetDefaultBasePath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                DEFAULT_BASE_FOLDER
            );
        }
    }
}
