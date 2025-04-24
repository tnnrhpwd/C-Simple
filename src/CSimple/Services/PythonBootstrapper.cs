using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace CSimple.Services
{
    /// <summary>
    /// Handles Python bootstrapping for applications without requiring user installation.
    /// Provides a self-contained Python environment for running scripts.
    /// </summary>
    public class PythonBootstrapper
    {
        // Constants
        private const string PYTHON_VERSION = "3.9.13"; // Python version we'll use
        private const string MINICONDA_VERSION = "py39_4.12.0"; // Miniconda version (more reliable than embedded Python)
        private const string PACKAGES_BASIC = "transformers torch"; // Basic packages required

        // Instance properties
        private readonly string _appDataPath;
        private readonly string _pythonRootPath;
        private readonly string _condaPath;
        private readonly string _condaExePath;
        private readonly string _packagesPath;
        private readonly HttpClient _httpClient;

        // Events for status updates
        public event EventHandler<string> StatusChanged;
        public event EventHandler<double> ProgressChanged;

        public PythonBootstrapper()
        {
            _httpClient = new HttpClient();
            _appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CSimple");
            _pythonRootPath = Path.Combine(_appDataPath, "python-env");
            _condaPath = Path.Combine(_pythonRootPath, "miniconda3");
            _packagesPath = Path.Combine(_appDataPath, "python-packages");

            // Set the conda executable path based on platform
            _condaExePath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? Path.Combine(_condaPath, "Scripts", "conda.exe")
                : Path.Combine(_condaPath, "bin", "conda");
        }

        /// <summary>
        /// Gets the Python executable path
        /// </summary>
        public string PythonExecutablePath
        {
            get
            {
                // First priority: Bundled Conda Python
                if (File.Exists(GetCondaPythonPath()))
                    return GetCondaPythonPath();

                // Second priority: System Python if available
                var systemPython = FindSystemPython();
                if (!string.IsNullOrEmpty(systemPython))
                    return systemPython;

                // Default fallback - will trigger installation if not found
                return GetCondaPythonPath();
            }
        }

        /// <summary>
        /// Initializes Python environment
        /// </summary>
        public async Task<bool> InitializeAsync()
        {
            try
            {
                // Ensure directories exist
                Directory.CreateDirectory(_pythonRootPath);
                Directory.CreateDirectory(_packagesPath);

                // First check if we already have Conda installed and usable
                if (IsCondaInstalled() && await TestCondaPythonAsync())
                {
                    UpdateStatus("Using existing Python installation");
                    return true;
                }

                // Next try to find system Python
                string systemPython = FindSystemPython();
                if (!string.IsNullOrEmpty(systemPython) && await TestPythonInstallationAsync(systemPython))
                {
                    UpdateStatus($"Using system Python: {systemPython}");
                    return true;
                }

                // Finally, if no usable Python, install Miniconda
                return await InstallMinicondaAsync();
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error initializing Python: {ex.Message}");
                Debug.WriteLine($"Python bootstrap error: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Installs required packages for the application
        /// </summary>
        public async Task<bool> InstallRequiredPackagesAsync()
        {
            try
            {
                UpdateStatus("Installing required packages...");

                // Determine the pip command to use
                string pipCmd = GetPipPath();

                // Create a pip.ini file to speed up downloads and improve reliability
                await CreatePipConfigAsync();

                // Install packages
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = pipCmd,
                        Arguments = $"install {PACKAGES_BASIC} --no-cache-dir",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        EnvironmentVariables = { ["PIP_CONFIG_FILE"] = Path.Combine(_appDataPath, "pip.ini") }
                    }
                };

                var output = new StringBuilder();
                var error = new StringBuilder();

                process.OutputDataReceived += (s, e) =>
                {
                    if (e.Data != null)
                    {
                        output.AppendLine(e.Data);
                        Debug.WriteLine($"Pip: {e.Data}");
                    }
                };

                process.ErrorDataReceived += (s, e) =>
                {
                    if (e.Data != null)
                    {
                        error.AppendLine(e.Data);
                        Debug.WriteLine($"Pip Error: {e.Data}");
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                // Use a timeout for package installation
                bool exited = await Task.Run(() => process.WaitForExit(300000)); // 5-minute timeout

                if (!exited)
                {
                    process.Kill();
                    UpdateStatus("Package installation timed out");
                    return false;
                }

                if (process.ExitCode != 0)
                {
                    UpdateStatus($"Package installation failed: {error}");
                    return false;
                }

                UpdateStatus("Required packages installed successfully");
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
            return Path.Combine(_packagesPath, scriptName);
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
                    string destPath = Path.Combine(_packagesPath, fileName);
                    File.Copy(scriptFile, destPath, overwrite: true);
                    Debug.WriteLine($"Copied script {fileName} to {destPath}");
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error copying scripts: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Executes a Python script with the bootstrapped environment
        /// </summary>
        public async Task<(string Output, string Error, int ExitCode)> ExecuteScriptAsync(
            string scriptPath, string arguments, int timeoutMs = 120000)
        {
            string pythonPath = PythonExecutablePath;
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

            // Add PYTHONPATH to include our scripts directory
            process.StartInfo.EnvironmentVariables["PYTHONPATH"] = _packagesPath;

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
                process.Kill();
                return ("", "Script execution timed out", -1);
            }

            return (outputBuilder.ToString(), errorBuilder.ToString(), process.ExitCode);
        }

        #region Private Helper Methods
        private bool IsCondaInstalled()
        {
            return File.Exists(_condaExePath) &&
                   File.Exists(GetCondaPythonPath());
        }

        private string GetCondaPythonPath()
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? Path.Combine(_condaPath, "python.exe")
                : Path.Combine(_condaPath, "bin", "python");
        }

        private string GetPipPath()
        {
            string pythonPath = PythonExecutablePath;
            return pythonPath; // Use python -m pip instead of direct pip calls
        }

        private string FindSystemPython()
        {
            try
            {
                // Try common Python locations
                List<string> pythonPaths = new List<string>();

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    // Windows Python paths
                    pythonPaths.AddRange(new[]
                    {
                        @"C:\Python39\python.exe",
                        @"C:\Program Files\Python39\python.exe",
                        @"C:\Program Files (x86)\Python39\python.exe",
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Python", "Python39", "python.exe"),
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Python39", "python.exe")
                    });
                }
                else
                {
                    // Unix-like Python paths
                    pythonPaths.AddRange(new[]
                    {
                        "/usr/bin/python3",
                        "/usr/local/bin/python3",
                        "/opt/homebrew/bin/python3"
                    });
                }

                // Try to find python in PATH
                try
                {
                    string pythonCmd = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "where python" : "which python3";
                    using (var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "cmd.exe" : "bash",
                            Arguments = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? $"/c {pythonCmd}" : $"-c \"{pythonCmd}\"",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            CreateNoWindow = true
                        }
                    })
                    {
                        process.Start();
                        string output = process.StandardOutput.ReadToEnd();
                        process.WaitForExit();

                        if (process.ExitCode == 0)
                        {
                            // Get the first line which should be the path to Python
                            string systemPath = output.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                            if (!string.IsNullOrEmpty(systemPath) && File.Exists(systemPath))
                            {
                                pythonPaths.Insert(0, systemPath); // Prioritize PATH python
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error looking for Python in PATH: {ex.Message}");
                }

                // Try each path
                foreach (string path in pythonPaths)
                {
                    if (File.Exists(path))
                    {
                        try
                        {
                            // Verify it's Python 3.x
                            using var process = Process.Start(new ProcessStartInfo
                            {
                                FileName = path,
                                Arguments = "-V",
                                UseShellExecute = false,
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                                CreateNoWindow = true
                            });

                            string output = process.StandardOutput.ReadToEnd();
                            string error = process.StandardError.ReadToEnd();
                            process.WaitForExit();

                            string version = string.IsNullOrEmpty(output) ? error : output;

                            if (version.Contains("Python 3") && !version.Contains("Python 2"))
                            {
                                Debug.WriteLine($"Found Python: {path} - Version: {version.Trim()}");
                                return path;
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error testing Python at {path}: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error finding system Python: {ex.Message}");
            }

            return null;
        }

        private async Task<bool> TestPythonInstallationAsync(string pythonPath)
        {
            try
            {
                // Try to import transformers to see if packages are already installed
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = pythonPath,
                        Arguments = "-c \"import transformers; print('Transformers installed')\"",
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
                    Debug.WriteLine("Python installation with transformers verified");
                    return true;
                }

                // Check if we can at least import pip
                using var pipProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = pythonPath,
                        Arguments = "-m pip --version",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                pipProcess.Start();
                await pipProcess.WaitForExitAsync();

                return pipProcess.ExitCode == 0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error testing Python installation: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> TestCondaPythonAsync()
        {
            return await TestPythonInstallationAsync(GetCondaPythonPath());
        }

        private async Task<bool> InstallMinicondaAsync()
        {
            string installerPath = Path.Combine(_appDataPath, "miniconda_installer.exe"); // Define here for cleanup
            try
            {
                UpdateStatus("Setting up Python environment...");

                // --- Enhanced Directory Cleaning ---
                if (Directory.Exists(_condaPath))
                {
                    UpdateStatus($"Attempting to remove existing directory: {_condaPath}");
                    bool deleted = false;
                    for (int i = 0; i < 5; i++) // Increased retries
                    {
                        try
                        {
                            Directory.Delete(_condaPath, true);
                            // Short delay to allow file system to catch up
                            await Task.Delay(200);
                            // Verify deletion
                            if (!Directory.Exists(_condaPath))
                            {
                                deleted = true;
                                UpdateStatus("Existing directory removed successfully.");
                                break; // Exit loop if successful
                            }
                            else
                            {
                                Debug.WriteLine($"Warning: Directory still exists after delete attempt {i + 1}.");
                            }
                        }
                        catch (Exception ex) when (i < 4) // Log intermediate errors
                        {
                            Debug.WriteLine($"Warning: Failed to delete existing conda directory (attempt {i + 1}): {ex.Message}. Retrying in 1 second...");
                            await Task.Delay(1000); // Wait longer before retrying
                        }
                        catch (Exception ex) // Log final error
                        {
                            UpdateStatus($"Error: Failed to delete existing conda directory after multiple attempts: {ex.Message}. Installation likely to fail.");
                            Debug.WriteLine($"Error: Failed to delete existing conda directory: {ex.Message}");
                            // Optionally, try renaming the folder as a last resort
                            try
                            {
                                string backupPath = _condaPath + "_backup_" + DateTime.Now.ToString("yyyyMMddHHmmss");
                                Directory.Move(_condaPath, backupPath);
                                UpdateStatus($"Renamed problematic directory to {backupPath}.");
                                deleted = true; // Treat rename as success for proceeding
                            }
                            catch (Exception moveEx)
                            {
                                UpdateStatus($"Error: Failed to rename problematic directory: {moveEx.Message}.");
                                Debug.WriteLine($"Error: Failed to rename problematic directory: {moveEx.Message}");
                                return false; // Abort if deletion and rename both fail
                            }
                        }
                    }
                    if (!deleted)
                    {
                        UpdateStatus("Error: Could not clear existing Miniconda directory. Aborting installation.");
                        return false; // Abort if deletion failed
                    }
                }
                else
                {
                    UpdateStatus("No existing Miniconda directory found.");
                }

                // Ensure parent directory exists before download/install
                Directory.CreateDirectory(_pythonRootPath);
                // --- End Enhanced Directory Cleaning ---


                // Download appropriate Miniconda installer
                string installerUrl = GetMinicondaInstallerUrl();
                // installerPath defined at the start of the method

                UpdateStatus("Downloading Python environment...");
                await DownloadFileWithProgressAsync(installerUrl, installerPath);

                // Silent install to specified directory
                UpdateStatus("Installing Python environment (this may take a few minutes)...");

                // Adjust arguments: /InstallationType=JustMe might help with permissions
                // /RegisterPython=0 prevents modifying system registry/PATH
                // /AddToPath=0 explicitly prevents adding to PATH
                string installerArgs = $"/S /InstallationType=JustMe /RegisterPython=0 /AddToPath=0 /D={_condaPath}";
                Debug.WriteLine($"Running Miniconda installer: {installerPath} {installerArgs}");

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = installerPath,
                        Arguments = installerArgs,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true, // Capture output
                        RedirectStandardError = true   // Capture error
                    }
                };

                var outputLog = new StringBuilder();
                var errorLog = new StringBuilder();

                // Use TaskCompletionSource for better async handling of process exit
                var tcs = new TaskCompletionSource<bool>();

                process.EnableRaisingEvents = true;
                process.Exited += (sender, args) =>
                {
                    // Log output/error immediately after exit
                    string stdOut = outputLog.ToString();
                    string stdErr = errorLog.ToString();
                    if (!string.IsNullOrWhiteSpace(stdOut))
                        Debug.WriteLine($"Miniconda Installer Output:\n{stdOut}");
                    if (!string.IsNullOrWhiteSpace(stdErr))
                        Debug.WriteLine($"Miniconda Installer Error:\n{stdErr}");

                    if (process.ExitCode != 0)
                    {
                        UpdateStatus($"Miniconda installation failed with exit code {process.ExitCode}. Check debug output for details.");
                        Debug.WriteLine($"Miniconda installation failed. Exit Code: {process.ExitCode}");
                        if (!string.IsNullOrWhiteSpace(stdErr))
                        {
                            UpdateStatus($"Installer Error Snippet: {stdErr.Trim().Split('\n').LastOrDefault()}"); // Show last line of error
                        }
                        tcs.TrySetResult(false); // Signal failure
                    }
                    else
                    {
                        tcs.TrySetResult(true); // Signal success
                    }
                    process.Dispose(); // Dispose the process object
                };

                process.OutputDataReceived += (s, e) => { if (e.Data != null) outputLog.AppendLine(e.Data); };
                process.ErrorDataReceived += (s, e) => { if (e.Data != null) errorLog.AppendLine(e.Data); };

                try
                {
                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                }
                catch (Exception startEx)
                {
                    UpdateStatus($"Failed to start Miniconda installer: {startEx.Message}");
                    Debug.WriteLine($"Failed to start Miniconda installer: {startEx.Message}");
                    return false;
                }


                // Wait for the process to exit or timeout
                var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(300000)); // 5-minute timeout

                if (completedTask != tcs.Task || !await tcs.Task) // Check if timed out or failed
                {
                    if (completedTask != tcs.Task) // Timeout case
                    {
                        UpdateStatus("Miniconda installation timed out.");
                        Debug.WriteLine("Miniconda installation timed out.");
                        try { if (!process.HasExited) process.Kill(); } catch { } // Try to kill if timed out
                    }
                    // Failure case already handled by Exited event handler
                    return false;
                }

                // --- Verification moved after successful exit ---
                // Verify installation only if process exited successfully (ExitCode 0)
                if (!File.Exists(GetCondaPythonPath()))
                {
                    UpdateStatus("Miniconda installation verification failed - Python executable not found after successful exit code.");
                    Debug.WriteLine("Miniconda installation verification failed: Python executable not found.");
                    return false;
                }

                UpdateStatus("Python environment installed successfully");
                return true;
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error installing Miniconda: {ex.Message}");
                Debug.WriteLine($"Miniconda installation error: {ex}");
                return false;
            }
            finally
            {
                // Ensure installer is cleaned up even if errors occur
                try
                {
                    if (File.Exists(installerPath))
                    {
                        File.Delete(installerPath);
                        Debug.WriteLine("Miniconda installer file deleted.");
                    }
                }
                catch (Exception cleanupEx)
                {
                    Debug.WriteLine($"Warning: Failed to delete Miniconda installer: {cleanupEx.Message}");
                }
            }
        }

        private string GetMinicondaInstallerUrl()
        {
            string arch = Environment.Is64BitOperatingSystem ? "x86_64" : "x86";

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return $"https://repo.anaconda.com/miniconda/Miniconda3-{MINICONDA_VERSION}-Windows-{arch}.exe";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // For M1/M2 Macs
                if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
                {
                    return $"https://repo.anaconda.com/miniconda/Miniconda3-{MINICONDA_VERSION}-MacOSX-arm64.sh";
                }
                return $"https://repo.anaconda.com/miniconda/Miniconda3-{MINICONDA_VERSION}-MacOSX-{arch}.sh";
            }
            else // Linux
            {
                return $"https://repo.anaconda.com/miniconda/Miniconda3-{MINICONDA_VERSION}-Linux-{arch}.sh";
            }
        }

        private async Task DownloadFileWithProgressAsync(string url, string destinationPath)
        {
            try
            {
                using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                var contentLength = response.Content.Headers.ContentLength ?? -1L;
                using var contentStream = await response.Content.ReadAsStreamAsync();
                using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                var buffer = new byte[8192];
                var totalBytesRead = 0L;
                var bytesRead = 0;

                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead);

                    totalBytesRead += bytesRead;

                    if (contentLength > 0)
                    {
                        var progressPercentage = (double)totalBytesRead / contentLength;
                        UpdateProgress(progressPercentage);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error downloading file: {ex.Message}");
                throw;
            }
        }

        private async Task CreatePipConfigAsync()
        {
            // Create pip.ini/pip.conf to speed up downloads and improve reliability
            string pipConfigPath = Path.Combine(_appDataPath, "pip.ini");

            string configContent = @"[global]
timeout = 60
retries = 3
use-feature = 2020-resolver
no-cache-dir = false

[install]
progress-bar = on
prefer-binary = true";

            await File.WriteAllTextAsync(pipConfigPath, configContent);
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
