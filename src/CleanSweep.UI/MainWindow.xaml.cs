using System;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media.Animation;
using CleanSweep.UI.ViewModels;

namespace CleanSweep.UI
{
    public partial class MainWindow : Window
    {
        private bool _settingsOpen;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Drive_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is DriveDisplayModel drive)
            {
                drive.ToggleAnalyzerCommand.Execute(null);
            }
        }

        // Minimize to Tray instead of closing
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            this.Hide();
            base.OnClosing(e);
        }

        // Tray Icon Double Click -> Show Window
        private void TrayIcon_TrayMouseDoubleClick(object sender, RoutedEventArgs e)
        {
            this.Show();
            this.WindowState = WindowState.Normal;
            this.Activate();
        }

        // Context Menu: Show
        private void MenuShow_Click(object sender, RoutedEventArgs e)
        {
            this.Show();
            this.WindowState = WindowState.Normal;
            this.Activate();
        }

        // Context Menu: Clean Files (Silent Background)
        private void MenuClean_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                if (vm.ScanAndCleanCommand.CanExecute(null))
                {
                    vm.ScanAndCleanCommand.Execute(null);
                }
            }
        }

        // Context Menu: Quit
        private void MenuQuit_Click(object sender, RoutedEventArgs e)
        {
            TrayIcon.Dispose();
            if (DataContext is MainViewModel vm)
                vm.Dispose();
            
            Application.Current.Shutdown();
        }

        // ═══════════════════════════════════════════
        //  SETTINGS PANEL
        // ═══════════════════════════════════════════

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            OpenSettingsPanel();
        }

        private void BtnCloseSettings_Click(object sender, RoutedEventArgs e)
        {
            CloseSettingsPanel();
        }

        private void SettingsBackdrop_Click(object sender, MouseButtonEventArgs e)
        {
            CloseSettingsPanel();
        }

        private void OpenSettingsPanel()
        {
            if (_settingsOpen) return;
            _settingsOpen = true;

            SettingsBackdrop.Visibility = Visibility.Visible;
            SettingsPanel.Visibility = Visibility.Visible;

            // Animate backdrop fade in
            var backdropFade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(250))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            SettingsBackdrop.BeginAnimation(OpacityProperty, backdropFade);

            // Animate panel slide in from right
            var slideIn = new DoubleAnimation(400, 0, TimeSpan.FromMilliseconds(300))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            SettingsPanelTranslate.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, slideIn);
        }

        private void CloseSettingsPanel()
        {
            if (!_settingsOpen) return;
            _settingsOpen = false;

            // Animate backdrop fade out
            var backdropFade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            backdropFade.Completed += (s, e) => SettingsBackdrop.Visibility = Visibility.Collapsed;
            SettingsBackdrop.BeginAnimation(OpacityProperty, backdropFade);

            // Animate panel slide out to right
            var slideOut = new DoubleAnimation(0, 400, TimeSpan.FromMilliseconds(250))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            slideOut.Completed += (s, e) => SettingsPanel.Visibility = Visibility.Collapsed;
            SettingsPanelTranslate.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, slideOut);
        }

        // GitHub link click
        private void GitHub_Click(object sender, MouseButtonEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://github.com/Md-Saim",
                    UseShellExecute = true
                });
            }
            catch { }
        }
    }

    // Local namespace alias for XAML: xmlns:local
    // Converter: percentage to pixel width for drive usage bars
    public class PercentToWidthConverter : IMultiValueConverter
    {
        public static readonly PercentToWidthConverter Instance = new();

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length == 2 &&
                values[0] is double percent &&
                values[1] is double totalWidth)
            {
                return Math.Max(0, totalWidth * percent / 100.0);
            }
            return 0.0;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    // Inverse bool converter for button enabled state
    public class InverseBoolConverter : IValueConverter
    {
        public static readonly InverseBoolConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b) return !b;
            return true;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b) return !b;
            return false;
        }
    }
}
