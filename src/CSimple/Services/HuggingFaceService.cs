using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Diagnostics;
using CSimple.Pages; // For ModelType enum
using CSimple.Models; // Add this using directive
using System.IO;
using System.Linq;
using static System.Net.Mime.MediaTypeNames;
using Microsoft.Maui.Storage;

namespace CSimple.Services
{
    public class HuggingFaceService
    {
        private readonly HttpClient _httpClient;
        private const string BaseUrl = "https://huggingface.co/api";
        private const string RepoUrl = "https://huggingface.co";

        public HuggingFaceService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public async Task<List<HuggingFaceModel>> SearchModelsAsync(string query, string category = null, int limit = 10)
        {
            try
            {
                var url = $"{BaseUrl}/models?search={Uri.EscapeDataString(query)}&limit={limit}";

                // Add category filter if provided
                if (!string.IsNullOrEmpty(category))
                {
                    url += $"&filter={Uri.EscapeDataString(category)}";
                }

                Debug.WriteLine($"Searching models with URL: {url}");
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"Received search response: {content.Substring(0, Math.Min(content.Length, 500))}...");

                var models = JsonSerializer.Deserialize<List<HuggingFaceModel>>(content,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                Debug.WriteLine($"Parsed {models?.Count ?? 0} models from response");

                return models ?? new List<HuggingFaceModel>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error searching HuggingFace models: {ex.Message}");
                return new List<HuggingFaceModel>();
            }
        }

        public async Task<HuggingFaceModelDetails> GetModelDetailsAsync(string modelId)
        {
            try
            {
                var url = $"{BaseUrl}/models/{modelId}";
                Debug.WriteLine($"Getting model details from URL: {url}");

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"Received details response: {content.Substring(0, Math.Min(content.Length, 500))}...");

                var modelDetails = JsonSerializer.Deserialize<HuggingFaceModelDetails>(content,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                // The API doesn't provide a direct file list, so we need to get files separately
                await GetModelFiles(modelDetails);

                return modelDetails;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting model details: {ex.Message}");
                Debug.WriteLine(ex.ToString());
                return null;
            }
        }

        public async Task<List<string>> GetModelFilesAsync(string modelId)
        {
            try
            {
                // Try to get files using the repository tree endpoint
                var url = $"{BaseUrl}/repos/{modelId}/tree/main";
                Debug.WriteLine($"Getting model files from URL: {url}");

                var response = await _httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"Received files response: {content.Length} bytes");

                    try
                    {
                        var fileTree = JsonSerializer.Deserialize<List<HuggingFaceFileInfo>>(content,
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                        var files = fileTree?
                            .Where(f => f.Type == "blob")
                            .Select(f => f.Path)
                            .ToList() ?? new List<string>();

                        if (files.Count > 0)
                        {
                            Debug.WriteLine($"Found {files.Count} files via repo API");
                            return files;
                        }
                    }
                    catch (JsonException jsonEx)
                    {
                        Debug.WriteLine($"Error parsing file tree JSON: {jsonEx.Message}");
                    }
                }

                // If we couldn't get files via the API, try a different approach
                // For modern transformer models, suggest common configuration and weight files
                var commonFiles = GetCommonModelFiles(modelId);
                Debug.WriteLine($"Using {commonFiles.Count} common model files as fallback");
                return commonFiles;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting model files: {ex}");
                return GetCommonModelFiles(modelId);
            }
        }

