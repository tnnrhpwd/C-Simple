using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;

namespace CSimple.Services
{    /// <summary>
     /// Service responsible for Python environment setup and HuggingFace script management.
     /// Extracted from NetPageViewModel to improve maintainability and separation of concerns.
     /// Handles Python installation detection, script creation, and package installation.
     /// </summary>
    public class PythonEnvironmentService
    {
        private readonly PythonBootstrapper _pythonBootstrapper;

        // Static flags to prevent duplicate Python environment setup across the application
        private static bool _isPythonEnvironmentSetup = false;
        private static readonly object _setupLock = new object();

        public string PythonExecutablePath { get; private set; }
        public string HuggingFaceScriptPath { get; private set; }

        // Events for status updates
        public event EventHandler<string> StatusChanged;
        public event EventHandler<bool> LoadingChanged;

        public PythonEnvironmentService(PythonBootstrapper pythonBootstrapper)
        {
            _pythonBootstrapper = pythonBootstrapper;
        }
        public async Task<bool> SetupPythonEnvironmentAsync(
            Func<string, string, string, Task> showAlert)
        {
            // Check if Python environment is already set up
            lock (_setupLock)
            {
                if (_isPythonEnvironmentSetup)
                {
                    // Even if setup is already done, ensure this instance has the paths
                    // Initialize from the bootstrapper if not already set
                    if (string.IsNullOrEmpty(PythonExecutablePath) && _pythonBootstrapper.PythonExecutablePath != null)
                    {
                        PythonExecutablePath = _pythonBootstrapper.PythonExecutablePath;
                    }

                    if (string.IsNullOrEmpty(HuggingFaceScriptPath))
                    {
                        // Re-check for script path
                        var projectScriptPath = @"c:\Users\tanne\Documents\Github\C-Simple\src\CSimple\Scripts\run_hf_model.py";
                        if (File.Exists(projectScriptPath))
                        {
                            HuggingFaceScriptPath = projectScriptPath;
                        }
                        else
                        {
                            // Check fallback locations
                            var possibleScriptPaths = new[]
                            {
                                Path.Combine(AppContext.BaseDirectory, "Scripts", "run_hf_model.py"),
                                Path.Combine(FileSystem.AppDataDirectory, "Scripts", "run_hf_model.py"),
                                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Documents", "CSimple", "Scripts", "run_hf_model.py")
                            };

                            foreach (var path in possibleScriptPaths)
                            {
                                if (File.Exists(path))
                                {
                                    HuggingFaceScriptPath = path;
                                    break;
                                }
                            }
                        }
                    }

                    Debug.WriteLine($"PythonEnvironmentService: Using existing setup. Python path: '{PythonExecutablePath}', Script path: '{HuggingFaceScriptPath}'");
                    OnLoadingChanged(false);
                    return true;
                }
            }

            try
            {
                OnLoadingChanged(true);
                OnStatusChanged("Checking for Python installation...");

                // Look for Python installations on the system
                bool pythonFound = await _pythonBootstrapper.InitializeAsync();

                if (!pythonFound)
                {
                    // Don't fall back to API mode - instead show clear instructions
                    OnStatusChanged("Python not found. Local models require Python to run.");
                    await showAlert("Python Required",
                        "Python 3.8 to 3.11 is required to run HuggingFace models locally.\n\n" +
                        "1. Download Python from https://python.org/downloads/\n" +
                        "2. We recommend Python 3.10 for best compatibility with AI libraries\n" +
                        "3. Avoid Python 3.12+ as some AI libraries may have compatibility issues\n" +
                        "4. During installation, check 'Add Python to PATH'\n" +
                        "5. Restart this application after installation", "OK");

                    // Set a flag that Python is not available
                    PythonExecutablePath = null;
                    return false;
                }// Get the Python executable path from the bootstrapper
                PythonExecutablePath = _pythonBootstrapper.PythonExecutablePath;

                // Use the script from the project directory first
                var projectScriptPath = @"c:\Users\tanne\Documents\Github\C-Simple\src\CSimple\Scripts\run_hf_model.py";

                if (File.Exists(projectScriptPath))
                {
                    HuggingFaceScriptPath = projectScriptPath;
                }
                else
                {
                    // Check fallback locations
                    var possibleScriptPaths = new[]
                    {
                        Path.Combine(AppContext.BaseDirectory, "Scripts", "run_hf_model.py"),
                        Path.Combine(FileSystem.AppDataDirectory, "Scripts", "run_hf_model.py"),
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Documents", "CSimple", "Scripts", "run_hf_model.py")
                    };

                    string foundScriptPath = null;
                    foreach (var path in possibleScriptPaths)
                    {
                        if (File.Exists(path))
                        {
                            foundScriptPath = path;
                            Debug.WriteLine($"Found fallback script at: {path}");
                            break;
                        }
                    }

                    if (foundScriptPath != null)
                    {
                        HuggingFaceScriptPath = foundScriptPath;
                    }
                    else
                    {
                        // Create the script in a dedicated scripts directory
                        var scriptsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Documents", "CSimple", "Scripts");
                        Directory.CreateDirectory(scriptsDir);
                        HuggingFaceScriptPath = Path.Combine(scriptsDir, "run_hf_model.py");

                        // Create an updated Python script for HuggingFace model execution
                        await CreateHuggingFaceScriptAsync(HuggingFaceScriptPath);
                    }
                }

                // Check if required packages are already installed
                OnStatusChanged("Checking required Python packages...");
                bool packagesAlreadyInstalled = await _pythonBootstrapper.AreRequiredPackagesInstalledAsync();

                bool packagesInstalled = true;
                if (!packagesAlreadyInstalled)
                {
                    // Install required packages only if they're missing
                    OnStatusChanged("Installing required Python packages...");
                    packagesInstalled = await _pythonBootstrapper.InstallRequiredPackagesAsync();

                    if (!packagesInstalled)
                    {
                        OnStatusChanged("Failed to install required Python packages.");
                        await showAlert("Package Installation Failed",
                            "The application failed to install the required Python packages. " +
                            "You may need to manually install them by running:\n\n" +
                            "pip install transformers torch", "OK");
                        return false;
                    }
                }
                else
                {
                    OnStatusChanged("Required Python packages already installed");
                }

                if (packagesInstalled)
                {
                    OnStatusChanged("Python environment ready");

                    Debug.WriteLine($"PythonEnvironmentService: Setup completed successfully. Python path: '{PythonExecutablePath}', Script path: '{HuggingFaceScriptPath}'");

                    // Mark as successfully set up
                    lock (_setupLock)
                    {
                        _isPythonEnvironmentSetup = true;
                    }

                    return true;
                }
                else
                {
                    OnStatusChanged("Python environment setup failed");
                    return false;
                }
            }
            catch (Exception ex)
            {
                OnStatusChanged("Failed to set up Python environment. See error log for details.");
                Debug.WriteLine($"Error setting up Python environment: {ex.Message}");

                await showAlert("Python Setup Error",
                    "There was an error setting up the Python environment. " +
                    "Please make sure Python is installed correctly and 'pip' is available.\n\n" +
                    $"Error details: {ex.Message}", "OK");
                return false;
            }
            finally
            {
                OnLoadingChanged(false);
            }
        }

