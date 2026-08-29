using System;
using System.Collections.Generic;
using System.IO;

namespace CleanSweep.Core.Cleaners
{
    public class PrefetchCleaner
    {
        private const string PrefetchDir = @"C:\Windows\Prefetch";

        public List<string> Scan(int ageInDays = 7)
        {
            var files = new List<string>();
            if (!CleanSweep.Core.System.AdminElevation.IsAdministrator()) return files;
            if (!Directory.Exists(PrefetchDir)) return files;

            try
            {
                var cutoffDate = DateTime.Now.AddDays(-ageInDays);
                foreach (var file in Directory.EnumerateFiles(PrefetchDir, "*.pf"))
                {
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.LastWriteTime < cutoffDate)
                    {
                        files.Add(file);
                    }
                }
            }
            catch { }

            return files;
        }

        public List<string> ScanAll()
        {
            var files = new List<string>();
            if (!CleanSweep.Core.System.AdminElevation.IsAdministrator()) return files;
            if (!Directory.Exists(PrefetchDir)) return files;

            try
            {
                foreach (var file in Directory.EnumerateFiles(PrefetchDir, "*.pf"))
                {
                    files.Add(file);
                }
            }
            catch { }

            return files;
        }

        public long GetEstimatedSize()
        {
            long totalSize = 0;
            var files = ScanAll();
            foreach (var file in files)
            {
                try
                {
                    totalSize += new FileInfo(file).Length;
                }
                catch { }
            }
            return totalSize;
        }

        public long ForceClean()
        {
            var files = ScanAll();
            return Clean(files);
        }

        public long Clean(IEnumerable<string> files)
        {
            long freed = 0;
            foreach (var file in files)
            {
                try
                {
                    var fileInfo = new FileInfo(file);
                    long size = fileInfo.Length;
                    fileInfo.Delete();
                    freed += size;
                }
                catch { }
            }
            return freed;
        }
    }
}