        private List<string> GetCommonModelFiles(string modelId)
        {
            // Detect model type from ID to suggest appropriate files
            bool isLlama = modelId.Contains("llama", StringComparison.OrdinalIgnoreCase);
            bool isWhisper = modelId.Contains("whisper", StringComparison.OrdinalIgnoreCase);
            bool isBert = modelId.Contains("bert", StringComparison.OrdinalIgnoreCase);
            bool isGpt = modelId.Contains("gpt", StringComparison.OrdinalIgnoreCase) ||
                         modelId.Contains("llm", StringComparison.OrdinalIgnoreCase);
            bool isSafetensors = modelId.Contains("stable", StringComparison.OrdinalIgnoreCase) ||
                                 modelId.Contains("diffusion", StringComparison.OrdinalIgnoreCase);

            var files = new List<string>();

            // Config files that almost all models have
            files.Add("config.json");

            // Model weight files
            if (isLlama || isGpt)
            {
                files.Add("pytorch_model.bin");
                files.Add("model.safetensors");
                files.Add("tokenizer.model");
                files.Add("tokenizer.json");
                files.Add("model.gguf"); // GGUF format for llama.cpp
            }
            else if (isWhisper)
            {
                files.Add("pytorch_model.bin");
                files.Add("model.bin");
                files.Add("encoder.onnx");
                files.Add("decoder.onnx");
            }
            else if (isBert)
            {
                files.Add("pytorch_model.bin");
                files.Add("tf_model.h5");
                files.Add("vocab.txt");
            }
            else if (isSafetensors)
            {
                files.Add("model.safetensors");
                files.Add("v1-inference.ckpt");
            }
            else
            {
                // Generic options
                files.Add("pytorch_model.bin");
                files.Add("model.safetensors");
                files.Add("model.bin");
                files.Add("model.onnx");
                files.Add("weights.bin");
            }

            // Other common files
            files.Add("tokenizer_config.json");
            files.Add("special_tokens_map.json");
            files.Add("vocab.json");
            files.Add("merges.txt");

            return files;
        }

