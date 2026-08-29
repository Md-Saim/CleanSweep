using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CleanSweep.Core.Analyzer
{
    public class FileTreeNode : INotifyPropertyChanged
    {
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public long Size { get; set; }
        public bool IsDirectory { get; set; }
        public DateTime ModifiedDate { get; set; }
        public List<FileTreeNode> Children { get; set; } = new List<FileTreeNode>();

        private bool _isExpanded;
        public bool IsExpanded
        {
            get => _isExpanded;
            set { _isExpanded = value; OnPropertyChanged(); }
        }

        public string FormattedSize
        {
            get
            {
                if (Size >= 1073741824L)
                    return $"{Size / 1073741824.0:F2} GB";
                else if (Size >= 1048576L)
                    return $"{Size / 1048576.0:F2} MB";
                else if (Size >= 1024L)
                    return $"{Size / 1024.0:F1} KB";
                else
                    return $"{Size} B";
            }
        }

        public double GetPercentageOf(long parentSize)
        {
            if (parentSize <= 0) return 0;
            return (double)Size / parentSize * 100;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
