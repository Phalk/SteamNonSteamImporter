using Playnite.SDK;
using System;
using System.Collections.ObjectModel;

namespace SteamNonSteamImporter
{
    public sealed class PluginLogEntry
    {
        public DateTime Timestamp { get; }
        public string Level { get; }
        public string Message { get; }

        public string DisplayText => $"{Timestamp:HH:mm:ss}  {Level,-5}  {Message}";

        public PluginLogEntry(DateTime timestamp, string level, string message)
        {
            Timestamp = timestamp;
            Level = level;
            Message = message;
        }
    }

    internal sealed class PluginLog
    {
        private const int MaximumEntries = 300;
        private static readonly ILogger PlayniteLogger = LogManager.GetLogger();

        private readonly IPlayniteAPI playniteApi;
        private readonly Func<SteamNonSteamImporterSettings> settingsProvider;
        private readonly ObservableCollection<PluginLogEntry> entries;

        public PluginLog(
            IPlayniteAPI playniteApi,
            Func<SteamNonSteamImporterSettings> settingsProvider,
            ObservableCollection<PluginLogEntry> entries)
        {
            this.playniteApi = playniteApi;
            this.settingsProvider = settingsProvider;
            this.entries = entries;
        }

        public void Info(string message)
        {
            Write("INFO", message, null, LogSeverity.Info);
        }

        public void Warning(string message)
        {
            Write("WARN", message, null, LogSeverity.Warning);
        }

        public void Error(string message, Exception exception = null)
        {
            Write("ERROR", message, exception, LogSeverity.Error);
        }

        public void Debug(string message)
        {
            var settings = settingsProvider();
            if (settings == null || !settings.EnableDetailedLogging)
            {
                return;
            }

            Write("DEBUG", message, null, LogSeverity.Debug);
        }

        private void Write(string level, string message, Exception exception, LogSeverity severity)
        {
            var settings = settingsProvider();
            var panelMessage = message ?? string.Empty;

            if (exception != null && settings != null && settings.EnableDetailedLogging)
            {
                panelMessage += $" ({exception.GetType().Name}: {exception.Message})";
            }

            AddToPanel(new PluginLogEntry(DateTime.Now, level, panelMessage));

            if (settings == null || !settings.WriteToPlayniteLog)
            {
                return;
            }

            var playniteMessage = $"[SteamNonSteamImporter] {message}";
            switch (severity)
            {
                case LogSeverity.Debug:
                    PlayniteLogger.Debug(playniteMessage);
                    break;
                case LogSeverity.Warning:
                    PlayniteLogger.Warn(playniteMessage);
                    break;
                case LogSeverity.Error:
                    if (exception == null)
                    {
                        PlayniteLogger.Error(playniteMessage);
                    }
                    else
                    {
                        PlayniteLogger.Error(exception, playniteMessage);
                    }
                    break;
                default:
                    PlayniteLogger.Info(playniteMessage);
                    break;
            }
        }

        private void AddToPanel(PluginLogEntry entry)
        {
            Action addEntry = () =>
            {
                entries.Add(entry);
                while (entries.Count > MaximumEntries)
                {
                    entries.RemoveAt(0);
                }
            };

            var dispatcher = playniteApi?.MainView?.UIDispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.BeginInvoke(addEntry);
            }
            else
            {
                addEntry();
            }
        }

        private enum LogSeverity
        {
            Debug,
            Info,
            Warning,
            Error
        }
    }
}
