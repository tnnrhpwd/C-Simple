using CSimple.Models;
using Microsoft.Maui.Controls;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace CSimple.Services
{
    public interface IChatManagementService
    {
        Task CommunicateWithModelAsync(
            string message,
            NeuralNetworkModel activeModel,
            ObservableCollection<ChatMessage> chatMessages,
            bool hasSelectedImage,
            bool hasSelectedAudio,
            string selectedImageName,
            string selectedImagePath,
            string selectedAudioName,
            string selectedAudioPath,
            Func<string, string> getLocalModelPath,
            Func<string, string, string, Task> showAlert,
            Action clearMedia
        );

        Task<bool> ValidateMediaUploadAsync(
            bool hasSelectedImage,
            bool hasSelectedAudio,
            bool supportsImageInput,
            bool supportsAudioInput,
            Func<string, string, string, Task> showAlert
        );

        Task SuggestModelsForMediaTypeAsync(
            bool hasSelectedImage,
            bool hasSelectedAudio,
            bool supportsImageInput,
            bool supportsAudioInput,
            Func<string, string, string, Task> showAlert
        );
    }

    public class ChatManagementService : IChatManagementService
    {
        private readonly ModelExecutionService _modelExecutionService;

        public ChatManagementService(ModelExecutionService modelExecutionService)
        {
            _modelExecutionService = modelExecutionService;
        }

        public async Task CommunicateWithModelAsync(
            string message,
            NeuralNetworkModel activeModel,
            ObservableCollection<ChatMessage> chatMessages,
            bool hasSelectedImage,
            bool hasSelectedAudio,
            string selectedImageName,
            string selectedImagePath,
            string selectedAudioName,
            string selectedAudioPath,
            Func<string, string> getLocalModelPath,
            Func<string, string, string, Task> showAlert,
            Action clearMedia)
        {
            // Handle media-only messages (when message is empty but media is selected)
            if (string.IsNullOrWhiteSpace(message) && (hasSelectedImage || hasSelectedAudio))
            {
                await ProcessMediaOnlyMessageAsync(
                    activeModel,
                    chatMessages,
                    hasSelectedImage,
                    hasSelectedAudio,
                    selectedImageName,
                    selectedImagePath,
                    selectedAudioName,
                    selectedAudioPath,
                    getLocalModelPath,
                    showAlert,
                    clearMedia);
                return;
            }

            // Regular text message processing would be handled by ModelCommunicationService
            // This method focuses on the media-specific chat functionality
        }

        private async Task ProcessMediaOnlyMessageAsync(
            NeuralNetworkModel activeModel,
            ObservableCollection<ChatMessage> chatMessages,
            bool hasSelectedImage,
            bool hasSelectedAudio,
            string selectedImageName,
            string selectedImagePath,
            string selectedAudioName,
            string selectedAudioPath,
            Func<string, string> getLocalModelPath,
            Func<string, string, string, Task> showAlert,
            Action clearMedia)
        {
            // Create a descriptive message for media-only processing
            string mediaDescription = "";
            string mediaPath = "";

            if (hasSelectedImage)
            {
                mediaDescription = $"[Processing image: {selectedImageName}]";
                mediaPath = selectedImagePath;
                Debug.WriteLine($"Media-only message: Processing image {selectedImagePath}");
            }
            else if (hasSelectedAudio)
            {
                mediaDescription = $"[Processing audio: {selectedAudioName}]";
                mediaPath = selectedAudioPath;
                Debug.WriteLine($"Media-only message: Processing audio {selectedAudioPath}");
            }

            // Add user message to chat to show what's being processed
            var userMessage = new ChatMessage(mediaDescription, isFromUser: true, includeInHistory: true);
            chatMessages.Add(userMessage);
            Debug.WriteLine($"Added media-only user message to chat. ChatMessages count: {chatMessages.Count}");

            // Add processing message
            var processingMessage = new ChatMessage("Processing your media file...", isFromUser: false, modelName: activeModel?.Name ?? "System", includeInHistory: false)
            {
                IsProcessing = true,
                LLMSource = "local"
            };
            chatMessages.Add(processingMessage);

            try
            {
                string responseText = "";

                if (activeModel != null && !string.IsNullOrEmpty(activeModel.HuggingFaceModelId))
                {
                    // Try to process the media file through the model
                    Debug.WriteLine($"Attempting to process media with model: {activeModel.HuggingFaceModelId}");

                    // For audio files, create a prompt that includes the file path
                    string mediaPrompt = "";
                    if (hasSelectedAudio)
                    {
                        mediaPrompt = $"Please process this audio file: {mediaPath}";
                    }
                    else if (hasSelectedImage)
                    {
                        mediaPrompt = $"Please analyze this image file: {mediaPath}";
                    }

                    // Try to execute the model with the media prompt
                    try
                    {
                        // Get the local model path to force local-only execution
                        string localModelPath = getLocalModelPath(activeModel.HuggingFaceModelId);

                        var modelResponse = await _modelExecutionService.ExecuteHuggingFaceModelAsyncEnhanced(
                            activeModel.HuggingFaceModelId,
                            mediaPrompt,
                            activeModel,
                            "python", // TODO: Make configurable
                            @"c:\Users\tanne\Documents\Github\C-Simple\scripts\run_hf_model.py", // TODO: Make configurable
                            localModelPath);

                        if (!string.IsNullOrEmpty(modelResponse))
                        {
                            responseText = modelResponse;
                            Debug.WriteLine($"Model processing successful: {modelResponse.Substring(0, Math.Min(100, modelResponse.Length))}...");
                        }
                        else
                        {
                            responseText = $"The model '{activeModel.Name}' processed your {(hasSelectedAudio ? "audio" : "image")} file, but returned an empty response. This might indicate that the model needs additional configuration for media processing.";
                        }
                    }
                    catch (Exception modelEx)
                    {
                        Debug.WriteLine($"Model execution failed: {modelEx.Message}");
                        responseText = $"I attempted to process your {(hasSelectedAudio ? "audio" : "image")} file with '{activeModel.Name}', but encountered an error: {modelEx.Message}\n\n" +
                                      $"This might be because:\n" +
                                      $"• The model requires specific media processing libraries\n" +
                                      $"• The Python script needs updates for media file handling\n" +
                                      $"• The model doesn't support direct file path processing\n\n" +
                                      $"File location: {mediaPath}";
                    }
                }
                else
                {
                    // No valid model available
                    responseText = $"I can see you've uploaded a {(hasSelectedAudio ? "audio" : "image")} file: '{(hasSelectedAudio ? selectedAudioName : selectedImageName)}', but no compatible model is properly configured.\n\n" +
                                  $"To process this media file, please ensure:\n" +
                                  $"• The active model supports {(hasSelectedAudio ? "audio" : "image")} processing\n" +
                                  $"• The model is properly downloaded and configured\n\n" +
                                  $"File location: {mediaPath}";
                }

                // Remove processing message and add actual response
                chatMessages.Remove(processingMessage);

                var aiMessage = new ChatMessage(responseText, false, activeModel?.Name ?? "System", includeInHistory: true)
                {
                    LLMSource = "local"
                };
                chatMessages.Add(aiMessage);
                Debug.WriteLine($"Added media processing response to chat. ChatMessages count: {chatMessages.Count}");
            }
            catch (Exception ex)
            {
                // Remove processing message and show error
                chatMessages.Remove(processingMessage);

                var errorMessage = new ChatMessage($"Error processing media file: {ex.Message}", false, "System", includeInHistory: true);
                chatMessages.Add(errorMessage);
                Debug.WriteLine($"Error processing media: {ex}");
            }

            // Clear the selected media after processing
            clearMedia();
        }

        public async Task<bool> ValidateMediaUploadAsync(
            bool hasSelectedImage,
            bool hasSelectedAudio,
            bool supportsImageInput,
            bool supportsAudioInput,
            Func<string, string, string, Task> showAlert)
        {
            try
            {
                // Check if we have media but no compatible models
                if (hasSelectedImage && !supportsImageInput)
                {
                    await showAlert("No Image Models Active",
                        "You have selected an image, but no image processing models are currently active. Please activate an image model or remove the image to continue.",
                        "OK");
                    return false;
                }

                if (hasSelectedAudio && !supportsAudioInput)
                {
                    await showAlert("No Audio Models Active",
                        "You have selected an audio file, but no audio processing models are currently active. Please activate an audio model or remove the audio to continue.",
                        "OK");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error validating media upload: {ex.Message}");
                return false;
            }
        }

        public async Task SuggestModelsForMediaTypeAsync(
            bool hasSelectedImage,
            bool hasSelectedAudio,
            bool supportsImageInput,
            bool supportsAudioInput,
            Func<string, string, string, Task> showAlert)
        {
            try
            {
                var missingTypes = new System.Collections.Generic.List<string>();
                var suggestions = new System.Collections.Generic.List<string>();

                if (hasSelectedImage && !supportsImageInput)
                {
                    missingTypes.Add("image");
                }

                if (hasSelectedAudio && !supportsAudioInput)
                {
                    missingTypes.Add("audio");
                }

                foreach (var missingType in missingTypes)
                {
                    switch (missingType.ToLower())
                    {
                        case "image":
                            suggestions.AddRange(new[] {
                                "• openai/clip-vit-base-patch32 (CLIP Vision)",
                                "• facebook/detr-resnet-50 (Object Detection)"
                            });
                            break;
                        case "audio":
                            suggestions.AddRange(new[] {
                                "• openai/whisper-base (Speech Recognition)",
                                "• facebook/wav2vec2-base-960h (Audio Processing)",
                                "• microsoft/speecht5_asr (Speech-to-Text)",
                                "• facebook/hubert-base-ls960 (Audio Understanding)"
                            });
                            break;
                    }
                }

                if (suggestions.Any())
                {
                    var suggestionText = string.Join("\n", suggestions);
                    await showAlert("Recommended Models",
                        $"Here are some recommended models for the media types you're trying to upload:\n\n{suggestionText}\n\n" +
                        "You can search for these models using the HuggingFace search feature.",
                        "OK");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error suggesting models: {ex.Message}");
            }
        }
    }
}