        private async Task GetModelFiles(HuggingFaceModelDetails modelDetails)
        {
            if (modelDetails == null) return;

            try
            {
                // Get files through the improved method
                var files = await GetModelFilesAsync(modelDetails.ModelId ?? modelDetails.Id);

                if (files != null && files.Count > 0)
                {
                    modelDetails.Siblings = files.Select(fileName => new Sibling { Rfilename = fileName }).ToList();
                    Debug.WriteLine($"Found {modelDetails.Siblings.Count} files for model {modelDetails.ModelId ?? modelDetails.Id}");
                }
                else
                {
                    // Empty files list as a fallback
                    modelDetails.Siblings = new List<Sibling>();
                    Debug.WriteLine("No files found for the model");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error retrieving model files: {ex}");
                modelDetails.Siblings = new List<Sibling>();
            }
        }

        public string GetModelDownloadUrl(string modelId, string filename)
        {
            return $"{RepoUrl}/{modelId}/resolve/main/{filename}";
        }

        public async Task<bool> DownloadModelFileAsync(string modelId, string filename, string destinationPath)
        {
            try
            {
                var url = GetModelDownloadUrl(modelId, filename);
                Debug.WriteLine($"Downloading file from URL: {url}");

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                using (var stream = await response.Content.ReadAsStreamAsync())
                using (var fileStream = System.IO.File.Create(destinationPath))
                {
                    await stream.CopyToAsync(fileStream);
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error downloading model file: {ex.Message}");
                return false;
            }
        }

        public Dictionary<string, string> GetModelCategoryFilters()
        {
            return new Dictionary<string, string>
            {
                { "Audio-to-Text", "automatic-speech-recognition" },
                { "Text-to-Text", "text-generation,text2text-generation" },
                { "Image Understanding", "image-classification,object-detection" },
                { "Text-to-Speech", "text-to-speech,text-to-audio" }
            };
        }

        private async Task ProcessModelDetails(CSimple.Models.HuggingFaceModelDetails details)
        {
            // Assuming you have a list of filenames you want to associate with the details object
            List<string> fileNamesFromSomewhere = await GetFileNamesForModelAsync(details.ModelId); // Example method

            if (fileNamesFromSomewhere != null)
            {
                // Instead of: details.Files = fileNamesFromSomewhere; (INVALID)

                // Do this: Create Sibling objects and assign to the Siblings list
                details.Siblings = fileNamesFromSomewhere.Select(fileName => new Sibling { Rfilename = fileName }).ToList();
                Debug.WriteLine($"Assigned {details.Siblings.Count} siblings based on filenames.");
            }
            else
            {
                // Handle case where filenames couldn't be retrieved, maybe initialize Siblings to empty list
                details.Siblings = new List<Sibling>();
                Debug.WriteLine("No filenames retrieved, initialized Siblings to empty list.");
            }

            // If you were trying to assign an empty list:
            // Instead of: details.Files = new List<string>(); (INVALID)
            // Do this:
            // details.Siblings = new List<Sibling>();

            // If you were trying to add a single file:
            // Instead of: details.Files.Add("somefile.bin"); (INVALID, Files is read-only)
            // Do this:
            // if (details.Siblings == null) details.Siblings = new List<Sibling>();
            // details.Siblings.Add(new Sibling { Rfilename = "somefile.bin" });
        }

        // Dummy method for example purposes
        private async Task<List<string>> GetFileNamesForModelAsync(string modelId)
        {
            await Task.Delay(10); // Simulate async work
            // Replace with actual logic to get filenames if needed
            return new List<string> { "config.json", "pytorch_model.bin", "tokenizer.json" };
        }

        public async Task DownloadModelAsync(string modelId, string destinationPath)
        {
            try
            {
                // Simulate downloading the model
                Debug.WriteLine($"Downloading model {modelId} to {destinationPath}...");
                await Task.Delay(2000); // Simulate download delay

                // Create a dummy file to represent the downloaded model
                File.WriteAllText(destinationPath, $"Model {modelId} downloaded successfully.");
                Debug.WriteLine($"Model {modelId} downloaded successfully.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error downloading model {modelId}: {ex.Message}");
                throw;
            }
        }
        public async Task DeleteModelAsync(string modelId, string modelPath)
        {
            try
            {
                // Simulate deleting the model
                Debug.WriteLine($"Deleting model {modelId} at {modelPath}...");
                if (File.Exists(modelPath))
                {
                    // Use async File operations to ensure this truly is async
                    await Task.Run(() => File.Delete(modelPath));
                    Debug.WriteLine($"Model {modelId} deleted successfully.");
                }
                else
                {
                    Debug.WriteLine($"Model {modelId} not found at {modelPath}.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error deleting model {modelId}: {ex.Message}");
                throw;
            }
        }
        public void EnsureHFModelCacheDirectoryExists()
        {
            try
            {
                string cacheDirectory = @"C:\Users\tanne\Documents\CSimple\Resources\HFModels";
                if (!Directory.Exists(cacheDirectory))
                {
                    Directory.CreateDirectory(cacheDirectory);
                    Debug.WriteLine($"Created HuggingFace model cache directory at {cacheDirectory}.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error ensuring HuggingFace model cache directory exists: {ex.Message}");
                throw;
            }
        }
        public List<string> RefreshDownloadedModelsList()
        {
            try
            {
                string cacheDirectory = @"C:\Users\tanne\Documents\CSimple\Resources\HFModels";
                if (Directory.Exists(cacheDirectory))
                {
                    var downloadedModels = Directory.GetFiles(cacheDirectory).Select(Path.GetFileName).ToList();
                    Debug.WriteLine($"Found {downloadedModels.Count} downloaded models.");
                    return downloadedModels;
                }
                else
                {
                    Debug.WriteLine("HuggingFace model cache directory does not exist.");
                    return new List<string>();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error refreshing downloaded models list: {ex.Message}");
                throw;
            }
        }
    }

    public class HuggingFaceFileInfo
    {
        public string Oid { get; set; }
        public string Path { get; set; }
        public string Type { get; set; } // "blob" for files, "tree" for directories
        public int Size { get; set; }
    }
}
