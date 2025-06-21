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
            NeuralNetworkModel model, string pythonExecutablePath, string huggingFaceScriptPath)
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

                // Escape quotes in input text and handle special characters
                string escapedInput = inputText.Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "");

                // Build arguments with enhanced parameters
                var argumentsBuilder = new StringBuilder();
                argumentsBuilder.Append($"\"{huggingFaceScriptPath}\" --model_id \"{modelId}\" --input \"{escapedInput}\"");

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

                Debug.WriteLine($"Starting process: {processStartInfo.FileName} {processStartInfo.Arguments}");

                using var process = new Process { StartInfo = processStartInfo };
                process.Start();

                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();

                // Determine timeout based on model type (CPU-friendly models get less time)
                var cpuFriendlyModels = new[] { "gpt2", "distilgpt2", "microsoft/DialoGPT" };
                bool isCpuFriendly = cpuFriendlyModels.Any(cpu => modelId.Contains(cpu, StringComparison.OrdinalIgnoreCase));
                int timeoutMs = isCpuFriendly ? 120000 : 300000; // 2 minutes for CPU-friendly, 5 minutes for others

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
                string error = await errorTask;
                int exitCode = process.ExitCode;

                Debug.WriteLine($"Process completed with exit code: {exitCode}");
                Debug.WriteLine($"Output: {output}");
                Debug.WriteLine($"Error: {error}");

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
            if (error.Contains("compute capability") && error.Contains("FP8"))
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
                            var lines = error.Split('\n');
                            var responseLine = lines.FirstOrDefault(l => l.Contains("Response:") || l.Contains("Output:"));
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
            }

            // Clean up and format the output
            string cleanedOutput = output.Trim();

            // Remove any debug prefixes that might have been added
            if (cleanedOutput.StartsWith("Loading model:") || cleanedOutput.StartsWith("Model:"))
            {
                var lines = cleanedOutput.Split('\n');
                cleanedOutput = string.Join('\n', lines.Skip(1)).Trim();
            }

            OutputReceived?.Invoke(cleanedOutput);
            return cleanedOutput;
        }
    }
}