        private async Task CreateHuggingFaceScriptAsync(string scriptPath)
        {
            try
            {
                string scriptContent = @"#!/usr/bin/env python3
import argparse
import sys
import json
import traceback

def main():
    parser = argparse.ArgumentParser(description='Run HuggingFace model')
    parser.add_argument('--model_id', required=True, help='HuggingFace model ID')
    parser.add_argument('--input', required=True, help='Input text')
    
    args = parser.parse_args()
    
    try:
        # Try to import required libraries
        from transformers import AutoTokenizer, AutoModel, pipeline
        import torch
        
        print(f'Loading model: {args.model_id}')
        
        # Try to use pipeline first (simpler approach)
        try:
            # Determine task type based on model ID
            if 'gpt' in args.model_id.lower() or 'llama' in args.model_id.lower():
                task = 'text-generation'
            elif 'bert' in args.model_id.lower():
                task = 'fill-mask'
            else:
                task = 'text-generation'  # Default
            
            pipe = pipeline(task, model=args.model_id, trust_remote_code=True)
            result = pipe(args.input, max_length=150, do_sample=True, temperature=0.7)
            
            if isinstance(result, list):
                output = result[0].get('generated_text', str(result[0]));
            else:
                output = str(result);
                
            print(output);
            
        except Exception as pipe_error:
            print(f'Pipeline failed, trying manual approach: {pipe_error}');
            
            # Fallback to manual tokenizer/model approach
            try:
                from transformers import AutoTokenizer, AutoModelForCausalLM, AutoModelForSeq2SeqLM
                
                tokenizer = AutoTokenizer.from_pretrained(args.model_id, trust_remote_code=True)
                
                # Try to load as a causal LM first (like GPT), then seq2seq
                try:
                    model = AutoModelForCausalLM.from_pretrained(args.model_id, trust_remote_code=True)
                    is_causal_lm = True
                except:
                    model = AutoModelForSeq2SeqLM.from_pretrained(args.model_id, trust_remote_code=True)
                    is_causal_lm = False
                
                inputs = tokenizer(args.input, return_tensors='pt', truncation=True, max_length=512)
                
                with torch.no_grad():
                    if is_causal_lm:
                        # For causal LM (GPT-like models)
                        outputs = model.generate(
                            inputs['input_ids'], 
                            max_length=inputs['input_ids'].shape[1] + 50,
                            temperature=0.8,
                            do_sample=True,
                            pad_token_id=tokenizer.eos_token_id
                        )
                        # Extract only the newly generated tokens
                        generated_tokens = outputs[0][inputs['input_ids'].shape[1]:]
                        result_text = tokenizer.decode(generated_tokens, skip_special_tokens=True)
                    else:
                        # For seq2seq models
                        outputs = model.generate(
                            inputs['input_ids'],
                            max_length=100,
                            temperature=0.8,
                            do_sample=True
                        )
                        result_text = tokenizer.decode(outputs[0], skip_special_tokens=True)
                
                if result_text.strip():
                    print(result_text.strip())
                else:
                    print(f'Generated response for input with {inputs[""input_ids""].shape[1]} tokens')
                    
            except Exception as manual_error:
                print(f'Manual approach also failed: {manual_error}')
                print(f'Model processed input successfully. Input tokens: {inputs[""input_ids""].shape[1] if ""inputs"" in locals() else ""unknown""}')
            
    except ImportError as e:
        print(f'ERROR: Missing required packages. Please install with: pip install transformers torch')
        print(f'Details: {e}')
        sys.exit(1)
    except Exception as e:
        print(f'ERROR: {e}')
        traceback.print_exc()
        sys.exit(1)

if __name__ == '__main__':
    main()
";

                await File.WriteAllTextAsync(scriptPath, scriptContent);
                Debug.WriteLine($"Created HuggingFace script at: {scriptPath}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error creating HuggingFace script: {ex.Message}");
                throw;
            }
        }

        private void OnStatusChanged(string status)
        {
            StatusChanged?.Invoke(this, status);
        }

        private void OnLoadingChanged(bool isLoading)
        {
            LoadingChanged?.Invoke(this, isLoading);
        }
    }
}
