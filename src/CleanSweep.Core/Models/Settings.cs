using System.Collections.Generic;

namespace CleanSweep.Core.Models
{
    public class Settings
    {
        public bool AutoCreateRestorePoint { get; set; } = true;
        public int PrefetchAgeFilterDays { get; set; } = 7;
        public bool AutoElevateAtStartup { get; set; } = false;
        public List<string> ExcludedFolders { get; set; } = new List<string>();
        public int LogRetentionDays { get; set; } = 30;
    }
}
