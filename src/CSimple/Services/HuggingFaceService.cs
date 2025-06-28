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
        public async Task DownloadModelAsync(string modelId, string destinationPath, IProgress<(double progress, string status)> progressReporter = null)
        {
            try
            {
                Debug.WriteLine($"Starting download of model {modelId} to {destinationPath}...");

                // Check if destinationPath is a marker file or directory
                string modelDir;
                bool isMarkerFile = destinationPath.EndsWith(".download_marker");

                if (isMarkerFile)
                {
                    // Extract directory from marker file path
                    modelDir = Path.GetDirectoryName(destinationPath);
                    string modelName = Path.GetFileNameWithoutExtension(destinationPath);
                    modelDir = Path.Combine(modelDir, modelName);

                    // Delete existing marker file if it exists
                    if (File.Exists(destinationPath))
                    {
                        Debug.WriteLine($"Removing existing marker file: {destinationPath}");
                        File.Delete(destinationPath);
                    }
                }
                else
                {
                    modelDir = destinationPath;
                }

                // Create model directory
                if (!Directory.Exists(modelDir))
                {
                    Directory.CreateDirectory(modelDir);
                    Debug.WriteLine($"Created model directory: {modelDir}");
                }

                // Report initial progress
                progressReporter?.Report((0.0, "Fetching file list..."));

                // Retrieve files with size information
                var files = await GetModelFilesWithSizeAsync(modelId);
                if (files == null || files.Count == 0)
                {
                    throw new InvalidOperationException($"No files found for model {modelId}");
                }

                long totalSize = files.Sum(f => f.Size);
                long downloadedBytes = 0;
                int fileIndex = 0;

                Debug.WriteLine($"Found {files.Count} files to download, total size: {totalSize:N0} bytes");
                progressReporter?.Report((0.05, $"Starting download of {files.Count} files..."));

                foreach (var file in files)
                {
                    fileIndex++;

                    // Skip if file path is empty or invalid
                    if (string.IsNullOrWhiteSpace(file.Path))
                    {
                        Debug.WriteLine($"Skipping file {fileIndex} with empty path");
                        continue;
                    }

                    // Prepare local file path, preserving subdirectories
                    string relativePath = file.Path.Replace("/", Path.DirectorySeparatorChar.ToString());
                    string localFilePath = Path.Combine(modelDir, relativePath);
                    string localDir = Path.GetDirectoryName(localFilePath);

                    if (!Directory.Exists(localDir))
                        Directory.CreateDirectory(localDir);

                    // Skip if file already exists and has the correct size
                    if (File.Exists(localFilePath))
                    {
                        var existingSize = new FileInfo(localFilePath).Length;
                        if (existingSize == file.Size && file.Size > 0)
                        {
                            Debug.WriteLine($"File {file.Path} already exists with correct size, skipping");
                            downloadedBytes += file.Size;

                            // Report progress for skipped file
                            if (totalSize > 0)
                            {
                                double progress = Math.Min(0.95, 0.05 + (downloadedBytes * 0.9 / totalSize));
                                progressReporter?.Report((progress, $"Verified {file.Path}"));
                            }

                            continue;
                        }
                        else
                        {
                            Debug.WriteLine($"File {file.Path} exists but size mismatch (existing: {existingSize}, expected: {file.Size}), re-downloading");
                        }
                    }

                    Debug.WriteLine($"Downloading file {fileIndex}/{files.Count}: {file.Path} ({file.Size:N0} bytes)");

                    // Report progress for current file start
                    if (totalSize > 0)
                    {
                        double currentProgress = Math.Min(0.95, 0.05 + (downloadedBytes * 0.9 / totalSize));
                        progressReporter?.Report((currentProgress, $"Downloading {file.Path}..."));
                    }

                    try
                    {
                        var downloadUrl = GetModelDownloadUrl(modelId, file.Path);
                        Debug.WriteLine($"Download URL: {downloadUrl}");

                        using (var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
                        {
                            if (!response.IsSuccessStatusCode)
                            {
                                Debug.WriteLine($"Failed to download {file.Path}: {response.StatusCode} - {response.ReasonPhrase}");
                                continue; // Skip this file but continue with others
                            }

                            using (var remoteStream = await response.Content.ReadAsStreamAsync())
                            using (var localStream = File.Create(localFilePath))
                            {
                                var buffer = new byte[81920]; // 80KB buffer
                                int bytesRead;
                                long fileDownloadedBytes = 0;
                                long lastReportedBytes = 0;

                                while ((bytesRead = await remoteStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                                {
                                    await localStream.WriteAsync(buffer, 0, bytesRead);
                                    fileDownloadedBytes += bytesRead;
                                    downloadedBytes += bytesRead;

                                    // Report progress every 1MB to avoid too many updates
                                    if (downloadedBytes - lastReportedBytes >= 1048576 || fileDownloadedBytes == file.Size) // 1MB or file complete
                                    {
                                        if (totalSize > 0)
                                        {
                                            double progress = Math.Min(0.95, 0.05 + (downloadedBytes * 0.9 / totalSize));
                                            string status = $"Downloading {file.Path} ({downloadedBytes:N0}/{totalSize:N0} bytes)";
                                            progressReporter?.Report((progress, status));

                                            int percent = (int)(downloadedBytes * 100 / totalSize);
                                            Debug.WriteLine($"Overall progress: {percent}% ({downloadedBytes:N0}/{totalSize:N0} bytes)");
                                        }
                                        lastReportedBytes = downloadedBytes;
                                    }
                                }

                                Debug.WriteLine($"Successfully downloaded {file.Path} ({fileDownloadedBytes:N0} bytes)");
                            }
                        }
                    }
                    catch (Exception fileEx)
                    {
                        Debug.WriteLine($"Error downloading file {file.Path}: {fileEx.Message}");
                        // Continue with next file
                    }
                }

                // Report completion
                progressReporter?.Report((1.0, "Download completed!"));

                // Create completion marker if this was called with a marker file path
                if (isMarkerFile)
                {
                    var markerContent = JsonSerializer.Serialize(new
                    {
                        ModelId = modelId,
                        DownloadedAt = DateTime.UtcNow,
                        Status = "completed",
                        ModelDirectory = modelDir,
                        FilesDownloaded = files.Count,
                        TotalSize = totalSize
                    }, new JsonSerializerOptions { WriteIndented = true });

                    await File.WriteAllTextAsync(destinationPath, markerContent);
                    Debug.WriteLine($"Created completion marker: {destinationPath}");
                }

                Debug.WriteLine($"Model {modelId} download completed successfully to {modelDir}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error downloading model {modelId}: {ex.Message}");
                Debug.WriteLine(ex.ToString());
                throw; // Re-throw to let caller handle the error
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
                var downloadedModels = new List<string>();

                if (Directory.Exists(cacheDirectory))
                {
                    // Look for completed marker files (.download_marker)
                    var markerFiles = Directory.GetFiles(cacheDirectory, "*.download_marker");
                    foreach (var markerFile in markerFiles)
                    {
                        try
                        {
                            // Read marker file to check if download was completed
                            var markerContent = File.ReadAllText(markerFile);
                            if (markerContent.Contains("\"Status\": \"completed\""))
                            {
                                var modelId = Path.GetFileNameWithoutExtension(markerFile).Replace("_", "/");
                                downloadedModels.Add(modelId);
                                Debug.WriteLine($"Found completed download: {modelId}");
                            }
                            else
                            {
                                Debug.WriteLine($"Marker file {markerFile} indicates incomplete download");
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error reading marker file {markerFile}: {ex.Message}");
                        }
                    }

                    // Also look for model directories with actual files
                    var allDirs = Directory.GetDirectories(cacheDirectory);
                    foreach (var modelDir in allDirs)
                    {
                        var dirName = Path.GetFileName(modelDir);

                        // Skip if this is not a model directory
                        if (dirName.StartsWith(".") || dirName == "temp") continue;

                        // Check if directory contains model files
                        var modelFiles = Directory.GetFiles(modelDir, "*", SearchOption.AllDirectories);
                        if (modelFiles.Length > 0)
                        {
                            string modelId;

                            // Handle different directory naming conventions
                            if (dirName.StartsWith("models--"))
                            {
                                // HuggingFace cache format: models--org--model
                                modelId = dirName.Substring(8).Replace("--", "/");
                            }
                            else
                            {
                                // Our custom format: org_model -> org/model
                                modelId = dirName.Replace("_", "/");
                            }

                            if (!downloadedModels.Contains(modelId))
                            {
                                downloadedModels.Add(modelId);
                                Debug.WriteLine($"Found model directory: {modelId} ({modelFiles.Length} files)");
                            }
                        }
                    }

#if DEBUG
                    Debug.WriteLine($"Found {downloadedModels.Count} downloaded models: {string.Join(", ", downloadedModels)}");
#endif
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

        public async Task<List<HuggingFaceFileInfo>> GetModelFilesWithSizeAsync(string modelId)
        {
            // Try multiple API endpoints in order of preference
            var endpoints = new[]
            {
                $"https://huggingface.co/api/models/{modelId}/tree/main", // Direct model tree endpoint
                $"{BaseUrl}/models/{modelId}/tree/main",                   // Alternative base URL
                $"https://huggingface.co/{modelId}/tree/main"             // Web interface endpoint
            };

            foreach (var endpoint in endpoints)
            {
                try
                {
                    Debug.WriteLine($"Trying endpoint: {endpoint}");
                    var files = await TryGetFilesFromEndpoint(endpoint, modelId);
                    if (files != null && files.Count > 0)
                    {
                        Debug.WriteLine($"Successfully retrieved {files.Count} files from {endpoint}");
                        return files;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Endpoint {endpoint} failed: {ex.Message}");
                    continue; // Try next endpoint
                }
            }

            // If all API endpoints fail, try the GitHub-style API as last resort
            try
            {
                Debug.WriteLine("Trying GitHub-style API format...");
                var githubStyleFiles = await TryGitHubStyleApi(modelId);
                if (githubStyleFiles != null && githubStyleFiles.Count > 0)
                {
                    Debug.WriteLine($"Retrieved {githubStyleFiles.Count} files via GitHub-style API");
                    return githubStyleFiles;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GitHub-style API failed: {ex.Message}");
            }

            // If all methods fail, use estimated sizes as fallback
            Debug.WriteLine("All API methods failed, using estimated sizes");
            return GetEstimatedFileSizes(modelId);
        }

        private async Task<List<HuggingFaceFileInfo>> TryGetFilesFromEndpoint(string endpoint, string modelId)
        {
            var response = await _httpClient.GetAsync(endpoint);

            if (!response.IsSuccessStatusCode)
            {
                Debug.WriteLine($"Endpoint returned {response.StatusCode}: {response.ReasonPhrase}");
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            Debug.WriteLine($"Received {content.Length} bytes from {endpoint}");

            // Try multiple parsing strategies
            var files = TryParseFileResponse(content, modelId);
            return files;
        }

        private List<HuggingFaceFileInfo> TryParseFileResponse(string content, string modelId)
        {
            // Validate content first
            if (!IsValidJsonResponse(content))
            {
                Debug.WriteLine("Invalid JSON response, skipping parsing");
                return null;
            }

            // Strategy 1: Try direct array parsing using JsonDocument
            try
            {
                using var doc = JsonDocument.Parse(content);
                var files = new List<HuggingFaceFileInfo>();

                // Check if root is an array
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in doc.RootElement.EnumerateArray())
                    {
                        if (item.TryGetProperty("type", out var typeElement) &&
                            typeElement.GetString()?.Equals("blob", StringComparison.OrdinalIgnoreCase) == true)
                        {
                            var file = new HuggingFaceFileInfo
                            {
                                Type = "blob",
                                Path = item.TryGetProperty("path", out var pathElement) ? pathElement.GetString() : "",
                                Oid = item.TryGetProperty("oid", out var oidElement) ? oidElement.GetString() : "",
                                Size = GetSizeFromJsonElement(item)
                            };
                            files.Add(file);
                        }
                    }

                    if (files.Count > 0)
                    {
                        Debug.WriteLine($"Strategy 1 success: Found {files.Count} files via direct array parsing");
                        return files;
                    }
                }
            }
            catch (JsonException ex)
            {
                Debug.WriteLine($"Strategy 1 failed: {ex.Message}");
            }

            // Strategy 2: Try object with tree property
            try
            {
                using var doc = JsonDocument.Parse(content);
                var files = new List<HuggingFaceFileInfo>();

                // Only try to access properties if root is an object
                if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                    doc.RootElement.TryGetProperty("tree", out var treeElement))
                {
                    foreach (var item in treeElement.EnumerateArray())
                    {
                        if (item.TryGetProperty("type", out var typeElement) &&
                            typeElement.GetString()?.Equals("blob", StringComparison.OrdinalIgnoreCase) == true)
                        {
                            var file = new HuggingFaceFileInfo
                            {
                                Type = "blob",
                                Path = item.TryGetProperty("path", out var pathElement) ? pathElement.GetString() : "",
                                Oid = item.TryGetProperty("oid", out var oidElement) ? oidElement.GetString() : "",
                                Size = GetSizeFromJsonElement(item)
                            };
                            files.Add(file);
                        }
                    }

                    if (files.Count > 0)
                    {
                        Debug.WriteLine($"Strategy 2 success: Found {files.Count} files via tree property parsing");
                        return files;
                    }
                }
                else
                {
                    Debug.WriteLine("Strategy 2 skipped: Root element is not an object or has no 'tree' property");
                }
            }
            catch (JsonException ex)
            {
                Debug.WriteLine($"Strategy 2 failed: {ex.Message}");
            }

            // Strategy 3: Try siblings array (HuggingFace model details format)
            try
            {
                using var doc = JsonDocument.Parse(content);
                var files = new List<HuggingFaceFileInfo>();

                // Only try to access properties if root is an object
                if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                    doc.RootElement.TryGetProperty("siblings", out var siblingsElement))
                {
                    foreach (var item in siblingsElement.EnumerateArray())
                    {
                        var file = new HuggingFaceFileInfo
                        {
                            Type = "blob",
                            Path = item.TryGetProperty("rfilename", out var filenameElement) ? filenameElement.GetString() : "",
                            Size = GetSizeFromJsonElement(item)
                        };

                        if (!string.IsNullOrEmpty(file.Path))
                        {
                            files.Add(file);
                        }
                    }

                    if (files.Count > 0)
                    {
                        Debug.WriteLine($"Strategy 3 success: Found {files.Count} files via siblings array parsing");
                        return files;
                    }
                }
                else
                {
                    Debug.WriteLine("Strategy 3 skipped: Root element is not an object or has no 'siblings' property");
                }
            }
            catch (JsonException ex)
            {
                Debug.WriteLine($"Strategy 3 failed: {ex.Message}");
            }

            // Strategy 4: Try to handle files without explicit "type" property
            try
            {
                using var doc = JsonDocument.Parse(content);
                var files = new List<HuggingFaceFileInfo>();

                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in doc.RootElement.EnumerateArray())
                    {
                        // Some APIs might not have explicit "type" but have file paths
                        if (item.TryGetProperty("path", out var pathElement) ||
                            item.TryGetProperty("filename", out pathElement) ||
                            item.TryGetProperty("name", out pathElement))
                        {
                            var path = pathElement.GetString();
                            if (!string.IsNullOrEmpty(path) && !path.EndsWith("/")) // Not a directory
                            {
                                var file = new HuggingFaceFileInfo
                                {
                                    Type = "blob",
                                    Path = path,
                                    Oid = item.TryGetProperty("oid", out var oidElement) ? oidElement.GetString() : "",
                                    Size = GetSizeFromJsonElement(item)
                                };
                                files.Add(file);
                            }
                        }
                    }

                    if (files.Count > 0)
                    {
                        Debug.WriteLine($"Strategy 4 success: Found {files.Count} files without type requirement");
                        return files;
                    }
                }
            }
            catch (JsonException ex)
            {
                Debug.WriteLine($"Strategy 4 failed: {ex.Message}");
            }

            Debug.WriteLine("All parsing strategies failed");
            return null;
        }

        private long GetSizeFromJsonElement(JsonElement item)
        {
            // Try different size property names
            string[] sizeProperties = { "size", "filesize", "file_size", "length", "lfs" };

            foreach (var prop in sizeProperties)
            {
                if (item.TryGetProperty(prop, out var sizeElement))
                {
                    if (sizeElement.ValueKind == JsonValueKind.Number)
                    {
                        return sizeElement.GetInt64();
                    }
                    else if (sizeElement.ValueKind == JsonValueKind.String &&
                             long.TryParse(sizeElement.GetString(), out var parsedSize))
                    {
                        return parsedSize;
                    }
                    else if (sizeElement.ValueKind == JsonValueKind.Object)
                    {
                        // Sometimes LFS info is in an object like {"size": 123}
                        if (sizeElement.TryGetProperty("size", out var nestedSizeElement))
                        {
                            if (nestedSizeElement.ValueKind == JsonValueKind.Number)
                            {
                                return nestedSizeElement.GetInt64();
                            }
                        }
                    }
                }
            }

            return 0; // Default if no size found
        }

        private async Task<List<HuggingFaceFileInfo>> TryGitHubStyleApi(string modelId)
        {
            // Some models might be accessible via a GitHub-style API
            var gitUrl = $"https://huggingface.co/{modelId}/raw/main";

            try
            {
                // Try to get a directory listing or manifest
                var manifestUrls = new[]
                {
                    $"{gitUrl}/.gitattributes",
                    $"{gitUrl}/README.md",
                    $"https://huggingface.co/{modelId}/resolve/main/config.json"
                };

                foreach (var url in manifestUrls)
                {
                    try
                    {
                        var response = await _httpClient.GetAsync(url);
                        if (response.IsSuccessStatusCode)
                        {
                            Debug.WriteLine($"Found accessible file at {url}, model likely exists");
                            // If we can access any file, assume the model exists and return estimated sizes
                            return GetEstimatedFileSizes(modelId);
                        }
                    }
                    catch
                    {
                        continue;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GitHub-style API check failed: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Get estimated file sizes for common models when API doesn't provide them
        /// </summary>
        private List<HuggingFaceFileInfo> GetEstimatedFileSizes(string modelId)
        {
            var files = new List<HuggingFaceFileInfo>();
            var modelName = modelId.ToLowerInvariant();

            Debug.WriteLine($"Using estimated file sizes for model: {modelId}");

            // Add common files with estimated sizes based on model type
            if (modelName.Contains("gpt2"))
            {
                // GPT-2 standard model sizes
                if (modelName.Contains("xl") || modelName.Contains("1558m"))
                {
                    files.Add(new HuggingFaceFileInfo { Path = "pytorch_model.bin", Type = "blob", Size = 6200000000 }); // ~6.2GB
                    files.Add(new HuggingFaceFileInfo { Path = "config.json", Type = "blob", Size = 1024 });
                    files.Add(new HuggingFaceFileInfo { Path = "tokenizer.json", Type = "blob", Size = 2000000 }); // ~2MB
                }
                else if (modelName.Contains("large") || modelName.Contains("774m"))
                {
                    files.Add(new HuggingFaceFileInfo { Path = "pytorch_model.bin", Type = "blob", Size = 3200000000 }); // ~3.2GB
                    files.Add(new HuggingFaceFileInfo { Path = "config.json", Type = "blob", Size = 1024 });
                    files.Add(new HuggingFaceFileInfo { Path = "tokenizer.json", Type = "blob", Size = 2000000 }); // ~2MB
                }
                else if (modelName.Contains("medium") || modelName.Contains("355m"))
                {
                    files.Add(new HuggingFaceFileInfo { Path = "pytorch_model.bin", Type = "blob", Size = 1400000000 }); // ~1.4GB
                    files.Add(new HuggingFaceFileInfo { Path = "config.json", Type = "blob", Size = 1024 });
                    files.Add(new HuggingFaceFileInfo { Path = "tokenizer.json", Type = "blob", Size = 2000000 }); // ~2MB
                }
                else
                {
                    // GPT-2 base model (124M parameters)
                    files.Add(new HuggingFaceFileInfo { Path = "pytorch_model.bin", Type = "blob", Size = 500000000 }); // ~500MB
                    files.Add(new HuggingFaceFileInfo { Path = "config.json", Type = "blob", Size = 1024 });
                    files.Add(new HuggingFaceFileInfo { Path = "tokenizer.json", Type = "blob", Size = 2000000 }); // ~2MB
                }
            }
            else if (modelName.Contains("deepseek"))
            {
                // DeepSeek models are typically very large (600GB+)
                files.Add(new HuggingFaceFileInfo { Path = "model.safetensors", Type = "blob", Size = 640000000000 }); // ~640GB estimated
                files.Add(new HuggingFaceFileInfo { Path = "config.json", Type = "blob", Size = 1024 });
                files.Add(new HuggingFaceFileInfo { Path = "tokenizer.json", Type = "blob", Size = 8000000 }); // ~8MB
            }
            else if (modelName.Contains("bert"))
            {
                if (modelName.Contains("large"))
                {
                    files.Add(new HuggingFaceFileInfo { Path = "pytorch_model.bin", Type = "blob", Size = 1340000000 }); // ~1.34GB
                }
                else
                {
                    // BERT base model sizes
                    files.Add(new HuggingFaceFileInfo { Path = "pytorch_model.bin", Type = "blob", Size = 440000000 }); // ~440MB
                }
                files.Add(new HuggingFaceFileInfo { Path = "config.json", Type = "blob", Size = 1024 });
                files.Add(new HuggingFaceFileInfo { Path = "vocab.txt", Type = "blob", Size = 230000 }); // ~230KB
            }
            else if (modelName.Contains("distil"))
            {
                // DistilBERT and similar models (smaller than BERT)
                files.Add(new HuggingFaceFileInfo { Path = "pytorch_model.bin", Type = "blob", Size = 270000000 }); // ~270MB
                files.Add(new HuggingFaceFileInfo { Path = "config.json", Type = "blob", Size = 1024 });
                files.Add(new HuggingFaceFileInfo { Path = "tokenizer.json", Type = "blob", Size = 1300000 }); // ~1.3MB
            }
            else if (modelName.Contains("roberta"))
            {
                if (modelName.Contains("large"))
                {
                    files.Add(new HuggingFaceFileInfo { Path = "pytorch_model.bin", Type = "blob", Size = 1400000000 }); // ~1.4GB
                }
                else
                {
                    files.Add(new HuggingFaceFileInfo { Path = "pytorch_model.bin", Type = "blob", Size = 500000000 }); // ~500MB
                }
                files.Add(new HuggingFaceFileInfo { Path = "config.json", Type = "blob", Size = 1024 });
                files.Add(new HuggingFaceFileInfo { Path = "tokenizer.json", Type = "blob", Size = 1300000 }); // ~1.3MB
            }
            else if (modelName.Contains("t5"))
            {
                if (modelName.Contains("11b") || modelName.Contains("large"))
                {
                    files.Add(new HuggingFaceFileInfo { Path = "pytorch_model.bin", Type = "blob", Size = 45000000000 }); // ~45GB
                }
                else if (modelName.Contains("3b") || modelName.Contains("base"))
                {
                    files.Add(new HuggingFaceFileInfo { Path = "pytorch_model.bin", Type = "blob", Size = 11000000000 }); // ~11GB
                }
                else
                {
                    files.Add(new HuggingFaceFileInfo { Path = "pytorch_model.bin", Type = "blob", Size = 3000000000 }); // ~3GB
                }
                files.Add(new HuggingFaceFileInfo { Path = "config.json", Type = "blob", Size = 1024 });
                files.Add(new HuggingFaceFileInfo { Path = "tokenizer.json", Type = "blob", Size = 2500000 }); // ~2.5MB
            }
            else if (modelName.Contains("whisper"))
            {
                if (modelName.Contains("large"))
                {
                    files.Add(new HuggingFaceFileInfo { Path = "pytorch_model.bin", Type = "blob", Size = 3100000000 }); // ~3.1GB
                }
                else if (modelName.Contains("medium"))
                {
                    files.Add(new HuggingFaceFileInfo { Path = "pytorch_model.bin", Type = "blob", Size = 1500000000 }); // ~1.5GB
                }
                else if (modelName.Contains("small"))
                {
                    files.Add(new HuggingFaceFileInfo { Path = "pytorch_model.bin", Type = "blob", Size = 970000000 }); // ~970MB
                }
                else
                {
                    files.Add(new HuggingFaceFileInfo { Path = "pytorch_model.bin", Type = "blob", Size = 240000000 }); // ~240MB (base/tiny)
                }
                files.Add(new HuggingFaceFileInfo { Path = "config.json", Type = "blob", Size = 1024 });
                files.Add(new HuggingFaceFileInfo { Path = "tokenizer.json", Type = "blob", Size = 800000 }); // ~800KB
            }
            else
            {
                // Generic model estimation - conservative 500MB default
                files.Add(new HuggingFaceFileInfo { Path = "pytorch_model.bin", Type = "blob", Size = 500000000 }); // ~500MB default
                files.Add(new HuggingFaceFileInfo { Path = "config.json", Type = "blob", Size = 1024 });
                files.Add(new HuggingFaceFileInfo { Path = "tokenizer.json", Type = "blob", Size = 1000000 }); // ~1MB
            }

            // Add common additional files that most models have
            files.Add(new HuggingFaceFileInfo { Path = "tokenizer_config.json", Type = "blob", Size = 512 });
            files.Add(new HuggingFaceFileInfo { Path = "special_tokens_map.json", Type = "blob", Size = 512 });
            files.Add(new HuggingFaceFileInfo { Path = "vocab.json", Type = "blob", Size = 1000000 }); // ~1MB
            files.Add(new HuggingFaceFileInfo { Path = "merges.txt", Type = "blob", Size = 500000 }); // ~500KB

            return files;
        }

        /// <summary>
        /// Validates and preprocesses API response content
        /// </summary>
        private bool IsValidJsonResponse(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                Debug.WriteLine("Response content is null or empty");
                return false;
            }

            if (content.Length < 2)
            {
                Debug.WriteLine("Response content too short to be valid JSON");
                return false;
            }

            // Check if it starts with valid JSON characters
            var trimmed = content.Trim();
            if (!trimmed.StartsWith("{") && !trimmed.StartsWith("["))
            {
                Debug.WriteLine("Response doesn't start with valid JSON");
                return false;
            }

            // Check for common error responses
            if (trimmed.Contains("\"error\"") && trimmed.Contains("Sorry, we can't find"))
            {
                Debug.WriteLine("Response contains 404 error message");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Test method to debug the actual API response structure
        /// </summary>
        public async Task<string> TestApiResponse(string modelId)
        {
            try
            {
                var endpoint = $"https://huggingface.co/api/models/{modelId}/tree/main";
                Debug.WriteLine($"Testing API endpoint: {endpoint}");

                var response = await _httpClient.GetAsync(endpoint);
                var content = await response.Content.ReadAsStringAsync();

                Debug.WriteLine($"API Response Status: {response.StatusCode}");
                Debug.WriteLine($"API Response Length: {content.Length} bytes");
                Debug.WriteLine($"API Response Content: {content}");

                return content;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Test API call failed: {ex.Message}");
                return $"Error: {ex.Message}";
            }
        }
    }

    public class HuggingFaceFileInfo
    {
        public string Oid { get; set; }
        public string Path { get; set; }
        public string Type { get; set; } // "blob" for files, "tree" for directories
        public long Size { get; set; } // Changed to long to handle larger files
    }
}
