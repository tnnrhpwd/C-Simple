using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace CSimple.Services
{
    /// <summary>
    /// Simplified Python bootstrapper that focuses on system Python or API-only mode
    /// </summary>
    public class PythonBootstrapper
    {
        private readonly string _appDataPath;
        private readonly string _scriptsPath;
        private string _pythonPath;
        private bool _useApiOnlyMode = false;

        // Events for status updates
        public event EventHandler<string> StatusChanged;
        public event EventHandler<double> ProgressChanged;

        public PythonBootstrapper()
        {
            _appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CSimple");
            _scriptsPath = Path.Combine(_appDataPath, "scripts");
            Directory.CreateDirectory(_scriptsPath);
        }

        /// <summary>
        /// Gets the Python executable path
        /// </summary>
        public string PythonExecutablePath => _pythonPath ?? "python";

        /// <summary>
        /// Initializes Python environment
        /// </summary>
        public async Task<bool> InitializeAsync()
        {
            try
            {
                UpdateStatus("Checking for Python installation...");

                // First, try to find system Python
                if (await FindSystemPythonAsync())
                {
                    UpdateStatus("Found system Python installation.");
                    return true;
                }

                // If no system Python, set to API-only mode
                UpdateStatus("No Python installation found. Using API-only mode.");
                _useApiOnlyMode = true;

                // Still ensure we have the API runtime script
                await EnsureApiRuntimeScriptAsync();

                return false; // Return false to indicate we're in API-only mode
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error initializing Python: {ex.Message}");
                Debug.WriteLine($"Python bootstrapper error: {ex}");
                _useApiOnlyMode = true;
                await EnsureApiRuntimeScriptAsync();
                return false;
            }
        }

        /// <summary>
        /// Installs required packages for the application
        /// </summary>
        public async Task<bool> InstallRequiredPackagesAsync()
        {
            if (_useApiOnlyMode)
            {
                UpdateStatus("API-only mode active, no packages to install.");
                return true; // Nothing to install in API-only mode
            }

            try
            {
                UpdateStatus("Installing required packages...");

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = PythonExecutablePath,
                        Arguments = "-m pip install requests urllib3 --user",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                StringBuilder output = new StringBuilder();
                StringBuilder error = new StringBuilder();

                process.OutputDataReceived += (sender, e) => { if (e.Data != null) output.AppendLine(e.Data); };
                process.ErrorDataReceived += (sender, e) => { if (e.Data != null) error.AppendLine(e.Data); };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    UpdateStatus($"Package installation failed: {error}");
                    return false;
                }

                UpdateStatus("Required packages installed.");
                return true;
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error installing packages: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets the path to a specific installed Python script
        /// </summary>
        public string GetScriptPath(string scriptName)
        {
            // If in API-only mode, use the api_runtime.py script regardless of what's requested
            if (_useApiOnlyMode && scriptName == "run_hf_model.py")
            {
                return Path.Combine(_scriptsPath, "api_runtime.py");
            }

            return Path.Combine(_scriptsPath, scriptName);
        }

        /// <summary>
        /// Copies scripts from the application directory to the persistent scripts location
        /// </summary>
        public async Task<bool> CopyScriptsAsync(string sourceDir)
        {
            try
            {
                if (!Directory.Exists(sourceDir))
                {
                    Debug.WriteLine($"Source scripts directory not found: {sourceDir}");
                    return false;
                }

                foreach (string scriptFile in Directory.GetFiles(sourceDir, "*.py"))
                {
                    string fileName = Path.GetFileName(scriptFile);
                    string destPath = Path.Combine(_scriptsPath, fileName);
                    File.Copy(scriptFile, destPath, overwrite: true);
                    Debug.WriteLine($"Copied script {fileName} to {destPath}");
                }

                // Always ensure API runtime script exists
                await EnsureApiRuntimeScriptAsync();

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error copying scripts: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Executes a Python script
        /// </summary>
        public async Task<(string Output, string Error, int ExitCode)> ExecuteScriptAsync(
            string scriptPath, string arguments, int timeoutMs = 120000)
        {
            string pythonPath = PythonExecutablePath;

            // If in API-only mode, route all executions to our api_runtime.py
            if (_useApiOnlyMode)
            {
                scriptPath = GetScriptPath("api_runtime.py");
            }

            Debug.WriteLine($"Executing script: {pythonPath} \"{scriptPath}\" {arguments}");

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = pythonPath,
                    Arguments = $"\"{scriptPath}\" {arguments}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            process.OutputDataReceived += (s, e) =>
            {
                if (e.Data != null) outputBuilder.AppendLine(e.Data);
            };

            process.ErrorDataReceived += (s, e) =>
            {
                if (e.Data != null) errorBuilder.AppendLine(e.Data);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Use a timeout
            bool exited = await Task.Run(() => process.WaitForExit(timeoutMs));

            if (!exited)
            {
                try { process.Kill(); } catch { }
                return ("", "Script execution timed out", -1);
            }

            return (outputBuilder.ToString(), errorBuilder.ToString(), process.ExitCode);
        }

        #region Private Helper Methods

        private async Task<bool> FindSystemPythonAsync()
        {
            try
            {
                // Try "python" and "python3" commands to see if Python is in PATH
                string[] pythonCommands = { "python", "python3" };

                foreach (var cmd in pythonCommands)
                {
                    try
                    {
                        var process = new Process
                        {
                            StartInfo = new ProcessStartInfo
                            {
                                FileName = cmd,
                                Arguments = "--version",
                                UseShellExecute = false,
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                                CreateNoWindow = true
                            }
                        };

                        process.Start();
                        string output = await process.StandardOutput.ReadToEndAsync();
                        string error = await process.StandardError.ReadToEndAsync();
                        await process.WaitForExitAsync();

                        if (process.ExitCode == 0)
                        {
                            // Capture combined output
                            string version = string.IsNullOrEmpty(output) ? error : output;

                            if (version.Contains("Python 3"))
                            {
                                _pythonPath = cmd;
                                UpdateStatus($"Found Python: {version.Trim()}");
                                return true;
                            }
                        }
                    }
                    catch
                    {
                        // Try next command
                    }
                }

                // Try common installation paths on Windows
                if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
                {
                    string[] commonPaths = {
                        @"C:\Python39\python.exe",
                        @"C:\Program Files\Python39\python.exe",
                        @"C:\Program Files (x86)\Python39\python.exe",
                    };

                    foreach (var path in commonPaths)
                    {
                        if (File.Exists(path))
                        {
                            _pythonPath = path;
                            UpdateStatus($"Found Python at: {path}");
                            return true;
                        }
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error finding system Python: {ex.Message}");
                return false;
            }
        }

        private async Task EnsureApiRuntimeScriptAsync()
        {
            string apiRuntimePath = Path.Combine(_scriptsPath, "api_runtime.py");

            // Check if the API script already exists
            if (File.Exists(apiRuntimePath))
                return;

            // First check if we have it in the bundled scripts
            string bundledScriptPath = Path.Combine(AppContext.BaseDirectory, "Scripts", "api_runtime.py");
            if (File.Exists(bundledScriptPath))
            {
                File.Copy(bundledScriptPath, apiRuntimePath, overwrite: true);
                return;
            }

            // If not, create the minimal API script
            string minimalApiScript = @"#!/usr/bin/env python3
import argparse
import json
import sys
import urllib.request
import urllib.parse
import urllib.error
import ssl
import time
from datetime import datetime

def parse_arguments():
    parser = argparse.ArgumentParser(description=""C-Simple API-Only Runtime"")
    parser.add_argument(""--model_id"", type=str, required=True, help=""HuggingFace model ID"")
    parser.add_argument(""--input"", type=str, required=True, help=""Input text or data"")
    parser.add_argument(""--api_key"", type=str, help=""Optional HuggingFace API key"")
    parser.add_argument(""--timeout"", type=int, default=30, help=""Request timeout in seconds"")
    return parser.parse_args()

def call_huggingface_api(model_id, inputs, api_key=None, timeout=30):
    api_url = f""https://api-inference.huggingface.co/models/{model_id}""
    payload = {""inputs"": inputs}
    data = json.dumps(payload).encode('utf-8')
    headers = {'Content-Type': 'application/json'}
    if api_key: headers['Authorization'] = f'Bearer {api_key}'
    ssl_context = ssl._create_unverified_context()
    try:
        req = urllib.request.Request(api_url, data=data, headers=headers, method='POST')
        with urllib.request.urlopen(req, timeout=timeout, context=ssl_context) as response:
            response_data = response.read().decode('utf-8')
            return json.loads(response_data)
    except urllib.error.HTTPError as e:
        if e.code == 429:
            print(""Model is loading, waiting..."", file=sys.stderr)
            time.sleep(20)
            return call_huggingface_api(model_id, inputs, api_key, timeout)
        else:
            error_body = e.read().decode('utf-8')
            print(f""HTTP Error: {e.code} - {error_body}"", file=sys.stderr)
            return {""error"": f""HTTP Error {e.code}"", ""details"": error_body}
    except Exception as e:
        print(f""API request error: {str(e)}"", file=sys.stderr)
        return {""error"": str(e)}

def extract_text_from_api_response(response):
    if isinstance(response, list) and len(response) > 0:
        if isinstance(response[0], dict) and ""generated_text"" in response[0]:
            return response[0][""generated_text""]
    elif isinstance(response, dict):
        if ""generated_text"" in response:
            return response[""generated_text""]
    return str(response)

def main():
    try:
        args = parse_arguments()
        print(f""Calling API for model: {args.model_id}"", file=sys.stderr)
        result = call_huggingface_api(args.model_id, args.input, args.api_key, args.timeout)
        if ""error"" in result:
            print(f""API Error: {result['error']}"", file=sys.stderr)
            return 1
        output_text = extract_text_from_api_response(result)
        print(output_text)
        return 0
    except Exception as e:
        print(f""Error: {str(e)}"", file=sys.stderr)
        return 1

if __name__ == ""__main__"":
    sys.exit(main())";

            await File.WriteAllTextAsync(apiRuntimePath, minimalApiScript);
            UpdateStatus("Created API-only runtime script.");
        }

        private void UpdateStatus(string status)
        {
            Debug.WriteLine($"[PythonBootstrapper] {status}");
            StatusChanged?.Invoke(this, status);
        }

        private void UpdateProgress(double progress)
        {
            ProgressChanged?.Invoke(this, progress);
        }
        #endregion
    }
}
