using CSimple.Models;
using CSimple.Services.AppModeService;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSimple.Services
{
    public class ModelExecutionService
    {
        private readonly CSimple.Services.AppModeService.AppModeService _appModeService;

        // Events for status updates
        public event Action<string> StatusUpdated;
        public event Action<string> OutputReceived;
        public event Action<string, Exception> ErrorOccurred;

        public ModelExecutionService(CSimple.Services.AppModeService.AppModeService appModeService)
        {
            _appModeService = appModeService;
        }

        /// <summary>
        /// Executes a HuggingFace model with enhanced error handling and parameter optimization
        /// </summary>
        public async Task<string> ExecuteHuggingFaceModelAsyncEnhanced(string modelId, string inputText,
            NeuralNetworkModel model, string pythonExecutablePath, string huggingFaceScriptPath, string localModelPath = null)
        {
            // Validate inputs
            if (string.IsNullOrEmpty(pythonExecutablePath))
            {
                throw new InvalidOperationException("Python is not available. Please install Python and restart the application.");
            }

            if (!File.Exists(huggingFaceScriptPath))
            {
                throw new FileNotFoundException($"HuggingFace script not found at: {huggingFaceScriptPath}");
            }

            try
            {
                Debug.WriteLine($"Executing Python script with model: {modelId}");
                Debug.WriteLine($"Script path: {huggingFaceScriptPath}");
                Debug.WriteLine($"Python path: {pythonExecutablePath}");

                StatusUpdated?.Invoke($"Executing model {modelId}...");                // Escape quotes in input text and clean up whitespace
                string cleanedInput = inputText.Trim().Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ");
                string escapedInput = cleanedInput.Replace("\"", "\\\"");

                // Build arguments with enhanced parameters
                var argumentsBuilder = new StringBuilder();
                argumentsBuilder.Append($"\"{huggingFaceScriptPath}\" --model_id \"{modelId}\" --input \"{escapedInput}\"");

                // Add local model path if provided to force local-only execution
                if (!string.IsNullOrEmpty(localModelPath) && Directory.Exists(localModelPath))
                {
                    argumentsBuilder.Append($" --local_model_path \"{localModelPath}\"");
                    Debug.WriteLine($"Using local model path for execution: {localModelPath}");
                }

                // Add CPU optimization flag for better local performance
                argumentsBuilder.Append(" --cpu_optimize");

                // Add max length parameter to prevent overly long responses
                int maxLength = Math.Min(200, inputText.Split(' ').Length + 100);
                argumentsBuilder.Append($" --max_length {maxLength}");

                // Add offline mode flag when in offline mode to disable API fallback
                if (_appModeService.CurrentMode == AppMode.Offline)
                {
                    argumentsBuilder.Append(" --offline_mode");
                }

                string arguments = argumentsBuilder.ToString();

                var processStartInfo = new ProcessStartInfo
                {
                    FileName = pythonExecutablePath,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    // Set working directory to script location for better relative path handling
                    WorkingDirectory = Path.GetDirectoryName(huggingFaceScriptPath)
                };

                Debug.WriteLine($"Starting process: {processStartInfo.FileName} {processStartInfo.Arguments}"); using var process = new Process { StartInfo = processStartInfo };

                // Collect stderr output for final processing
                var stderrOutput = new StringBuilder();

                // Set up real-time stderr reading for progress updates
                process.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        Debug.WriteLine($"Python stderr: {e.Data}");
                        stderrOutput.AppendLine(e.Data);                        // Update status if it looks like a progress message
                        if (e.Data.Contains("Progress:") || e.Data.Contains("Loading") || e.Data.Contains("Downloading") ||
                            e.Data.Contains("Installing") || e.Data.Contains("Tokenizer"))
                        {
                            StatusUpdated?.Invoke(e.Data);
                        }
                        // Also update status for important error messages
                        else if (e.Data.Contains("Fast tokenizer failed") || e.Data.Contains("SentencePiece") ||
                                e.Data.Contains("Attempting to load slow tokenizer"))
                        {
                            StatusUpdated?.Invoke(e.Data);
                        }
                    }
                };

                process.Start();
                process.BeginErrorReadLine(); // Start async reading of stderr

                var outputTask = process.StandardOutput.ReadToEndAsync();                // Determine timeout based on model type and first-time setup
                var cpuFriendlyModels = new[] { "gpt2", "distilgpt2", "microsoft/DialoGPT" };
                bool isCpuFriendly = cpuFriendlyModels.Any(cpu => modelId.Contains(cpu, StringComparison.OrdinalIgnoreCase));

                // Increase timeout for first-time downloads and package installations
                int baseTimeoutMs = isCpuFriendly ? 120000 : 300000; // 2 min for CPU-friendly, 5 min for others
                int timeoutMs = baseTimeoutMs * 2; // Double timeout for package installation scenarios

                StatusUpdated?.Invoke($"Processing with {modelId} (timeout: {timeoutMs / 1000}s)...");

                // Wait for process to complete with dynamic timeout
                bool completed = process.WaitForExit(timeoutMs);

                if (!completed)
                {
                    process.Kill();
                    throw new TimeoutException($"Model execution timed out after {timeoutMs / 1000} seconds. " +
                        (isCpuFriendly ? "Try a shorter input message." : "Large models may require more time on first run."));
                }
                string output = await outputTask;
                string error = stderrOutput.ToString(); // Use collected stderr instead of reading again
                int exitCode = process.ExitCode;

                Debug.WriteLine($"Process completed with exit code: {exitCode}");
                Debug.WriteLine($"Raw Output: '{output}'");
                Debug.WriteLine($"Raw Error: '{error}'");

                if (exitCode != 0)
                {
                    throw ProcessModelExecutionError(modelId, exitCode, error);
                }

                // Enhanced output processing
                return ProcessModelOutput(modelId, output, error);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error running model: {ex.Message}");
                ErrorOccurred?.Invoke($"Model execution failed for {modelId}", ex);
                throw; // Re-throw to be handled by caller
            }
        }

        /// <summary>
        /// Executes a HuggingFace model with basic error handling
        /// </summary>
        public async Task<string> ExecuteHuggingFaceModelAsync(string modelId, string inputText,
            string pythonExecutablePath, string huggingFaceScriptPath)
        {
            // Validate inputs
            if (string.IsNullOrEmpty(pythonExecutablePath))
            {
                throw new InvalidOperationException("Python is not available. Please install Python and restart the application.");
            }

            if (!File.Exists(huggingFaceScriptPath))
            {
                throw new FileNotFoundException($"HuggingFace script not found at: {huggingFaceScriptPath}");
            }

            try
            {
                Debug.WriteLine($"Executing Python script with model: {modelId}");
                Debug.WriteLine($"Script path: {huggingFaceScriptPath}");
                Debug.WriteLine($"Python path: {pythonExecutablePath}");

                StatusUpdated?.Invoke($"Executing model {modelId}...");

                // Escape quotes in input text
                string escapedInput = inputText.Replace("\"", "\\\"");
                string arguments = $"\"{huggingFaceScriptPath}\" --model_id \"{modelId}\" --input \"{escapedInput}\"";

                var processStartInfo = new ProcessStartInfo
                {
                    FileName = pythonExecutablePath,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                Debug.WriteLine($"Starting process: {processStartInfo.FileName} {processStartInfo.Arguments}");

                using var process = new Process { StartInfo = processStartInfo };
                process.Start();

                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();

                // Wait for process to complete with timeout
                bool completed = process.WaitForExit(180000); // 3 minutes timeout for large models

                if (!completed)
                {
                    process.Kill();
                    throw new TimeoutException("Model execution timed out after 3 minutes. Large models may take longer to load initially.");
                }

                string output = await outputTask;
                string error = await errorTask;
                int exitCode = process.ExitCode;

                Debug.WriteLine($"Process completed with exit code: {exitCode}");
                Debug.WriteLine($"Output: {output}");
                Debug.WriteLine($"Error: {error}");

                if (exitCode != 0)
                {
                    throw ProcessModelExecutionError(modelId, exitCode, error);
                }

                // Check if we got valid output
                return ProcessModelOutput(modelId, output, error);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error running model: {ex.Message}");
                ErrorOccurred?.Invoke($"Model execution failed for {modelId}", ex);
                throw; // Re-throw to be handled by caller
            }
        }

        /// <summary>
        /// Installs the accelerate package for enhanced model support
        /// </summary>
        public async Task<bool> InstallAcceleratePackageAsync(string pythonExecutablePath)
        {
            try
            {
                Debug.WriteLine("Installing accelerate package...");
                StatusUpdated?.Invoke("Installing accelerate package...");

                var processStartInfo = new ProcessStartInfo
                {
                    FileName = pythonExecutablePath,
                    Arguments = "-m pip install accelerate",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = processStartInfo };
                process.Start();

                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();

                bool completed = process.WaitForExit(60000); // 1 minute timeout

                if (!completed)
                {
                    process.Kill();
                    Debug.WriteLine("Accelerate installation timed out");
                    StatusUpdated?.Invoke("Accelerate installation timed out");
                    return false;
                }

                string output = await outputTask;
                string error = await errorTask;
                int exitCode = process.ExitCode;

                Debug.WriteLine($"Accelerate installation completed with exit code: {exitCode}");
                Debug.WriteLine($"Output: {output}");
                Debug.WriteLine($"Error: {error}");

                bool success = exitCode == 0;
                StatusUpdated?.Invoke(success ? "Accelerate package installed successfully" : "Failed to install accelerate package");

                return success;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error installing accelerate package: {ex.Message}");
                ErrorOccurred?.Invoke("Failed to install accelerate package", ex);
                return false;
            }
        }        /// <summary>
                 /// Suggests CPU-friendly models when execution fails
                 /// </summary>
        public Task<string[]> GetCpuFriendlyModelSuggestions()
        {
            return Task.FromResult(new[]
            {
                "gpt2",
                "distilgpt2",
                "microsoft/DialoGPT-small",
                "facebook/bart-base",
                "t5-small"
            });
        }
        private Exception ProcessModelExecutionError(string modelId, int exitCode, string error)
        {
            // Enhanced error handling with specific suggestions

            // Handle tokenizer compatibility issues first
            if (error.Contains("Fast tokenizer failed") || error.Contains("SentencePiece") || error.Contains("Tiktoken"))
            {
                return new Exception($"Tokenizer compatibility issue with model '{modelId}'. " +
                    "This model uses a SentencePiece or Tiktoken tokenizer that requires additional setup. " +
                    "The script will attempt to install required packages and use a fallback tokenizer.");
            }
            else if (error.Contains("Both fast and slow tokenizers failed"))
            {
                return new Exception($"Critical tokenizer error for model '{modelId}'. " +
                    "Both fast and slow tokenizers failed to load. This model may not be compatible with the current environment. " +
                    "Try a different model like 'gpt2' or 'microsoft/DialoGPT-medium'.");
            }
            else if (error.Contains("compute capability") && error.Contains("FP8"))
            {
                return new Exception($"Model '{modelId}' requires FP8 quantization (GPU compute capability 8.9+). " +
                    "Your RTX 3090 (8.6) is detected but doesn't support FP8. Switch to online mode or try a different model variant.");
            }
            else if (error.Contains("No GPU or XPU found") && error.Contains("FP8 quantization"))
            {
                return new Exception($"Model '{modelId}' requires GPU acceleration for FP8 quantization. " +
                    "Consider trying a CPU-friendly model like 'gpt2' or 'distilgpt2' for local execution.");
            }
            else if (error.Contains("accelerate") && error.Contains("FP8"))
            {
                return new Exception("Model requires 'accelerate' package for FP8 quantization support. " +
                    "Installing this package automatically...");
            }
            else if (error.Contains("ModuleNotFoundError") || error.Contains("ImportError"))
            {
                return new Exception("Required Python packages are missing. Please install transformers and torch.");
            }
            else if (error.Contains("OutOfMemoryError") || error.Contains("CUDA out of memory"))
            {
                return new Exception($"Model '{modelId}' is too large for available memory. " +
                    "Try closing other applications or switching to a smaller model like 'distilgpt2'.");
            }
            else if (error.Contains("AUTHENTICATION_ERROR") || error.Contains("AUTHENTICATION_REQUIRED"))
            {
                if (_appModeService.CurrentMode == AppMode.Offline)
                {
                    return new Exception($"Model '{modelId}' requires authentication and cannot be used in offline mode. " +
                        "Switch to online mode or try a public model like 'gpt2' or 'distilgpt2'.");
                }
                else
                {
                    return new Exception($"Model '{modelId}' requires a HuggingFace API key for access. " +
                        "Get a free API key from https://huggingface.co/settings/tokens or try a public model like 'gpt2'.");
                }
            }
            else if (error.Contains("API Error 401") || error.Contains("Invalid username or password"))
            {
                if (_appModeService.CurrentMode == AppMode.Offline)
                {
                    return new Exception($"Authentication failed for model '{modelId}' and API fallback is disabled in offline mode. " +
                        "Switch to online mode or try a public model like 'gpt2' or 'distilgpt2'.");
                }
                else
                {
                    return new Exception($"Authentication failed for model '{modelId}'. " +
                        "This model requires a HuggingFace API key or try a public model like 'gpt2' or 'distilgpt2'.");
                }
            }
            else
            {
                return new Exception($"Script failed with exit code {exitCode}. Error: {error}");
            }
        }
        private string ProcessModelOutput(string modelId, string output, string error)
        {
            if (string.IsNullOrWhiteSpace(output))
            {
                if (string.IsNullOrWhiteSpace(error))
                {
                    return "Model processed the input but generated no text output. Try rephrasing your input or using a different model.";
                }
                else
                {
                    // Handle various warning/info scenarios
                    if (error.Contains("AUTHENTICATION_ERROR") || error.Contains("AUTHENTICATION_REQUIRED"))
                    {
                        throw new Exception($"Model requires authentication. Get a HuggingFace API key from https://huggingface.co/settings/tokens or try a public model like 'gpt2'.");
                    }
                    else if (error.Contains("API Error") || error.Contains("Falling back to HuggingFace API"))
                    {
                        if (_appModeService.CurrentMode == AppMode.Offline)
                        {
                            throw new Exception($"Model '{modelId}' failed to run locally and API fallback is disabled in offline mode. " +
                                "Switch to online mode or try a CPU-friendly model like 'gpt2' or 'distilgpt2'.");
                        }
                        else
                        {
                            // Extract meaningful response from API fallback
                            var errorLines = error.Split('\n');
                            var responseLine = errorLines.FirstOrDefault(l => l.Contains("Response:") || l.Contains("Output:"));
                            if (!string.IsNullOrEmpty(responseLine))
                            {
                                return responseLine.Substring(responseLine.IndexOf(':') + 1).Trim();
                            }
                            return $"Local execution failed, used HuggingFace API: {error}";
                        }
                    }
                    else
                    {
                        return $"Model execution completed with warnings: {error}";
                    }
                }
            }            // Clean up and format the output
            string cleanedOutput = output.Trim();

            // Remove package installation messages that might be mixed with output
            var lines = cleanedOutput.Split('\n');
            var filteredLines = lines.Where(line =>
                !string.IsNullOrWhiteSpace(line) &&
                !line.Contains("Requirement already satisfied:") &&
                !line.Contains("Installing collected packages:") &&
                !line.Contains("Successfully installed") &&
                !line.StartsWith("Loading model:") &&
                !line.StartsWith("Model:") &&
                !line.Contains("Cache size:") &&
                !line.Contains("Progress:") &&
                !line.Contains("✓") &&
                !line.Contains("Downloading") &&
                !line.Contains("Loading tokenizer") &&
                !line.Contains("WARNING:") &&
                !line.Contains("FutureWarning:") &&
                line.Trim().Length > 0).ToArray();

            if (filteredLines.Any())
            {
                cleanedOutput = string.Join("\n", filteredLines).Trim();
            }

            // If we still don't have meaningful output, check stderr for the actual generated text
            if (string.IsNullOrWhiteSpace(cleanedOutput) || cleanedOutput.Length < 5)
            {
                var errorLines = error.Split('\n');
                var cleanedTextLine = errorLines.FirstOrDefault(l => l.Contains("Cleaned generated text:"));
                if (!string.IsNullOrEmpty(cleanedTextLine))
                {
                    // Extract the text between single quotes
                    var startIndex = cleanedTextLine.IndexOf("'") + 1;
                    var endIndex = cleanedTextLine.LastIndexOf("'");
                    if (startIndex > 0 && endIndex > startIndex)
                    {
                        cleanedOutput = cleanedTextLine.Substring(startIndex, endIndex - startIndex);
                    }
                }
            }

            // Debug the processing
            Debug.WriteLine($"Original output lines: {lines.Length}");
            Debug.WriteLine($"Filtered output lines: {filteredLines.Length}");
            Debug.WriteLine($"Final cleaned output: '{cleanedOutput}'");

            // Final validation
            if (string.IsNullOrWhiteSpace(cleanedOutput))
            {
                return "Model processed the input but generated no readable text output. Try rephrasing your input or using a different model.";
            }

            OutputReceived?.Invoke(cleanedOutput);
            return cleanedOutput;
        }

        /// <summary>
        /// Test method to verify output processing logic
        /// </summary>
        public string TestOutputProcessing(string testOutput, string testError)
        {
            return ProcessModelOutput("test-model", testOutput, testError);
        }
    }
}
