using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.IO.Compression;
using System.Collections.Specialized; // Add this using

namespace CSimple.Services
{
    public class PythonDependencyManager
    {
        private readonly HttpClient _httpClient;
        private readonly string _appDataPath;
        private readonly string _pythonPath;
        private readonly string _virtualEnvPath;
        private readonly string _scriptsPath;
        private readonly string _packageRequirementsPath;
        private string _systemPythonPath; // Add this field

        // Event for progress updates
        public event EventHandler<string> StatusUpdated;
        public event EventHandler<double> ProgressUpdated;

        public PythonDependencyManager()
        {
            _httpClient = new HttpClient();

            // Get the app data directory for CSimple
            _appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CSimple"
            );

            // Paths for Python, virtual environment, and scripts
            _pythonPath = Path.Combine(_appDataPath, "python");
            _virtualEnvPath = Path.Combine(_appDataPath, "venv");
            _scriptsPath = Path.Combine(_appDataPath, "scripts");
            _packageRequirementsPath = Path.Combine(_scriptsPath, "requirements.txt");

            // Create directories if they don't exist
            Directory.CreateDirectory(_appDataPath);
            Directory.CreateDirectory(_scriptsPath);
        }

        public async Task<bool> EnsurePythonSetupAsync()
        {
            try
            {
                UpdateStatus("Checking Python installation...");

                // First check if Python is already on the PATH
                if (await IsPythonInstalledOnPathAsync())
                {
                    UpdateStatus("Python found on system PATH");
                    // We'll still set up our virtual environment
                    string systemPythonPath = await GetSystemPythonPathAsync();
                    return await SetupVirtualEnvAsync(systemPythonPath);
                }

                // Next check if we have embedded Python
                if (IsPythonInstalledInAppData())
                {
                    UpdateStatus("Using app-bundled Python installation");
                    return await SetupVirtualEnvAsync(GetEmbeddedPythonExePath());
                }

                // If no Python found, download and install it
                UpdateStatus("Python not found, downloading embedded Python...");
                bool downloadSuccess = await DownloadAndExtractPythonAsync();
                if (!downloadSuccess)
                {
                    UpdateStatus("Failed to download Python. Please install Python 3.9+ manually.");
                    return false;
                }

                // Set up virtual environment with the downloaded Python
                return await SetupVirtualEnvAsync(GetEmbeddedPythonExePath());
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error ensuring Python setup: {ex.Message}");
                Debug.WriteLine($"Python setup error: {ex}");
                return false;
            }
        }

        private async Task<bool> IsPythonInstalledOnPathAsync()
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "python",
                        Arguments = "--version",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                await process.WaitForExitAsync();

