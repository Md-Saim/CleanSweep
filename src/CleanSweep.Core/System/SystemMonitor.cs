using System;
using System.Diagnostics;
using System.Management;
using System.Threading.Tasks;

namespace CleanSweep.Core.System
{
    public class SystemInfo
    {
        public float CpuUsage { get; set; }
        public float CpuTemperature { get; set; }
        public bool HasCpuTemp { get; set; }
        public double RamUsedGB { get; set; }
        public double RamTotalGB { get; set; }
        public double RamFreeGB { get; set; }
        public float RamPercent { get; set; }
        public float GpuUsage { get; set; }
        public float GpuTemperature { get; set; }
        public bool HasGpuInfo { get; set; }
    }

    public class SystemMonitor : IDisposable
    {
        private PerformanceCounter? _cpuCounter;
        private bool _disposed;

        public SystemMonitor()
        {
            try
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                _cpuCounter.NextValue(); // First call always returns 0
            }
            catch
            {
                _cpuCounter = null;
            }
        }

        public async Task<SystemInfo> GetSystemInfoAsync()
        {
            return await Task.Run(() =>
            {
                var info = new SystemInfo();

                // CPU Usage
                try
                {
                    if (_cpuCounter != null)
                    {
                        info.CpuUsage = _cpuCounter.NextValue();
                    }
                }
                catch { }

                // CPU Temperature via WMI
                try
                {
                    using var searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT * FROM MSAcpi_ThermalZoneTemperature");
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        var temp = Convert.ToDouble(obj["CurrentTemperature"]);
                        info.CpuTemperature = (float)((temp - 2732) / 10.0); // Convert from tenths of Kelvin
                        info.HasCpuTemp = true;
                        break;
                    }
                }
                catch
                {
                    info.HasCpuTemp = false;
                }

                // RAM
                try
                {
                    using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_OperatingSystem");
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        double totalKB = Convert.ToDouble(obj["TotalVisibleMemorySize"]);
                        double freeKB = Convert.ToDouble(obj["FreePhysicalMemory"]);
                        info.RamTotalGB = totalKB / 1048576.0;
                        info.RamFreeGB = freeKB / 1048576.0;
                        info.RamUsedGB = info.RamTotalGB - info.RamFreeGB;
                        info.RamPercent = (float)(info.RamUsedGB / info.RamTotalGB * 100);
                        break;
                    }
                }
                catch { }

                // GPU Usage via nvidia-smi (NVIDIA) or WMI
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "nvidia-smi",
                        Arguments = "--query-gpu=utilization.gpu,temperature.gpu --format=csv,noheader,nounits",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    };
                    using var process = Process.Start(psi);
                    if (process != null)
                    {
                        var output = process.StandardOutput.ReadToEnd();
                        process.WaitForExit(2000);
                        var parts = output.Trim().Split(',');
                        if (parts.Length >= 2)
                        {
                            if (float.TryParse(parts[0].Trim(), out float gpuUse))
                                info.GpuUsage = gpuUse;
                            if (float.TryParse(parts[1].Trim(), out float gpuTemp))
                                info.GpuTemperature = gpuTemp;
                            info.HasGpuInfo = true;
                        }
                    }
                }
                catch
                {
                    info.HasGpuInfo = false;
                }

                return info;
            });
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _cpuCounter?.Dispose();
                _disposed = true;
            }
        }
    }
}
