using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace CleanSweep.UI
{
    public partial class App : Application
    {
        private static readonly string CrashLogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            "CleanSweep_CrashLog.txt");

        protected override void OnStartup(StartupEventArgs e)
        {
            // Catch all unhandled WPF dispatcher exceptions
            DispatcherUnhandledException += App_DispatcherUnhandledException;

            // Catch non-UI thread exceptions
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            // Catch Task exceptions
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

            base.OnStartup(e);
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            LogCrash("DispatcherUnhandledException", e.Exception);
            MessageBox.Show(
                $"CleanSweep encountered an error:\n\n{e.Exception.Message}\n\nDetails saved to Desktop\\CleanSweep_CrashLog.txt",
                "CleanSweep - Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            e.Handled = true;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = e.ExceptionObject as Exception;
            LogCrash("AppDomain.UnhandledException", ex);
            MessageBox.Show(
                $"CleanSweep encountered a fatal error:\n\n{ex?.Message}\n\nDetails saved to Desktop\\CleanSweep_CrashLog.txt",
                "CleanSweep - Fatal Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            LogCrash("TaskScheduler.UnobservedTaskException", e.Exception);
            e.SetObserved();
        }

        private static void LogCrash(string source, Exception? ex)
        {
            try
            {
                var message = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Source: {source}\n{ex}\n\n";
                File.AppendAllText(CrashLogPath, message);
            }
            catch { }
        }
    }
}
