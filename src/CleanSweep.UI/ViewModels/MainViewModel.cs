using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CleanSweep.Core.Cleaners;
using CleanSweep.Core.Models;
using CleanSweep.Core.System;
using CleanSweep.Core.Analyzer;
using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace CleanSweep.UI.ViewModels
{
    public partial class MainViewModel : ObservableObject, IDisposable
    {
        private readonly SystemMonitor _monitor;
        private readonly DispatcherTimer _timer;
        private CancellationTokenSource? _scanAllCts;

        // Cached scan results per drive letter (e.g. "C:" -> FileTreeNode)
        private readonly ConcurrentDictionary<string, FileTreeNode> _driveCache = new();

        // === System Monitor ===
        [ObservableProperty] private float _cpuUsage;
        [ObservableProperty] private string _cpuTemp = "-- °C";
        [ObservableProperty] private string _ramUsedText = "0.00 GB";
        [ObservableProperty] private string _ramTotalText = "0.00 GB";
        [ObservableProperty] private string _ramFreeText = "0.00 GB";
        [ObservableProperty] private float _ramPercent;
        [ObservableProperty] private string _ramPercentText = "0%";
        [ObservableProperty] private float _gpuUsage;
        [ObservableProperty] private string _gpuTemp = "-- °C";
        [ObservableProperty] private bool _hasGpuInfo;

        // === Drives ===
        [ObservableProperty] private ObservableCollection<DriveDisplayModel> _drives = new();

        // === Disk Scan Status ===
        [ObservableProperty] private string _diskScanStatus = "Scanning drives...";
        [ObservableProperty] private bool _isDiskScanComplete;

        // === Cleaner ===
        [ObservableProperty] private bool _cleanTemp = true;
        [ObservableProperty] private bool _cleanPrefetch = true;
        [ObservableProperty] private bool _cleanRecycleBin = true;
        [ObservableProperty] private string _tempSize = "Calculating...";
        [ObservableProperty] private string _prefetchSize = "Calculating...";
        [ObservableProperty] private string _recycleBinSize = "Calculating...";
        [ObservableProperty] private string _totalEstimated = "Calculating...";
        [ObservableProperty] private bool _isCleaning;
        [ObservableProperty] private string _cleanStatus = "Select items to clean and reclaim disk space.";
        [ObservableProperty] private double _cleanProgress;

        private long _tempBytes, _prefetchBytes, _recycleBinBytes;

        public MainViewModel()
        {
            _monitor = new SystemMonitor();
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _timer.Tick += async (s, e) => await RefreshSystemInfoAsync();
            _timer.Start();

            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            await RefreshSystemInfoAsync();
            RefreshDrives();
            await ScanEstimatesAsync();

            // Scan ALL drives silently in background on first launch
            await ScanAllDrivesAsync();
        }

        // ═══════════════════════════════════════════
        //  SYSTEM MONITOR
        // ═══════════════════════════════════════════

        private async Task RefreshSystemInfoAsync()
        {
            try
            {
                var info = await _monitor.GetSystemInfoAsync();
                CpuUsage = info.CpuUsage;
                CpuTemp = info.HasCpuTemp ? $"{info.CpuTemperature:F0} °C" : "-- °C";
                RamUsedText = $"{info.RamUsedGB:F2} GB";
                RamTotalText = $"{info.RamTotalGB:F2} GB";
                RamFreeText = $"{info.RamFreeGB:F2} GB";
                RamPercent = info.RamPercent;
                RamPercentText = $"{info.RamPercent:F0}%";
                GpuUsage = info.GpuUsage;
                GpuTemp = info.HasGpuInfo ? $"{info.GpuTemperature:F0} °C" : "-- °C";
                HasGpuInfo = info.HasGpuInfo;
            }
            catch { }
        }

        // ═══════════════════════════════════════════
        //  DRIVES
        // ═══════════════════════════════════════════

        private void RefreshDrives()
        {
            Drives.Clear();
            var driveInfos = DriveInfoModel.GetAllDrives();
            foreach (var d in driveInfos)
            {
                Drives.Add(new DriveDisplayModel(d, this));
            }
        }

        // ═══════════════════════════════════════════
        //  DISK SCAN — SCAN ALL DRIVES ONCE AT STARTUP
        // ═══════════════════════════════════════════

        private async Task ScanAllDrivesAsync()
        {
            _scanAllCts = new CancellationTokenSource();
            IsDiskScanComplete = false;
            var scanner = new DiskScanner();
            int scanned = 0;
            int total = Drives.Count;

            foreach (var drive in Drives.ToList())
            {
                if (_scanAllCts.IsCancellationRequested) break;

                var driveLetter = drive.Info.DriveLetter;
                DiskScanStatus = $"Scanning {driveLetter} ({scanned + 1}/{total})...";
                drive.AnalyzerStatus = "Scanning...";

                try
                {
                    var root = await scanner.ScanAsync(driveLetter + "\\", path =>
                    {
                        // Throttle UI updates — only update periodically 
                        System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
                        {
                            drive.AnalyzerStatus = $"Scanning: {TruncatePath(path, 60)}";
                        }, DispatcherPriority.Background);
                    }, _scanAllCts.Token);

                    // Cache the result
                    _driveCache[driveLetter] = root;

                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        drive.AnalyzerStatus = $"Ready — {root.Children.Count} items, {root.FormattedSize}";
                        drive.HasCachedData = true;
                    });
                }
                catch (OperationCanceledException)
                {
                    drive.AnalyzerStatus = "Scan cancelled.";
                }
                catch (Exception ex)
                {
                    drive.AnalyzerStatus = $"Error: {ex.Message}";
                }

                scanned++;
            }

            IsDiskScanComplete = true;
            DiskScanStatus = "All drives scanned ✓";
        }

        /// <summary>
        /// Called when user clicks a drive row — just toggles expand/collapse using cached data.
        /// No re-scanning. Instant.
        /// </summary>
        public void ToggleDriveExpand(DriveDisplayModel drive)
        {
            drive.IsExpanded = !drive.IsExpanded;

            if (drive.IsExpanded)
            {
                // Collapse other drives
                foreach (var d in Drives)
                {
                    if (d != drive)
                    {
                        d.IsExpanded = false;
                        d.AnalyzerNodes.Clear();
                    }
                }

                // Load cached data into the observable collection
                if (_driveCache.TryGetValue(drive.Info.DriveLetter, out var cachedRoot))
                {
                    drive.AnalyzerNodes.Clear();
                    foreach (var child in cachedRoot.Children)
                    {
                        drive.AnalyzerNodes.Add(child);
                    }
                    drive.AnalyzerStatus = $"{cachedRoot.Children.Count} items — {cachedRoot.FormattedSize} total";
                }
                else
                {
                    drive.AnalyzerStatus = "Still scanning... please wait.";
                }
            }
            else
            {
                drive.AnalyzerNodes.Clear();
            }
        }

        // ═══════════════════════════════════════════
        //  CLEANER
        // ═══════════════════════════════════════════

        private async Task ScanEstimatesAsync()
        {
            await Task.Run(() =>
            {
                var tempCleaner = new TempCleaner();
                _tempBytes = tempCleaner.GetEstimatedSize();
                TempSize = FormatBytes(_tempBytes);

                var prefetchCleaner = new PrefetchCleaner();
                _prefetchBytes = prefetchCleaner.GetEstimatedSize();
                PrefetchSize = FormatBytes(_prefetchBytes);

                var recycleBinCleaner = new RecycleBinCleaner();
                _recycleBinBytes = recycleBinCleaner.GetEstimatedSize();
                RecycleBinSize = FormatBytes(_recycleBinBytes);

                UpdateTotalEstimate();
            });
        }

        private void UpdateTotalEstimate()
        {
            long total = 0;
            if (CleanTemp) total += _tempBytes;
            if (CleanPrefetch) total += _prefetchBytes;
            if (CleanRecycleBin) total += _recycleBinBytes;
            TotalEstimated = FormatBytes(total);
        }

        partial void OnCleanTempChanged(bool value) => UpdateTotalEstimate();
        partial void OnCleanPrefetchChanged(bool value) => UpdateTotalEstimate();
        partial void OnCleanRecycleBinChanged(bool value) => UpdateTotalEstimate();

        [RelayCommand]
        private async Task ScanAndCleanAsync()
        {
            if (IsCleaning) return;
            IsCleaning = true;
            CleanProgress = 0;
            long totalFreed = 0;

            try
            {
                if (CleanTemp)
                {
                    CleanStatus = "🧹 Cleaning temporary files...";
                    CleanProgress = 10;
                    await Task.Run(() =>
                    {
                        var cleaner = new TempCleaner();
                        totalFreed += cleaner.ForceClean();
                    });
                    CleanProgress = 33;
                }

                if (CleanPrefetch)
                {
                    CleanStatus = "🧹 Cleaning prefetch cache...";
                    CleanProgress = 40;
                    await Task.Run(() =>
                    {
                        var cleaner = new PrefetchCleaner();
                        totalFreed += cleaner.ForceClean();
                    });
                    CleanProgress = 66;
                }

                if (CleanRecycleBin)
                {
                    CleanStatus = "🗑️ Emptying Recycle Bin...";
                    CleanProgress = 75;
                    await Task.Run(() =>
                    {
                        var cleaner = new RecycleBinCleaner();
                        cleaner.Clean(noConfirmation: true);
                    });
                    CleanProgress = 90;
                }

                CleanProgress = 100;
                CleanStatus = $"✅ Done! Freed {FormatBytes(totalFreed)}";

                await ScanEstimatesAsync();
                RefreshDrives();
            }
            catch (Exception ex)
            {
                CleanStatus = $"❌ Error: {ex.Message}";
            }
            finally
            {
                IsCleaning = false;
            }
        }

        // ═══════════════════════════════════════════
        //  HELPERS
        // ═══════════════════════════════════════════

        public static string FormatBytes(long bytes)
        {
            if (bytes >= 1073741824L)
                return $"{bytes / 1073741824.0:F2} GB";
            else if (bytes >= 1048576L)
                return $"{bytes / 1048576.0:F2} MB";
            else if (bytes >= 1024L)
                return $"{bytes / 1024.0:F1} KB";
            else
                return $"{bytes} B";
        }

        private static string TruncatePath(string path, int maxLen)
        {
            if (path.Length <= maxLen) return path;
            return "..." + path.Substring(path.Length - maxLen + 3);
        }

        public void Dispose()
        {
            _timer.Stop();
            _monitor.Dispose();
            _scanAllCts?.Cancel();
        }
    }

    // ═══════════════════════════════════════════
    //  DRIVE DISPLAY MODEL
    // ═══════════════════════════════════════════

    public partial class DriveDisplayModel : ObservableObject
    {
        private readonly MainViewModel _parent;

        public DriveInfoModel Info { get; }

        [ObservableProperty] private bool _isExpanded;
        [ObservableProperty] private bool _hasCachedData;
        [ObservableProperty] private ObservableCollection<FileTreeNode> _analyzerNodes = new();
        [ObservableProperty] private string _analyzerStatus = "Waiting for scan...";

        public string DisplayName => $"{Info.DriveLetter}";
        public string Label => Info.Label;
        public string UsedText => $"{Info.UsedGB:F2} GB";
        public string FreeText => $"{Info.FreeGB:F2} GB";
        public string TotalText => $"{Info.TotalGB:F2} GB";
        public double UsagePercent => Info.UsagePercent;
        public string UsagePercentText => $"{Info.UsagePercent:F0}%";

        public DriveDisplayModel(DriveInfoModel info, MainViewModel parent)
        {
            Info = info;
            _parent = parent;
        }

        [RelayCommand]
        private void ToggleAnalyzer()
        {
            _parent.ToggleDriveExpand(this);
        }
    }
}
