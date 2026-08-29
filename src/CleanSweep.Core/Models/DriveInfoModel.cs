using System;
using System.Collections.Generic;
using System.IO;

namespace CleanSweep.Core.Models
{
    public class DriveInfoModel
    {
        public string DriveLetter { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public double UsedGB { get; set; }
        public double FreeGB { get; set; }
        public double TotalGB { get; set; }
        public double UsagePercent { get; set; }
        public bool IsExpanded { get; set; }

        public static List<DriveInfoModel> GetAllDrives()
        {
            var drives = new List<DriveInfoModel>();
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (!drive.IsReady) continue;
                try
                {
                    var totalGB = drive.TotalSize / (1024.0 * 1024.0 * 1024.0);
                    var freeGB = drive.TotalFreeSpace / (1024.0 * 1024.0 * 1024.0);
                    var usedGB = totalGB - freeGB;
                    var usagePercent = totalGB > 0 ? (usedGB / totalGB * 100) : 0;

                    drives.Add(new DriveInfoModel
                    {
                        DriveLetter = drive.Name.TrimEnd('\\'),
                        Label = string.IsNullOrWhiteSpace(drive.VolumeLabel) ? "Local Disk" : drive.VolumeLabel,
                        UsedGB = Math.Round(usedGB, 2),
                        FreeGB = Math.Round(freeGB, 2),
                        TotalGB = Math.Round(totalGB, 2),
                        UsagePercent = Math.Round(usagePercent, 0)
                    });
                }
                catch { }
            }
            return drives;
        }
    }
}
