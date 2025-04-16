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

        private async Task GetModelFiles(HuggingFaceModelDetails modelDetails)
        {
            if (modelDetails == null) return;

            try
            {
                // Use a separate API call to get model files through the repository tree endpoint
                var url = $"{BaseUrl}/repos/{modelDetails.ModelId ?? modelDetails.Id}/tree/main";
                Debug.WriteLine($"Getting model files from URL: {url}");

                var response = await _httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"Received files response: {content.Substring(0, Math.Min(content.Length, 500))}...");

                    try
                    {
                        var fileTree = JsonSerializer.Deserialize<List<HuggingFaceFileInfo>>(content,
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                        modelDetails.Files = fileTree?
                            .Where(f => f.Type == "blob")
                            .Select(f => f.Path)
                            .ToList() ?? new List<string>();

                        Debug.WriteLine($"Found {modelDetails.Files.Count} files for model");
                    }
                    catch (JsonException jsonEx)
                    {
                        Debug.WriteLine($"Error parsing file tree JSON: {jsonEx.Message}");
                        // Fallback to common model files
                        modelDetails.Files = GetDefaultModelFiles(modelDetails.ModelId ?? modelDetails.Id);
                    }
                }
                else
                {
                    Debug.WriteLine($"Failed to get file tree: {response.StatusCode}");
                    // Fallback to common model files
                    modelDetails.Files = GetDefaultModelFiles(modelDetails.ModelId ?? modelDetails.Id);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting model files: {ex.Message}");
                // Fallback to common model files
                modelDetails.Files = GetDefaultModelFiles(modelDetails.ModelId ?? modelDetails.Id);
            }
        }

        private List<string> GetDefaultModelFiles(string modelId)
        {
            Debug.WriteLine("Using fallback default model files");

            // Provide common default files based on model naming patterns
            var files = new List<string>();

            if (modelId.Contains("bert") || modelId.Contains("gpt") ||
                modelId.Contains("llama") || modelId.Contains("t5"))
            {
                files.Add("pytorch_model.bin");
                files.Add("config.json");
                files.Add("tokenizer.json");
                files.Add("vocab.json");
            }
            else if (modelId.Contains("whisper") || modelId.Contains("wav2vec"))
            {
                files.Add("pytorch_model.bin");
                files.Add("model.safetensors");
                files.Add("config.json");
            }
            else
            {
                // Generic fallbacks
                files.Add("pytorch_model.bin");
                files.Add("model.safetensors");
                files.Add("model.onnx");
                files.Add("model.bin");
                files.Add("config.json");
            }

            return files;
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