                string output = await process.StandardOutput.ReadToEndAsync();
                return process.ExitCode == 0 && output.Contains("Python 3");
            }
            catch
            {
                return false;
            }
        }

        private async Task<string> GetSystemPythonPathAsync()
        {
            // Get the path to the system Python executable
            string pythonCommand = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "where python" : "which python";
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "cmd.exe" : "bash",
                    Arguments = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? $"/c {pythonCommand}" : $"-c \"{pythonCommand}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };

            process.Start();
            string output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            // Return the first line of the output
            return output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)[0];
        }

        private bool IsPythonInstalledInAppData()
        {
            return File.Exists(GetEmbeddedPythonExePath());
        }

        private string GetEmbeddedPythonExePath()
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? Path.Combine(_pythonPath, "python.exe")
                : Path.Combine(_pythonPath, "bin", "python3");
        }

        private string GetVirtualEnvPythonPath()
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? Path.Combine(_virtualEnvPath, "Scripts", "python.exe")
                : Path.Combine(_virtualEnvPath, "bin", "python");
        }

        private string GetPipPath()
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? Path.Combine(_virtualEnvPath, "Scripts", "pip.exe")
                : Path.Combine(_virtualEnvPath, "bin", "pip");
        }

        private async Task<bool> DownloadAndExtractPythonAsync()
        {
            try
            {
                string downloadUrl = GetPythonDownloadUrl();
                string downloadPath = Path.Combine(_appDataPath, "python_embed.zip");

                UpdateStatus("Downloading Python, please wait...");

                // Download the Python embedded package
                using (var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();

                    var contentLength = response.Content.Headers.ContentLength ?? -1L;
                    using var stream = await response.Content.ReadAsStreamAsync();
                    using var fileStream = new FileStream(downloadPath, FileMode.Create, FileAccess.Write, FileShare.None);

                    var buffer = new byte[8192];
                    var totalBytesRead = 0L;
                    var bytesRead = 0;

                    while ((bytesRead = await stream.ReadAsync(buffer)) > 0)
                    {
                        await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                        totalBytesRead += bytesRead;

                        if (contentLength > 0)
                        {
                            var progress = (double)totalBytesRead / contentLength;
                            ProgressUpdated?.Invoke(this, progress);
                        }
                    }
                }

                UpdateStatus("Extracting Python package...");
                // Extract the zip file
                if (Directory.Exists(_pythonPath))
                    Directory.Delete(_pythonPath, true);

                ZipFile.ExtractToDirectory(downloadPath, _pythonPath);
                File.Delete(downloadPath);

                // Fix the python3x._pth file to allow importing modules
                string pthFileName = Directory.GetFiles(_pythonPath, "python3*._pth").FirstOrDefault();
                if (!string.IsNullOrEmpty(pthFileName))
                {
                    string content = File.ReadAllText(pthFileName);
                    // Uncomment the import site line by removing the # if it exists
                    if (content.Contains("#import site"))
                    {
                        content = content.Replace("#import site", "import site");
                        File.WriteAllText(pthFileName, content);
                        UpdateStatus("Modified Python path configuration to allow package imports");
                    }
                }
                else
                {
                    UpdateStatus("Warning: Could not find Python path configuration file");
                }

                // Create a version file to track the installed Python version
                File.WriteAllText(Path.Combine(_pythonPath, "version.txt"), "embedded-3.9.13");

                return true;
            }
            catch (Exception ex)
            {
                UpdateStatus($"Failed to download Python: {ex.Message}");
                Debug.WriteLine($"Python download error: {ex}");
                return false;
            }
        }

        private string GetPythonDownloadUrl()
        {
            // Select the appropriate Python download URL based on the OS and architecture
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return Environment.Is64BitOperatingSystem
                    ? "https://www.python.org/ftp/python/3.9.13/python-3.9.13-embed-amd64.zip"
                    : "https://www.python.org/ftp/python/3.9.13/python-3.9.13-embed-win32.zip";
            }
            else
            {
                throw new PlatformNotSupportedException(
                    "Automatic Python installation is currently only supported on Windows. " +
                    "Please install Python 3.9+ manually on your system.");
            }
        }

        private async Task<bool> SetupVirtualEnvAsync(string pythonPath)
        {
            try
            {
                // Skip if virtual environment already exists and is valid
                if (IsVirtualEnvValid())
                {
                    UpdateStatus("Using existing virtual environment");
                    return await InstallRequiredPackagesAsync();
                }

                UpdateStatus("Setting up Python virtual environment...");

                if (Directory.Exists(_virtualEnvPath))
                    Directory.Delete(_virtualEnvPath, true);

                // For embedded Python, we need to try several approaches
                bool pipInstalled = false;

                if (pythonPath.Contains(_pythonPath))
                {
                    try
                    {
                        // First try direct approach
                        await InstallPipForEmbeddedPythonAsync(pythonPath);
                        pipInstalled = true;
                    }
                    catch (Exception ex)
                    {
                        UpdateStatus($"Could not install pip for embedded Python: {ex.Message}");
                        UpdateStatus("Attempting alternative setup...");

                        // Try to use the built-in ensurepip module for newer Python versions
                        try
                        {
                            await RunProcessAsync(pythonPath, "-m ensurepip --default-pip");
                            pipInstalled = true;
                        }
                        catch
                        {
                            UpdateStatus("Built-in pip installer failed, trying system Python...");

                            // Try system Python as a last resort
                            if (await FindSystemPythonAsync())
                            {
                                pythonPath = Path.Combine(_pythonPath,
                                    RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "python.exe" : "python");

                                if (File.Exists(pythonPath))
                                {
                                    UpdateStatus($"Using system Python: {pythonPath}");
                                    pipInstalled = true;
                                }
                            }
                        }
                    }

                    if (!pipInstalled)
                    {
                        UpdateStatus("Failed to set up pip. Please install Python manually.");
                        return false;
                    }
                }

                // Install virtualenv module if not using embedded Python
                if (!pythonPath.Contains(_pythonPath))
                {
                    await RunProcessAsync(pythonPath, "-m pip install --user virtualenv");
                }

                // Create the virtual environment
                try
                {
                    await RunProcessAsync(pythonPath, $"-m venv \"{_virtualEnvPath}\"");
                }
                catch (Exception ex)
                {
                    UpdateStatus($"Failed to create virtual environment: {ex.Message}");

                    // Try direct module approach if venv fails
                    try
                    {
                        await RunProcessAsync(pythonPath, $"-m virtualenv \"{_virtualEnvPath}\"");
                    }
                    catch
                    {
                        UpdateStatus("Failed to create virtual environment with both venv and virtualenv");
                        return false;
                    }
                }

                // Verify the environment was created successfully
                if (!File.Exists(GetVirtualEnvPythonPath()))
                {
                    UpdateStatus("Failed to create virtual environment");
                    return false;
                }

                return await InstallRequiredPackagesAsync();
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error setting up virtual environment: {ex.Message}");
                Debug.WriteLine($"Virtual env setup error: {ex}");
                return false;
            }
        }

        private async Task InstallPipForEmbeddedPythonAsync(string pythonPath)
        {
            // For embedded Python, download and install pip
            UpdateStatus("Installing pip for embedded Python...");

            try
            {
                // Download get-pip.py
                string getPipUrl = "https://bootstrap.pypa.io/get-pip.py";
                string getPipPath = Path.Combine(_appDataPath, "get-pip.py");

                await DownloadFileAsync(getPipUrl, getPipPath);

                // Copy required DLLs if they aren't already there
                // This is a common issue - embedded Python often misses Python3.dll and other core DLLs
                string pythonDir = Path.GetDirectoryName(pythonPath);
                string python3dll = Path.Combine(pythonDir, "python3.dll");
                string python39dll = Path.Combine(pythonDir, "python39.dll");

                // Download missing DLLs if needed
                if (!File.Exists(python3dll))
                {
                    UpdateStatus("Downloading missing Python core DLLs...");
                    // URL to a repo with Python DLLs or use a CDN service to host them
                    string dllUrl = "https://github.com/indygreg/python-build-standalone/releases/download/20230116/cpython-3.9.16+20230116-i686-pc-windows-msvc-shared-install_only.tar.gz";
                    string dllsArchivePath = Path.Combine(_appDataPath, "python_dlls.tar.gz");

                    // This is a placeholder - in a real implementation, you would download the DLL files
                    // and extract them to the Python directory
                    UpdateStatus("Note: Manual download of Python DLLs may be required.");
                    UpdateStatus("Using alternative approach - full Python installation");

                    // Instead, let's try a different approach - use a system-installed Python or install one
                    if (await FindSystemPythonAsync())
                    {
                        return;
                    }

                    throw new Exception("Could not find or install a suitable Python environment");
                }

                // Run get-pip.py
                await RunProcessAsync(pythonPath, $"\"{getPipPath}\"");

                // Clean up
                if (File.Exists(getPipPath))
                    File.Delete(getPipPath);
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error installing pip: {ex.Message}");
                // Try alternative approach
                await SetupFullPythonInstallationAsync();
                throw; // Re-throw to let calling code know installation failed
            }
        }

        private async Task<bool> FindSystemPythonAsync()
        {
            try
            {
                // Try to locate a system installation of Python
                UpdateStatus("Looking for Python installation on the system...");

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "python",
                        Arguments = "--version",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                try
                {
                    process.Start();
                    string output = await process.StandardOutput.ReadToEndAsync();
                    await process.WaitForExitAsync();

                    if (process.ExitCode == 0 && output.Contains("Python 3"))
                    {
                        UpdateStatus($"Found system Python: {output.Trim()}");

                        // Get Python path
                        process.StartInfo.Arguments = "-c \"import sys; print(sys.executable)\"";
                        process.Start();
                        string pythonPath = await process.StandardOutput.ReadToEndAsync();
                        await process.WaitForExitAsync();

                        if (!string.IsNullOrWhiteSpace(pythonPath))
                        {
                            // Instead of directly assigning to _pythonPath (which might be readonly)
                            // Store the path information in a local field or property that we can use
                            var systemPythonDir = Path.GetDirectoryName(pythonPath.Trim());
                            UpdateStatus($"Using system Python at: {systemPythonDir}");

                            // Store system Python path in a property or method instead
                            SetSystemPythonPath(systemPythonDir);
                            return true;
                        }
                    }
                }
                catch
                {
                    // System Python not found or not accessible
                }

                return false;
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error finding system Python: {ex.Message}");
                return false;
            }
        }

        // Add this method to set the Python path properly
        private void SetSystemPythonPath(string path)
        {
            // Use a local field or property to store the system Python path
            // This avoids direct assignment to a readonly field
            _systemPythonPath = path;
        }

        private async Task SetupFullPythonInstallationAsync()
        {
            try
            {
                UpdateStatus("Setting up alternative Python installation...");

                // Check if we can use Python from PATH
                if (await FindSystemPythonAsync())
                {
                    return;
                }

                // If not, explain to the user that Python must be installed
                UpdateStatus("Python installation is required but could not be automated.");
                UpdateStatus("Please install Python 3.9+ from python.org and try again.");

                // Could enhance this with a Python installer download and execution
                // but that would require more complex installer logic
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error setting up Python: {ex.Message}");
            }
        }

        private bool IsVirtualEnvValid()
        {
            // Check if virtual environment python and pip exist
            return File.Exists(GetVirtualEnvPythonPath()) && File.Exists(GetPipPath());
        }

        private async Task<bool> InstallRequiredPackagesAsync()
        {
            try
            {
                UpdateStatus("Installing required packages...");

                // Create or update requirements.txt
                CreateRequirementsFile();

                // Install requirements
                string pipPath = GetPipPath();
                await RunProcessAsync(
                    pipPath,
                    $"install -r \"{_packageRequirementsPath}\" --disable-pip-version-check"
                );

                UpdateStatus("Python environment setup complete");
                return true;
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error installing packages: {ex.Message}");
                Debug.WriteLine($"Package installation error: {ex}");
                return false;
            }
        }

        private void CreateRequirementsFile()
        {
            // Create a requirements.txt file with the required packages
            string requirements = @"
transformers==4.31.0
torch==2.0.1
accelerate==0.21.0
# Adding GPU support for torch if needed, but using CPU by default
# torch==2.0.1+cu118 -f https://download.pytorch.org/whl/torch_stable.html
";
            File.WriteAllText(_packageRequirementsPath, requirements.Trim());
        }

        public async Task<bool> CopyScriptsToAppDataAsync(string sourceScriptsDir)
        {
            try
            {
                // Ensure scripts directory exists
                Directory.CreateDirectory(_scriptsPath);

                foreach (var file in Directory.GetFiles(sourceScriptsDir, "*.py"))
                {
                    string destPath = Path.Combine(_scriptsPath, Path.GetFileName(file));
                    File.Copy(file, destPath, true);
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error copying scripts: {ex}");
                return false;
            }
        }

        public string GetScriptPath(string scriptName)
        {
            return Path.Combine(_scriptsPath, scriptName);
        }

        public string GetPythonExecutablePath()
        {
            return File.Exists(GetVirtualEnvPythonPath())
                ? GetVirtualEnvPythonPath()
                : (IsPythonInstalledInAppData() ? GetEmbeddedPythonExePath() : "python");
        }

        private async Task DownloadFileAsync(string url, string destinationPath)
        {
            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            using var streamToReadFrom = await response.Content.ReadAsStreamAsync();
            using var streamToWriteTo = File.Create(destinationPath);
            await streamToReadFrom.CopyToAsync(streamToWriteTo);
        }

        private async Task RunProcessAsync(string fileName, string arguments)
        {
            var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                // Ensure UTF8 encoding for output
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                StandardErrorEncoding = System.Text.Encoding.UTF8
            };

            // --- Add PATH, PYTHONHOME, and WorkingDirectory modification for embedded Python ---
            string embeddedPythonExe = GetEmbeddedPythonExePath();
            // Use full paths for reliable comparison
            if (Path.GetFullPath(fileName).Equals(Path.GetFullPath(embeddedPythonExe), StringComparison.OrdinalIgnoreCase))
            {
                string pythonDir = Path.GetDirectoryName(embeddedPythonExe); // e.g., C:\Users\...\CSimple\python
                string scriptsDir = Path.Combine(pythonDir, "Scripts");

                // 1. Set Working Directory
                process.StartInfo.WorkingDirectory = pythonDir;
                Debug.WriteLine($"[RunProcessAsync] Set WorkingDirectory: {pythonDir}");

                // 2. Modify PATH
                string currentPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
                // Ensure Python dir and Scripts dir are at the beginning
                string newPath = $"{pythonDir};{scriptsDir};{currentPath}";

                // 3. Set PYTHONHOME
                string pythonHome = pythonDir; // The root directory of the embedded distribution

                // Use StringDictionary for environment variables
                if (process.StartInfo.EnvironmentVariables is StringDictionary envVars)
                {
                    envVars["PATH"] = newPath;
                    envVars["PYTHONHOME"] = pythonHome;
                }
                else // Fallback
                {
                    process.StartInfo.EnvironmentVariables["PATH"] = newPath;
                    process.StartInfo.EnvironmentVariables["PYTHONHOME"] = pythonHome;
                }
                Debug.WriteLine($"[RunProcessAsync] Modified PATH for embedded Python: {newPath}");
                Debug.WriteLine($"[RunProcessAsync] Set PYTHONHOME for embedded Python: {pythonHome}");
            }
            // --- End modification ---

            // Set up output and error handlers to debug
            process.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    Debug.WriteLine($"[Process Output] {e.Data}");
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    Debug.WriteLine($"[Process Error] {e.Data}");
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                // Capture error output for better diagnostics
                // Note: Error output is already being captured by the event handler and printed to Debug.
                // Consider collecting it into a variable if needed for the exception message.
                throw new Exception($"Process '{fileName} {arguments}' exited with code {process.ExitCode}. Check Debug Output for errors.");
            }
        }

        private void UpdateStatus(string status)
        {
            Debug.WriteLine($"[PythonDependencyManager] {status}");
            StatusUpdated?.Invoke(this, status);
        }
    }
}
