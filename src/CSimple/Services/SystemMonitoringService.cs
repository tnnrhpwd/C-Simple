using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Timers;
using Microsoft.Maui.Controls;

namespace CSimple.Services
{
    /// <summary>
    /// Service responsible for monitoring system resource usage (CPU, RAM, GPU)
    /// </summary>
    public class SystemMonitoringService : INotifyPropertyChanged, IDisposable
    {
        private Timer _monitoringTimer;
        private PerformanceCounter _cpuCounter;
        private PerformanceCounter _ramCounter;
        private bool _isMonitoring;

        // --- Properties ---
        private bool _isSystemMonitoringEnabled;
        public bool IsSystemMonitoringEnabled
        {
            get => _isSystemMonitoringEnabled;
            set
            {
                if (SetProperty(ref _isSystemMonitoringEnabled, value))
                {
                    if (value)
                        StartMonitoring();
                    else
                        StopMonitoring();
                }
            }
        }

        private double _cpuUsagePercent;
        public double CpuUsagePercent
        {
            get => _cpuUsagePercent;
            set => SetProperty(ref _cpuUsagePercent, value);
        }

        private double _ramUsagePercent;
        public double RamUsagePercent
        {
            get => _ramUsagePercent;
            set => SetProperty(ref _ramUsagePercent, value);
        }

        private double _gpuUsagePercent;
        public double GpuUsagePercent
        {
            get => _gpuUsagePercent;
            set => SetProperty(ref _gpuUsagePercent, value);
        }

        private long _totalRamMB;
        public long TotalRamMB
        {
            get => _totalRamMB;
            set => SetProperty(ref _totalRamMB, value);
        }

        private long _usedRamMB;
        public long UsedRamMB
        {
            get => _usedRamMB;
            set => SetProperty(ref _usedRamMB, value);
        }

        public string SystemUsageDisplay
        {
            get
            {
                if (!IsSystemMonitoringEnabled)
                    return "System monitoring disabled";

                return $"CPU: {CpuUsagePercent:F1}% | RAM: {RamUsagePercent:F1}% ({UsedRamMB:N0}/{TotalRamMB:N0} MB) | GPU: {GpuUsagePercent:F1}%";
            }
        }

        // --- Constructor ---
        public SystemMonitoringService()
        {
            InitializePerformanceCounters();
            InitializeMonitoringTimer();

            // Get total RAM at startup
            GetTotalSystemMemory();
        }

        // --- Initialization Methods ---
        /// <summary>
        /// Initialize performance counters for CPU and RAM monitoring
        /// </summary>
        private void InitializePerformanceCounters()
        {
            try
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                _ramCounter = new PerformanceCounter("Memory", "Available MBytes");

                // Initialize counters (first call often returns 0)
                _cpuCounter.NextValue();
                _ramCounter.NextValue();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to initialize performance counters: {ex.Message}");
                // Fallback: counters will be null and we'll use alternative methods
            }
        }

        /// <summary>
        /// Initialize the monitoring timer
        /// </summary>
        private void InitializeMonitoringTimer()
        {
            _monitoringTimer = new Timer(2000); // Update every 2 seconds
            _monitoringTimer.Elapsed += OnMonitoringTimerElapsed;
            _monitoringTimer.AutoReset = true;
        }

        /// <summary>
        /// Get total system memory in MB
        /// </summary>
        private void GetTotalSystemMemory()
        {
            try
            {
                // Cross-platform method using GC and performance counters
                var gcTotalMemory = GC.GetTotalMemory(false);

                // Try to get system info through performance counters (Windows)
                try
                {
                    using var totalMemoryCounter = new PerformanceCounter("Memory", "Committed Bytes");
                    var totalBytes = totalMemoryCounter.NextValue();
                    TotalRamMB = (long)(totalBytes / (1024 * 1024));
                    return;
                }
                catch
                {
                    // Fallback: estimate based on common system configurations
                    TotalRamMB = 16384; // Default to 16GB assumption
                }
            }
            catch
            {
                TotalRamMB = 8192; // Final fallback (8GB)
            }
        }

        // --- Monitoring Methods ---
        /// <summary>
        /// Start system monitoring
        /// </summary>
        public void StartMonitoring()
        {
            if (_isMonitoring) return;

            _isMonitoring = true;
            _monitoringTimer?.Start();
            Debug.WriteLine("System monitoring started");
        }

        /// <summary>
        /// Stop system monitoring
        /// </summary>
        public void StopMonitoring()
        {
            if (!_isMonitoring) return;

            _isMonitoring = false;
            _monitoringTimer?.Stop();

            // Reset values when monitoring is stopped
            CpuUsagePercent = 0;
            RamUsagePercent = 0;
            GpuUsagePercent = 0;
            UsedRamMB = 0;

            Debug.WriteLine("System monitoring stopped");
        }

