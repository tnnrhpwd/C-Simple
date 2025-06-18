using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace CSimple.Services
{    /// <summary>
     /// Python bootstrapper that creates and manages a virtual environment for ML dependencies
     /// </summary>
    public class PythonBootstrapper
    {
        private readonly string _appDataPath;
        private readonly string _scriptsPath;
        private readonly string _venvPath;
        private string _pythonPath = null;
        private string _venvPythonPath = null;
        private bool _venvCreated = false;

        // Events for status updates
        public event EventHandler<string> StatusChanged;
        public event EventHandler<double> ProgressChanged; public PythonBootstrapper()
        {
            _appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CSimple");
            _scriptsPath = Path.Combine(_appDataPath, "scripts");
            _venvPath = Path.Combine(_appDataPath, "venv");
            Directory.CreateDirectory(_scriptsPath);
        }

        /// <summary>
        /// Gets the Python executable path (venv python if available, otherwise system python)
        /// </summary>
        public string PythonExecutablePath => _venvCreated ? _venvPythonPath : _pythonPath;        /// <summary>
                                                                                                   /// Initializes by finding Python and setting up virtual environment
                                                                                                   /// </summary>
        public async Task<bool> InitializeAsync()
        {
            try
            {
                UpdateStatus("Checking for Python installation...");
                UpdateProgress(0.1);

                // Try to find Python in common locations
                if (await FindSystemPythonAsync())
                {
                    UpdateStatus($"Found Python at: {_pythonPath}");
                    UpdateProgress(0.3);

                    // Check if virtual environment exists or create it
                    if (await SetupVirtualEnvironmentAsync())
                    {
                        UpdateStatus("Virtual environment ready");
                        UpdateProgress(1.0);
                        return true;
                    }
                    else
                    {
                        UpdateStatus("Failed to setup virtual environment, using system Python");
                        UpdateProgress(1.0);
                        return true; // Still return true as we have system Python
                    }
                }

                // No Python found
                UpdateStatus("Python not found. Please install Python from python.org");
                UpdateProgress(1.0);
                return false;
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error initializing Python environment: {ex.Message}");
                Debug.WriteLine($"Error in InitializeAsync: {ex}");
                return false;
            }
        }        /// <summary>
                 /// Sets up or verifies the virtual environment
                 /// </summary>
        private async Task<bool> SetupVirtualEnvironmentAsync()
        {
            try
            {
                // Check if venv already exists and is valid
                if (await IsVenvValidAsync())
                {
                    UpdateStatus("Using existing virtual environment");
                    return true;
                }

                UpdateStatus("Creating virtual environment...");
                UpdateProgress(0.4);

                // Create virtual environment
                if (await CreateVirtualEnvironmentAsync())
                {
                    UpdateStatus("Virtual environment created successfully");
                    UpdateProgress(0.6);

                    // Install required packages in the venv
                    if (await InstallPackagesInVenvAsync())
                    {
                        UpdateStatus("Required packages installed in virtual environment");
                        UpdateProgress(0.9);
                        return true;
                    }
                    else
                    {
                        UpdateStatus("Failed to install packages in virtual environment");
                        return false;
                    }
                }
                else
                {
                    UpdateStatus("Failed to create virtual environment");
                    return false;
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error setting up virtual environment: {ex.Message}");
                Debug.WriteLine($"Error in SetupVirtualEnvironmentAsync: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Checks if the virtual environment exists and is valid
        /// </summary>
        private async Task<bool> IsVenvValidAsync()
        {
            try
            {
                // Set the venv python path
                _venvPythonPath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    ? Path.Combine(_venvPath, "Scripts", "python.exe")
                    : Path.Combine(_venvPath, "bin", "python");

                if (!File.Exists(_venvPythonPath))
                {
                    Debug.WriteLine($"Venv python not found at: {_venvPythonPath}");
                    return false;
                }

                // Test if the venv python works
                var result = await ExecuteCommandAsync(_venvPythonPath, "--version");
                if (result.ExitCode == 0 && result.Output.Contains("Python 3"))
                {
                    // Check if required packages are installed
                    var packageResult = await ExecuteCommandAsync(_venvPythonPath, "-c \"import transformers, torch; print('Packages OK')\"");
                    if (packageResult.ExitCode == 0 && packageResult.Output.Contains("Packages OK"))
                    {
                        _venvCreated = true;
                        Debug.WriteLine("Existing venv is valid and has required packages");
                        return true;
                    }
                    else
                    {
                        Debug.WriteLine("Venv exists but missing required packages");
                        _venvCreated = true; // Mark as created so we can install packages
                        return false;
                    }
                }

                Debug.WriteLine("Venv python is not working properly");
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking venv validity: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Creates a new virtual environment
        /// </summary>
        private async Task<bool> CreateVirtualEnvironmentAsync()
        {
            try
            {
                // Remove existing venv if it exists but is invalid
                if (Directory.Exists(_venvPath))
                {
                    try
                    {
                        Directory.Delete(_venvPath, recursive: true);
                        UpdateStatus("Removed invalid virtual environment");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Could not remove existing venv: {ex.Message}");
                        // Continue anyway
                    }
                }

                // Create the virtual environment
                var result = await ExecuteCommandAsync(_pythonPath, $"-m venv \"{_venvPath}\"");

                if (result.ExitCode != 0)
                {
                    Debug.WriteLine($"Failed to create venv. Exit code: {result.ExitCode}, Error: {result.Error}");
                    UpdateStatus("Failed to create virtual environment. Trying with system Python.");
                    return false;
                }

                // Set the venv python path
                _venvPythonPath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    ? Path.Combine(_venvPath, "Scripts", "python.exe")
                    : Path.Combine(_venvPath, "bin", "python");

                // Verify the venv was created
                if (File.Exists(_venvPythonPath))
                {
                    _venvCreated = true;
                    Debug.WriteLine($"Virtual environment created at: {_venvPath}");
                    return true;
                }
                else
                {
                    Debug.WriteLine("Virtual environment creation appeared to succeed but python executable not found");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error creating virtual environment: {ex.Message}");
                UpdateStatus($"Error creating virtual environment: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Installs required packages in the virtual environment
        /// </summary>
        private async Task<bool> InstallPackagesInVenvAsync()
        {
            if (!_venvCreated || string.IsNullOrEmpty(_venvPythonPath))
            {
                UpdateStatus("Virtual environment not available for package installation");
                return false;
            }

            try
            {
                UpdateStatus("Installing required packages in virtual environment...");

                // First, upgrade pip in the venv
                var pipUpgradeResult = await ExecuteCommandAsync(_venvPythonPath, "-m pip install --upgrade pip");
                if (pipUpgradeResult.ExitCode != 0)
                {
                    Debug.WriteLine($"Warning: Failed to upgrade pip: {pipUpgradeResult.Error}");
                    // Continue anyway as this is not critical
                }

                // Install packages with specific versions for better compatibility
                var packages = new[]
                {
                    "torch",
                    "transformers",
                    "accelerate",
                    "tokenizers"
                };

                foreach (var package in packages)
                {
                    UpdateStatus($"Installing {package}...");
                    var result = await ExecuteCommandAsync(_venvPythonPath, $"-m pip install {package} --no-cache-dir");

                    if (result.ExitCode != 0)
                    {
                        Debug.WriteLine($"Failed to install {package}: {result.Error}");
                        UpdateStatus($"Failed to install {package}. Check your internet connection and try again.");
                        return false;
                    }
                    else
                    {
                        Debug.WriteLine($"Successfully installed {package}");
                    }
                }

                // Verify installation
                var verifyResult = await ExecuteCommandAsync(_venvPythonPath, "-c \"import transformers, torch, accelerate; print('All packages installed successfully')\"");
                if (verifyResult.ExitCode == 0)
                {
                    UpdateStatus("All required packages installed successfully");
                    return true;
                }
                else
                {
                    UpdateStatus("Package installation verification failed");
                    Debug.WriteLine($"Package verification failed: {verifyResult.Error}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error installing packages: {ex.Message}");
                Debug.WriteLine($"Error installing packages in venv: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Installs required packages for HuggingFace models (legacy method - now uses venv)
        /// </summary>
        public async Task<bool> InstallRequiredPackagesAsync()
        {
            // If we have a venv, use it
            if (_venvCreated)
            {
                return await InstallPackagesInVenvAsync();
            }

            // Fallback to system Python installation
            if (string.IsNullOrEmpty(_pythonPath))
            {
                UpdateStatus("Python not found. Cannot install packages.");
                return false;
            }

            try
            {
                UpdateStatus("Installing required packages in system Python...");
                UpdateProgress(0.2);

                // Create a process to run pip
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = _pythonPath,
                    Arguments = "-m pip install transformers torch accelerate --upgrade --user",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                using var process = new Process { StartInfo = processStartInfo };
                var output = new StringBuilder();
                var error = new StringBuilder();

                process.OutputDataReceived += (s, e) =>
                {
                    if (e.Data != null)
                    {
                        output.AppendLine(e.Data);
                        UpdateStatus($"Installing: {e.Data}");
                    }
                };
                process.ErrorDataReceived += (s, e) =>
                {
                    if (e.Data != null) error.AppendLine(e.Data);
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                // Add a reasonable timeout
                var timeoutTask = Task.Delay(TimeSpan.FromMinutes(5)); // 5 minute timeout
                var completedTask = await Task.WhenAny(process.WaitForExitAsync(), timeoutTask);

                if (completedTask == timeoutTask)
                {
                    try { process.Kill(); } catch { }
                    UpdateStatus("Package installation timed out. Try installing manually.");
                    return false;
                }

                if (process.ExitCode != 0)
                {
                    UpdateStatus($"Package installation failed: {error}");
                    return false;
                }

                UpdateStatus("Required packages installed successfully.");
                UpdateProgress(1.0);
                return true;
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error installing packages: {ex.Message}");
                Debug.WriteLine($"Error installing packages: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Gets the path to a specific installed Python script
        /// </summary>
        public string GetScriptPath(string scriptName)
        {
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
        }        /// <summary>
                 /// Executes a Python script using the virtual environment if available
                 /// </summary>
        public async Task<(string Output, string Error, int ExitCode)> ExecuteScriptAsync(
            string scriptPath, string arguments, int timeoutMs = 120000)
        {
            // Use venv python if available, otherwise system python
            string pythonToUse = _venvCreated ? _venvPythonPath : _pythonPath;

            if (string.IsNullOrEmpty(pythonToUse))
            {
                return ("", "Python not found. Please install Python and restart the application.", -1);
            }

            try
            {
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = pythonToUse,
                    Arguments = $"\"{scriptPath}\" {arguments}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                Debug.WriteLine($"Executing: {processStartInfo.FileName} {processStartInfo.Arguments}");
                Debug.WriteLine($"Using virtual environment: {_venvCreated}");

                using var process = new Process { StartInfo = processStartInfo };
                var output = new StringBuilder();
                var error = new StringBuilder();

                process.OutputDataReceived += (s, e) => { if (e.Data != null) output.AppendLine(e.Data); };
                process.ErrorDataReceived += (s, e) => { if (e.Data != null) error.AppendLine(e.Data); };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                // Add a reasonable timeout
                var timeoutTask = Task.Delay(timeoutMs);
                var completedTask = await Task.WhenAny(process.WaitForExitAsync(), timeoutTask);

                if (completedTask == timeoutTask)
                {
                    try { process.Kill(); } catch { }
                    return ("", "Script execution timed out.", -1);
                }

                return (output.ToString(), error.ToString(), process.ExitCode);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error executing script: {ex.Message}");
                return ("", $"Error executing script: {ex.Message}", -1);
            }
        }

        #region Private Helper Methods

        private async Task<bool> FindSystemPythonAsync()
        {
            // List of potential Python executable names by platform
            var pythonExecutables = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? new[] { "python.exe", "python3.exe", "py.exe" }
                : new[] { "python", "python3" };

            // Try all executables in PATH first
            foreach (var executable in pythonExecutables)
            {
                try
                {
                    var result = await ExecuteCommandAsync(executable, "--version");
                    if (result.ExitCode == 0 && result.Output.Contains("Python 3"))
                    {
                        // Validate Python version is in our supported range
                        string versionOutput = result.Output.Trim();
                        if (IsValidPythonVersion(versionOutput))
                        {
                            _pythonPath = executable;
                            Debug.WriteLine($"Found Python in PATH: {executable} - {versionOutput}");
                            UpdateStatus($"Found Python: {versionOutput}");
                            return true;
                        }
                        else
                        {
                            Debug.WriteLine($"Found Python but version not in supported range: {versionOutput}");
                            UpdateStatus($"Found unsupported Python version: {versionOutput}. Please install Python 3.8-3.11.");
                            // Continue checking for other Python installations that might meet our version requirements
                        }
                    }
                }
                catch
                {
                    // Continue to next executable
                }
            }

            // If we're on Windows, check common installation paths
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Try common Windows install locations - newer Python versions first
                var commonPaths = GetCommonWindowsPythonPaths();
                foreach (var path in commonPaths)
                {
                    if (File.Exists(path))
                    {
                        try
                        {
                            var result = await ExecuteCommandAsync(path, "--version");
                            if (result.ExitCode == 0 && result.Output.Contains("Python 3"))
                            {
                                // Validate Python version is in our supported range
                                string versionOutput = result.Output.Trim();
                                if (IsValidPythonVersion(versionOutput))
                                {
                                    _pythonPath = path;
                                    Debug.WriteLine($"Found Python at common location: {path} - {versionOutput}");
                                    UpdateStatus($"Found Python: {versionOutput}");
                                    return true;
                                }
                                else
                                {
                                    Debug.WriteLine($"Found Python but version not in supported range: {versionOutput}");
                                    UpdateStatus($"Found unsupported Python version: {versionOutput}. Please install Python 3.8-3.11.");
                                    // Continue checking for other Python installations that might meet our version requirements
                                }
                            }
                        }
                        catch
                        {
                            // Continue to next path
                        }
                    }
                }

                // Check Windows Registry for Python installations
                try
                {
                    var registryPaths = GetPythonPathsFromRegistry();
                    foreach (var path in registryPaths)
                    {
                        if (File.Exists(path))
                        {
                            try
                            {
                                var result = await ExecuteCommandAsync(path, "--version");
                                if (result.ExitCode == 0 && result.Output.Contains("Python 3"))
                                {
                                    // Validate Python version is in our supported range
                                    string versionOutput = result.Output.Trim();
                                    if (IsValidPythonVersion(versionOutput))
                                    {
                                        _pythonPath = path;
                                        Debug.WriteLine($"Found Python in registry: {path} - {versionOutput}");
                                        UpdateStatus($"Found Python: {versionOutput}");
                                        return true;
                                    }
                                    else
                                    {
                                        Debug.WriteLine($"Found Python but version not in supported range: {versionOutput}");
                                        UpdateStatus($"Found unsupported Python version: {versionOutput}. Please install Python 3.8-3.11.");
                                        // Continue checking for other Python installations that might meet our version requirements
                                    }
                                }
                            }
                            catch
                            {
                                // Continue to next path
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error reading registry: {ex.Message}");
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // macOS common paths
                var commonPaths = new[]
                {
                    "/usr/bin/python3",
                    "/usr/local/bin/python3",
                    "/opt/homebrew/bin/python3"
                };

                foreach (var path in commonPaths)
                {
                    if (File.Exists(path))
                    {
                        try
                        {
                            var result = await ExecuteCommandAsync(path, "--version");
                            if (result.ExitCode == 0 && result.Output.Contains("Python 3"))
                            {
                                // Validate Python version is in our supported range
                                string versionOutput = result.Output.Trim();
                                if (IsValidPythonVersion(versionOutput))
                                {
                                    _pythonPath = path;
                                    Debug.WriteLine($"Found Python at common location: {path} - {versionOutput}");
                                    UpdateStatus($"Found Python: {versionOutput}");
                                    return true;
                                }
                                else
                                {
                                    Debug.WriteLine($"Found Python but version not in supported range: {versionOutput}");
                                    UpdateStatus($"Found unsupported Python version: {versionOutput}. Please install Python 3.8-3.11.");
                                    // Continue checking for other Python installations that might meet our version requirements
                                }
                            }
                        }
                        catch
                        {
                            // Continue to next path
                        }
                    }
                }
            }

            Debug.WriteLine("No supported Python installation found.");
            UpdateStatus("No Python 3.8-3.11 installation found. Please install from python.org.");
            return false;
        }

        private bool IsValidPythonVersion(string versionString)
        {
            // Extract version number from string like "Python 3.10.2"
            Match match = Regex.Match(versionString, @"Python\s+(\d+)\.(\d+)\.(\d+)");
            if (match.Success)
            {
                int major = int.Parse(match.Groups[1].Value);
                int minor = int.Parse(match.Groups[2].Value);

                // Check if version is between 3.8 and 3.11 inclusive
                if (major == 3 && minor >= 8 && minor <= 11)
                {
                    return true;
                }
            }

            // If no match or version outside range
            return false;
        }

        private List<string> GetCommonWindowsPythonPaths()
        {
            var paths = new List<string>();
            var drives = Directory.GetLogicalDrives();

            // For each drive, check common Python installation paths
            foreach (var drive in drives)
            {
                // Check Python Launcher
                paths.Add(Path.Combine(drive, "Windows", "py.exe"));

                // Check Program Files
                for (int version = 312; version >= 36; version--)
                {
                    // Check both 64-bit and 32-bit installations
                    var majorVersion = version / 10;
                    var minorVersion = version % 10;

                    paths.Add(Path.Combine(drive, "Program Files", $"Python{majorVersion}{minorVersion}", "python.exe"));
                    paths.Add(Path.Combine(drive, "Program Files (x86)", $"Python{majorVersion}{minorVersion}", "python.exe"));

                    // Also check Python installed at root of drive (common for custom installations)
                    paths.Add(Path.Combine(drive, $"Python{majorVersion}{minorVersion}", "python.exe"));
                }

                // Check Windows Store Python installations
                var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var programsPath = Path.Combine(localAppData, "Programs");
                if (Directory.Exists(programsPath))
                {
                    try
                    {
                        foreach (var dir in Directory.GetDirectories(programsPath, "Python*"))
                        {
                            paths.Add(Path.Combine(dir, "python.exe"));
                        }
                    }
                    catch
                    {
                        // Ignore directory access errors
                    }
                }
            }

            return paths;
        }

        private List<string> GetPythonPathsFromRegistry()
        {
            var paths = new List<string>();

#if WINDOWS
            // This section requires Microsoft.Win32 references which are only available on Windows
            try
            {
                using (var baseKey = Microsoft.Win32.RegistryKey.OpenBaseKey(
                    Microsoft.Win32.RegistryHive.LocalMachine,
                    Microsoft.Win32.RegistryView.Registry64))
                {
                    // Check for Python installations registered in the Windows Registry
                    using (var pythonCoreKey = baseKey.OpenSubKey(@"SOFTWARE\Python\PythonCore"))
                    {
                        if (pythonCoreKey != null)
                        {
                            foreach (var versionName in pythonCoreKey.GetSubKeyNames())
                            {
                                using (var versionKey = pythonCoreKey.OpenSubKey(versionName))
                                {
                                    if (versionKey != null)
                                    {
                                        using (var installPathKey = versionKey.OpenSubKey("InstallPath"))
                                        {
                                            if (installPathKey != null)
                                            {
                                                var installPath = installPathKey.GetValue("")?.ToString();
                                                if (!string.IsNullOrEmpty(installPath))
                                                {
                                                    var pythonPath = Path.Combine(installPath, "python.exe");
                                                    paths.Add(pythonPath);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error reading registry: {ex.Message}");
            }
#endif

            return paths;
        }

        private async Task<(string Output, string Error, int ExitCode)> ExecuteCommandAsync(string command, string arguments)
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = processStartInfo };
            var output = new StringBuilder();
            var error = new StringBuilder();

            process.OutputDataReceived += (s, e) => { if (e.Data != null) output.AppendLine(e.Data); };
            process.ErrorDataReceived += (s, e) => { if (e.Data != null) error.AppendLine(e.Data); };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync();
            return (output.ToString(), error.ToString(), process.ExitCode);
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
    if (api_key): headers['Authorization'] = f'Bearer {api_key}'
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
