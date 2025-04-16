using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Diagnostics;
using CSimple.Pages; // For ModelType enum

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
                    modelDetails.Files = files;
                    Debug.WriteLine($"Found {files.Count} files for model {modelDetails.ModelId ?? modelDetails.Id}");
                }
                else
                {
                    // Empty files list as a fallback
                    modelDetails.Files = new List<string>();
                    Debug.WriteLine("No files found for the model");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error retrieving model files: {ex}");
                modelDetails.Files = new List<string>();
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
    }

    public class HuggingFaceFileInfo
    {
        public string Oid { get; set; }
        public string Path { get; set; }
        public string Type { get; set; } // "blob" for files, "tree" for directories
        public int Size { get; set; }
    }

    public class HuggingFaceModel
    {
        public string Id { get; set; }
        public string ModelId { get; set; }
        public string Author { get; set; }
        public List<string> Tags { get; set; } = new List<string>();
        public string Pipeline_tag { get; set; }
        public string Description { get; set; }
        public string LastModified { get; set; }
        public int Downloads { get; set; }
        public ModelType RecommendedModelType => DetermineModelType();

        private ModelType DetermineModelType()
        {
            if (string.IsNullOrEmpty(Pipeline_tag))
                return ModelType.General;

            return Pipeline_tag.ToLower() switch
            {
                var tag when tag.Contains("speech") || tag.Contains("audio") => ModelType.InputSpecific,
                var tag when tag.Contains("text-generation") || tag.Contains("text2text") => ModelType.General,
                var tag when tag.Contains("image") || tag.Contains("vision") => ModelType.InputSpecific,
                _ => ModelType.General
            };
        }

        public override string ToString() => $"{ModelId ?? Id}";
    }

    public class HuggingFaceModelDetails : HuggingFaceModel
    {
        public List<string> Files { get; set; } = new List<string>();
        public string CardData { get; set; }
        public new long? Downloads { get; set; }
        public List<string> SiblingModels { get; set; } = new List<string>();
        public string License { get; set; }
    }
}
