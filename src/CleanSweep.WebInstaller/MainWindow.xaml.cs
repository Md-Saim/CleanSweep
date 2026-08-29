using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Navigation;

namespace CleanSweep.WebInstaller
{
    public partial class MainWindow : Window
    {
        // Download URL — points to the latest GitHub release asset
        private const string DownloadUrl = "https://github.com/Md-Saim/CleanSweep/releases/latest/download/CleanSweep.zip";
        
        private int _currentPage = 1;
        private string _installPath = @"C:\Program Files\CleanSweep";

        public MainWindow()
        {
            InitializeComponent();
            UpdateWizardState();
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true
            });
            e.Handled = true;
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Select Installation Folder for CleanSweep",
                InitialDirectory = @"C:\Program Files"
            };

            if (dialog.ShowDialog() == true)
            {
                TxtPath.Text = Path.Combine(dialog.FolderName, "CleanSweep");
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage == 3) return; // Disallow cancel during download/install
            Application.Current.Shutdown();
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage > 1 && _currentPage != 3 && _currentPage != 4)
            {
                _currentPage--;
                UpdateWizardState();
            }
        }

        private async void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage == 1)
            {
                _currentPage = 2;
                UpdateWizardState();
            }
            else if (_currentPage == 2)
            {
                _installPath = TxtPath.Text;
                if (string.IsNullOrWhiteSpace(_installPath))
                {
                    MessageBox.Show("Please select a valid installation path.", "Invalid Path", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                _currentPage = 3;
                UpdateWizardState();
                await RunDownloadAndInstallAsync();
            }
            else if (_currentPage == 4)
            {
                // Finish
                if (ChkCreateShortcut.IsChecked == true)
                {
                    CreateDesktopShortcut(_installPath);
                }

                if (ChkLaunchApp.IsChecked == true)
                {
                    try
                    {
                        string exePath = Path.Combine(_installPath, "CleanSweep.UI.exe");
                        Process.Start(new ProcessStartInfo { FileName = exePath, WorkingDirectory = _installPath, UseShellExecute = true });
                    }
                    catch { }
                }
                Application.Current.Shutdown();
            }
        }

        private void UpdateWizardState()
        {
            Page1_Welcome.Visibility = Visibility.Collapsed;
            Page2_Location.Visibility = Visibility.Collapsed;
            Page3_Installing.Visibility = Visibility.Collapsed;
            Page4_Finished.Visibility = Visibility.Collapsed;

            BtnBack.IsEnabled = true;
            BtnNext.IsEnabled = true;
            BtnCancel.IsEnabled = true;

            switch (_currentPage)
            {
                case 1:
                    Page1_Welcome.Visibility = Visibility.Visible;
                    TxtHeaderTitle.Text = "Welcome to CleanSweep Setup";
                    TxtHeaderSubtitle.Text = "This wizard will guide you through the installation.";
                    BtnBack.IsEnabled = false;
                    BtnNext.Content = "Next ›";
                    break;
                case 2:
                    Page2_Location.Visibility = Visibility.Visible;
                    TxtHeaderTitle.Text = "Select Installation Folder";
                    TxtHeaderSubtitle.Text = "Choose where CleanSweep will be installed.";
                    BtnNext.Content = "Install";
                    break;
                case 3:
                    Page3_Installing.Visibility = Visibility.Visible;
                    TxtHeaderTitle.Text = "Downloading & Installing";
                    TxtHeaderSubtitle.Text = "Please wait while files are downloaded from GitHub.";
                    BtnBack.IsEnabled = false;
                    BtnNext.IsEnabled = false;
                    BtnCancel.IsEnabled = false;
                    break;
                case 4:
                    Page4_Finished.Visibility = Visibility.Visible;
                    TxtHeaderTitle.Text = "Setup Complete";
                    TxtHeaderSubtitle.Text = "CleanSweep has been successfully installed.";
                    BtnBack.IsEnabled = false;
                    BtnNext.Content = "Finish";
                    BtnCancel.Visibility = Visibility.Collapsed;
                    break;
            }
        }

        private async Task RunDownloadAndInstallAsync()
        {
            string tempZipPath = Path.Combine(Path.GetTempPath(), "CleanSweep_Download.zip");

            try
            {
                // Phase 1: Download from GitHub
                TxtStatus.Text = "Connecting to GitHub...";
                
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("CleanSweep-Installer/1.0");
                httpClient.Timeout = TimeSpan.FromMinutes(30);

                using var response = await httpClient.GetAsync(DownloadUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                long totalBytes = response.Content.Headers.ContentLength ?? -1;
                string totalMB = totalBytes > 0 ? $"{totalBytes / 1048576.0:F1} MB" : "Unknown";

                TxtStatus.Text = $"Downloading CleanSweep ({totalMB})...";

                using (var contentStream = await response.Content.ReadAsStreamAsync())
                using (var fileStream = new FileStream(tempZipPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920))
                {
                    long downloaded = 0;
                    var buffer = new byte[81920];
                    int bytesRead;
                    var lastUpdate = DateTime.UtcNow;

                    while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, bytesRead);
                        downloaded += bytesRead;

                        // Update UI at most every 100ms
                        if ((DateTime.UtcNow - lastUpdate).TotalMilliseconds > 100)
                        {
                            lastUpdate = DateTime.UtcNow;
                            double downloadedMB = downloaded / 1048576.0;

                            if (totalBytes > 0)
                            {
                                double percent = (double)downloaded / totalBytes * 100;
                                ProgressBar.Value = percent;
                                TxtProgressPercent.Text = $"{percent:F0}%";
                                TxtDownloadSize.Text = $"{downloadedMB:F1} MB / {totalMB}";
                                TxtStatus.Text = $"Downloading... {downloadedMB:F1} MB / {totalMB}";
                            }
                            else
                            {
                                TxtDownloadSize.Text = $"{downloadedMB:F1} MB downloaded";
                                TxtStatus.Text = $"Downloading... {downloadedMB:F1} MB";
                            }
                        }
                    }
                }

                ProgressBar.Value = 100;
                TxtProgressPercent.Text = "100%";

                // Phase 2: Extract
                TxtStatus.Text = "Extracting files...";
                ProgressBar.Value = 0;
                TxtProgressPercent.Text = "";

                await Task.Run(() =>
                {
                    if (!Directory.Exists(_installPath))
                        Directory.CreateDirectory(_installPath);

                    using var archive = ZipFile.OpenRead(tempZipPath);
                    int total = archive.Entries.Count;
                    int extracted = 0;

                    foreach (var entry in archive.Entries)
                    {
                        string destPath = Path.GetFullPath(Path.Combine(_installPath, entry.FullName));
                        if (!destPath.StartsWith(Path.GetFullPath(_installPath))) continue;

                        if (entry.FullName.EndsWith("/"))
                        {
                            Directory.CreateDirectory(destPath);
                        }
                        else
                        {
                            var dir = Path.GetDirectoryName(destPath);
                            if (dir != null) Directory.CreateDirectory(dir);
                            entry.ExtractToFile(destPath, overwrite: true);
                        }

                        extracted++;
                        if (extracted % 3 == 0 || extracted == total)
                        {
                            double pct = (double)extracted / total * 100;
                            Dispatcher.Invoke(() =>
                            {
                                ProgressBar.Value = pct;
                                TxtProgressPercent.Text = $"{pct:F0}%";
                                TxtStatus.Text = $"Extracting files... ({extracted}/{total})";
                            });
                        }
                    }
                });

                ProgressBar.Value = 100;
                TxtProgressPercent.Text = "100%";
                TxtStatus.Text = "Installation complete!";

                // Clean up temp file
                try { File.Delete(tempZipPath); } catch { }

                // Automatically advance to finish page
                _currentPage = 4;
                UpdateWizardState();
            }
            catch (HttpRequestException ex)
            {
                MessageBox.Show(
                    $"Failed to download CleanSweep:\n\n{ex.Message}\n\nPlease check your internet connection and ensure the release exists at:\n{DownloadUrl}",
                    "Download Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                _currentPage = 2;
                UpdateWizardState();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Installation failed:\n\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                _currentPage = 2;
                UpdateWizardState();
            }
            finally
            {
                try { if (File.Exists(tempZipPath)) File.Delete(tempZipPath); } catch { }
            }
        }

        private void CreateDesktopShortcut(string installPath)
        {
            try
            {
                string exePath = Path.Combine(installPath, "CleanSweep.UI.exe");
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string shortcutPath = Path.Combine(desktopPath, "CleanSweep.lnk");

                string psScript = $@"
                    $WshShell = New-Object -comObject WScript.Shell
                    $Shortcut = $WshShell.CreateShortcut('{shortcutPath}')
                    $Shortcut.TargetPath = '{exePath}'
                    $Shortcut.WorkingDirectory = '{installPath}'
                    $Shortcut.IconLocation = '{exePath}, 0'
                    $Shortcut.Save()
                ";

                Process.Start(new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{psScript}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                })?.WaitForExit();
            }
            catch { }
        }
    }
}
