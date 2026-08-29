using System;
using System.IO;
using System.Text.Json;
using CleanSweep.Core.Models;

namespace CleanSweep.Core.System
{
    public static class SettingsManager
    {
        private static readonly string AppDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CleanSweep");
        private static readonly string SettingsFile = Path.Combine(AppDataFolder, "settings.json");

        public static Settings Load()
        {
            if (!File.Exists(SettingsFile))
            {
                return new Settings();
            }

            try
            {
                var json = File.ReadAllText(SettingsFile);
                return JsonSerializer.Deserialize<Settings>(json) ?? new Settings();
            }
            catch
            {
                return new Settings();
            }
        }

        public static void Save(Settings settings)
        {
            try
            {
                if (!Directory.Exists(AppDataFolder))
                {
                    Directory.CreateDirectory(AppDataFolder);
                }

                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsFile, json);
            }
            catch { }
        }
    }
}
