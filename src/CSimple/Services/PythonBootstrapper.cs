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
{
    /// <summary>
    /// Simplified Python bootstrapper that focuses on system Python or API-only mode
    /// </summary>
    public class PythonBootstrapper
    {
        private readonly string _appDataPath;
        private readonly string _scriptsPath;
        private string _pythonPath = null;

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
        public string PythonExecutablePath => _pythonPath;

        /// <summary>
        /// Initializes by finding an existing Python installation
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
                    UpdateProgress(1.0);
                    return true;
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
        }

        /// <summary>
        /// Installs required packages for HuggingFace models
        /// </summary>
        public async Task<bool> InstallRequiredPackagesAsync()
        {
            if (string.IsNullOrEmpty(_pythonPath))
            {
                UpdateStatus("Python not found. Cannot install packages.");
                return false;
            }

            try
            {
                UpdateStatus("Installing required packages...");
                UpdateProgress(0.2);

                // Create a process to run pip
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = _pythonPath,
                    Arguments = "-m pip install transformers torch --upgrade",
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
        }

        /// <summary>
        /// Executes a Python script
        /// </summary>
        public async Task<(string Output, string Error, int ExitCode)> ExecuteScriptAsync(
            string scriptPath, string arguments, int timeoutMs = 120000)
        {
            if (string.IsNullOrEmpty(_pythonPath))
            {
                return ("", "Python not found. Please install Python and restart the application.", -1);
            }

            try
            {
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = _pythonPath,
                    Arguments = $"\"{scriptPath}\" {arguments}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                Debug.WriteLine($"Executing: {processStartInfo.FileName} {processStartInfo.Arguments}");

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
