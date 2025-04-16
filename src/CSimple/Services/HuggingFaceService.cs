using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Diagnostics;
using CSimple.Pages; // Add reference to access ModelType enum

namespace CSimple.Services
{
    public class HuggingFaceService
    {
        private readonly HttpClient _httpClient;
        private const string BaseUrl = "https://huggingface.co/api";

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

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var models = JsonSerializer.Deserialize<List<HuggingFaceModel>>(content,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

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
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var modelDetails = JsonSerializer.Deserialize<HuggingFaceModelDetails>(content,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                return modelDetails;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting model details: {ex.Message}");
                return null;
            }
        }

        public string GetModelDownloadUrl(string modelId, string filename)
        {
            return $"https://huggingface.co/{modelId}/resolve/main/{filename}";
        }

        public async Task<bool> DownloadModelFileAsync(string modelId, string filename, string destinationPath)
        {
            try
            {
                var url = GetModelDownloadUrl(modelId, filename);
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
        public new long? Downloads { get; set; } // Fixed: Added 'new' keyword to properly hide the base member
        public List<string> SiblingModels { get; set; } = new List<string>();
        public string License { get; set; }
    }
}
