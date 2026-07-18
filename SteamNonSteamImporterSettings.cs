using Playnite.SDK;
using Playnite.SDK.Data;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace SteamNonSteamImporter
{
    public sealed class SteamNonSteamImporterSettings : ObservableObject
    {
        private string steamPath = string.Empty;
        private string userDataPath = string.Empty;
        private bool enableDetailedLogging = true;
        private bool writeToPlayniteLog;
        private int settingsVersion;

        public string SteamPath
        {
            get => steamPath;
            set => SetValue(ref steamPath, value);
        }

        public string UserDataPath
        {
            get => userDataPath;
            set => SetValue(ref userDataPath, value);
        }

        public bool EnableDetailedLogging
        {
            get => enableDetailedLogging;
            set => SetValue(ref enableDetailedLogging, value);
        }

        public bool WriteToPlayniteLog
        {
            get => writeToPlayniteLog;
            set => SetValue(ref writeToPlayniteLog, value);
        }

        public int SettingsVersion
        {
            get => settingsVersion;
            set => SetValue(ref settingsVersion, value);
        }
    }

    public sealed class SteamNonSteamImporterSettingsViewModel : ObservableObject, ISettings
    {
        private readonly SteamNonSteamImporter plugin;
        private SteamNonSteamImporterSettings editingClone;
        private SteamNonSteamImporterSettings settings;

        public SteamNonSteamImporterSettings Settings
        {
            get => settings;
            set => SetValue(ref settings, value);
        }

        [DontSerialize]
        public ObservableCollection<PluginLogEntry> LogEntries { get; } =
            new ObservableCollection<PluginLogEntry>();

        public SteamNonSteamImporterSettingsViewModel(SteamNonSteamImporter plugin)
        {
            this.plugin = plugin;
            Settings = plugin.LoadPluginSettings<SteamNonSteamImporterSettings>()
                       ?? new SteamNonSteamImporterSettings();

            // Version 1 changes detailed panel logging from opt-in to enabled by default.
            if (Settings.SettingsVersion < 1)
            {
                Settings.EnableDetailedLogging = true;
                Settings.SettingsVersion = 1;
                plugin.SavePluginSettings(Settings);
            }
        }

        public void BeginEdit()
        {
            editingClone = Serialization.GetClone(Settings);
        }

        public void CancelEdit()
        {
            if (editingClone != null)
            {
                Settings = editingClone;
            }
        }

        public void EndEdit()
        {
            Settings.SteamPath = (Settings.SteamPath ?? string.Empty).Trim();
            Settings.UserDataPath = (Settings.UserDataPath ?? string.Empty).Trim();
            plugin.SavePluginSettings(Settings);
        }

        public bool VerifySettings(out List<string> errors)
        {
            errors = new List<string>();
            return true;
        }

        public void ClearLogs()
        {
            LogEntries.Clear();
        }

        public string GetLogText()
        {
            return string.Join(System.Environment.NewLine, LogEntries.Select(a => a.DisplayText));
        }
    }
}
