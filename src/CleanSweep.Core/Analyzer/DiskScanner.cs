using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CleanSweep.Core.Analyzer
{
    public class DiskScanner
    {
        private const int MaxDepth = 5; // Prevent extremely deep recursion
        private int _progressThrottle;

        private readonly HashSet<string> _excludedDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            @"$Recycle.Bin",
            @"System Volume Information",
            @"Windows\WinSxS",
            @"ProgramData",
            @"Recovery",
            @"$WinREAgent"
        };

        public async Task<FileTreeNode> ScanAsync(string rootPath, Action<string> progressCallback, CancellationToken cancellationToken)
        {
            var rootNode = new FileTreeNode
            {
                Name = rootPath,
                FullPath = rootPath,
                IsDirectory = true
            };

            _progressThrottle = 0;

            await Task.Run(() => ScanDirectory(rootNode, progressCallback, cancellationToken, 0), cancellationToken);

            return rootNode;
        }

        private void ScanDirectory(FileTreeNode node, Action<string> progressCallback, CancellationToken token, int depth)
        {
            if (token.IsCancellationRequested) return;
            if (depth > MaxDepth) return; // Stop scanning beyond max depth

            try
            {
                // Skip reparse points / junction points to avoid infinite loops
                var dirInfo = new DirectoryInfo(node.FullPath);
                if (dirInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
                    return;

                // Throttle progress callbacks to avoid flooding UI (every 50 items)
                if (Interlocked.Increment(ref _progressThrottle) % 50 == 0)
                {
                    progressCallback?.Invoke(node.FullPath);
                }

                // Files
                try
                {
                    foreach (var file in Directory.EnumerateFiles(node.FullPath))
                    {
                        if (token.IsCancellationRequested) break;

                        try
                        {
                            var fileInfo = new FileInfo(file);
                            
                            // Skip reparse point files
                            if (fileInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
                                continue;

                            var fileNode = new FileTreeNode
                            {
                                Name = fileInfo.Name,
                                FullPath = fileInfo.FullName,
                                Size = fileInfo.Length,
                                IsDirectory = false,
                                ModifiedDate = fileInfo.LastWriteTime
                            };
                            node.Children.Add(fileNode);
                            node.Size += fileNode.Size;
                        }
                        catch (UnauthorizedAccessException) { }
                        catch (IOException) { }
                        catch (Exception) { }
                    }
                }
                catch (UnauthorizedAccessException) { }
                catch (DirectoryNotFoundException) { }
                catch (IOException) { }
                catch (Exception) { }

                // Directories
                try
                {
                    foreach (var dir in Directory.EnumerateDirectories(node.FullPath))
                    {
                        if (token.IsCancellationRequested) break;

                        try
                        {
                            var subDirInfo = new DirectoryInfo(dir);

                            // Skip reparse points / junctions
                            if (subDirInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
                                continue;

                            // Skip excluded system folders
                            if (_excludedDirs.Any(ex => subDirInfo.FullName.Contains(ex, StringComparison.OrdinalIgnoreCase)))
                                continue;

                            var dirNode = new FileTreeNode
                            {
                                Name = subDirInfo.Name,
                                FullPath = subDirInfo.FullName,
                                IsDirectory = true,
                                ModifiedDate = subDirInfo.LastWriteTime
                            };

                            ScanDirectory(dirNode, progressCallback, token, depth + 1);

                            if (dirNode.Size > 0 || dirNode.Children.Count > 0)
                            {
                                node.Children.Add(dirNode);
                                node.Size += dirNode.Size;
                            }
                        }
                        catch (UnauthorizedAccessException) { }
                        catch (IOException) { }
                        catch (Exception) { }
                    }
                }
                catch (UnauthorizedAccessException) { }
                catch (DirectoryNotFoundException) { }
                catch (IOException) { }
                catch (Exception) { }

                // Sort children by size descending
                node.Children = node.Children.OrderByDescending(c => c.Size).ToList();
            }
            catch (UnauthorizedAccessException) { }
            catch (PathTooLongException) { }
            catch (DirectoryNotFoundException) { }
            catch (IOException) { }
            catch (Exception) { }
        }
    }
}