        /// <summary>
        /// Timer elapsed event handler to update system usage metrics
        /// </summary>
        private void OnMonitoringTimerElapsed(object sender, ElapsedEventArgs e)
        {
            if (!IsSystemMonitoringEnabled || !_isMonitoring)
                return;

            try
            {
                UpdateCpuUsage();
                UpdateRamUsage();
                UpdateGpuUsage();

                // Update UI on main thread
                Application.Current?.Dispatcher.Dispatch(() =>
                {
                    OnPropertyChanged(nameof(SystemUsageDisplay));
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating system monitoring: {ex.Message}");
            }
        }

        /// <summary>
        /// Update CPU usage percentage
        /// </summary>
        private void UpdateCpuUsage()
        {
            try
            {
                if (_cpuCounter != null)
                {
                    CpuUsagePercent = _cpuCounter.NextValue();
                }
                else
                {
                    // Fallback: Use Process.GetCurrentProcess() for current process CPU
                    // This is less accurate but better than nothing
                    var process = Process.GetCurrentProcess();
                    CpuUsagePercent = process.TotalProcessorTime.TotalMilliseconds / Environment.TickCount * 100;
                    CpuUsagePercent = Math.Min(100, Math.Max(0, CpuUsagePercent));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating CPU usage: {ex.Message}");
                CpuUsagePercent = 0;
            }
        }

        /// <summary>
        /// Update RAM usage percentage
        /// </summary>
        private void UpdateRamUsage()
        {
            try
            {
                if (_ramCounter != null && TotalRamMB > 0)
                {
                    var availableRamMB = _ramCounter.NextValue();
                    UsedRamMB = TotalRamMB - (long)availableRamMB;
                    RamUsagePercent = ((double)UsedRamMB / TotalRamMB) * 100;
                }
                else
                {
                    // Fallback: Use GC for rough estimate
                    UsedRamMB = GC.GetTotalMemory(false) / (1024 * 1024);
                    if (TotalRamMB > 0)
                    {
                        RamUsagePercent = ((double)UsedRamMB / TotalRamMB) * 100;
                    }
                }

                // Ensure values are within reasonable bounds
                RamUsagePercent = Math.Min(100, Math.Max(0, RamUsagePercent));
                UsedRamMB = Math.Min(TotalRamMB, Math.Max(0, UsedRamMB));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating RAM usage: {ex.Message}");
                RamUsagePercent = 0;
                UsedRamMB = 0;
            }
        }

        /// <summary>
        /// Update GPU usage percentage
        /// Note: GPU monitoring is complex and platform-specific. This is a placeholder implementation.
        /// </summary>
        private void UpdateGpuUsage()
        {
            try
            {
                // For now, this is a placeholder implementation
                // Real GPU monitoring would require platform-specific APIs:
                // - NVIDIA: NVML/nvidia-ml-py
                // - AMD: ADL
                // - Intel: Intel GPU tools
                // - Cross-platform: WMI queries on Windows

                // Placeholder: simulate GPU usage based on system load
                // In a real implementation, you'd use proper GPU monitoring APIs
                GpuUsagePercent = CpuUsagePercent * 0.7; // Rough correlation placeholder
                GpuUsagePercent = Math.Min(100, Math.Max(0, GpuUsagePercent));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating GPU usage: {ex.Message}");
                GpuUsagePercent = 0;
            }
        }

        // --- Public Control Methods ---
        /// <summary>
        /// Toggle system monitoring on/off
        /// </summary>
        public void ToggleMonitoring()
        {
            IsSystemMonitoringEnabled = !IsSystemMonitoringEnabled;
        }

        /// <summary>
        /// Enable system monitoring
        /// </summary>
        public void EnableMonitoring()
        {
            IsSystemMonitoringEnabled = true;
        }

        /// <summary>
        /// Disable system monitoring
        /// </summary>
        public void DisableMonitoring()
        {
            IsSystemMonitoringEnabled = false;
        }

        // --- INotifyPropertyChanged Implementation ---
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T backingStore, T value, [CallerMemberName] string propertyName = "")
        {
            if (object.Equals(backingStore, value))
                return false;

            backingStore = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        // --- IDisposable Implementation ---
        public void Dispose()
        {
            StopMonitoring();

            _monitoringTimer?.Stop();
            _monitoringTimer?.Dispose();
            _monitoringTimer = null;

            _cpuCounter?.Dispose();
            _cpuCounter = null;

            _ramCounter?.Dispose();
            _ramCounter = null;
        }
    }
}
