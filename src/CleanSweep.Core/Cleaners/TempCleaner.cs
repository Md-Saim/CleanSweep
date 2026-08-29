using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CleanSweep.Core.Cleaners
{
    public class TempCleaner
    {
        public List<string> Scan()
        {
            var files = new List<string>();
            files.AddRange(ScanDirectory(Path.GetTempPath()));

            if (CleanSweep.Core.System.AdminElevation.IsAdministrator())
            {
                files.AddRange(ScanDirectory(@"C:\Windows\Temp"));
            }
            return files;
        }

        public long GetEstimatedSize()
        {
            long totalSize = 0;
            var files = Scan();
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
            var files = Scan();
            long freed = 0;

            foreach (var file in files)
            {
                try
                {
                    var fi = new FileInfo(file);
                    long size = fi.Length;
                    fi.Delete();
                    freed += size;
                }
                catch { }
            }

            // Also try to delete empty temp directories
            try
            {
                CleanEmptyDirs(Path.GetTempPath());
                if (CleanSweep.Core.System.AdminElevation.IsAdministrator())
                    CleanEmptyDirs(@"C:\Windows\Temp");
            }
            catch { }

            return freed;
        }

        private void CleanEmptyDirs(string path)
        {
            if (!Directory.Exists(path)) return;
            try
            {
                foreach (var dir in Directory.EnumerateDirectories(path, "*", SearchOption.AllDirectories).Reverse())
                {
                    try
                    {
                        if (!Directory.EnumerateFileSystemEntries(dir).Any())
                            Directory.Delete(dir);
                    }
                    catch { }
                }
            }
            catch { }
        }

        private List<string> ScanDirectory(string path)
        {
            var files = new List<string>();
            if (!Directory.Exists(path)) return files;

            try
            {
                foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                {
                    files.Add(file);
                }
            }
            catch (UnauthorizedAccessException) { }
            catch (PathTooLongException) { }
            catch (Exception) { }

            return files;
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
                catch
                {
                    // Skip locked/inaccessible files
                }
            }
            return freed;
        }
    }
}
