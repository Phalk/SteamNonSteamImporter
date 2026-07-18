using Microsoft.Win32;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Controls;

namespace SteamNonSteamImporter
{
    public sealed class SteamNonSteamImporter : LibraryPlugin
    {
        private const byte VdfObject = 0x00;
        private const byte VdfString = 0x01;
        private const byte VdfInt32 = 0x02;
        private const byte VdfFloat32 = 0x03;
        private const byte VdfPointer = 0x04;
        private const byte VdfWideString = 0x05;
        private const byte VdfColor = 0x06;
        private const byte VdfUInt64 = 0x07;
        private const byte VdfEnd = 0x08;
        private const byte VdfInt64 = 0x0A;

        private readonly SteamNonSteamImporterSettingsViewModel settingsViewModel;
        private readonly PluginLog log;

        public override Guid Id { get; } = Guid.Parse("8d105bff-eac4-45c5-90ba-c77fdd66b882");
        public override string Name => "Steam Non-Steam Importer";

        private SteamNonSteamImporterSettings Settings => settingsViewModel.Settings;

        public SteamNonSteamImporter(IPlayniteAPI api) : base(api)
        {
            settingsViewModel = new SteamNonSteamImporterSettingsViewModel(this);
            log = new PluginLog(api, () => settingsViewModel.Settings, settingsViewModel.LogEntries);

            Properties = new LibraryPluginProperties
            {
                HasSettings = true
            };

            log.Info("Plugin loaded. Writing to the Playnite log is disabled by default.");
        }

        public override ISettings GetSettings(bool firstRunSettings)
        {
            return settingsViewModel;
        }

        public override UserControl GetSettingsView(bool firstRunSettings)
        {
            return new SteamNonSteamImporterSettingsView();
        }

        public override IEnumerable<GameMetadata> GetGames(LibraryGetGamesArgs args)
        {
            var games = new List<GameMetadata>();
            var userDataPath = GetSteamUserDataPath();

            log.Info("Import started.");

            if (string.IsNullOrEmpty(userDataPath) || !Directory.Exists(userDataPath))
            {
                const string message = "The Steam userdata folder could not be located.";
                log.Error(message);
                AddErrorNotification("SteamNonSteamImporter.Path", message);
                return games;
            }

            var userFoldersScanned = 0;
            var shortcutFilesFound = 0;
            var ignoredShortcuts = 0;

            try
            {
                foreach (var userFolder in Directory.GetDirectories(userDataPath))
                {
                    userFoldersScanned++;
                    var shortcutsPath = Path.Combine(userFolder, "config", "shortcuts.vdf");

                    if (!File.Exists(shortcutsPath))
                    {
                        log.Debug($"No shortcuts.vdf found for {Path.GetFileName(userFolder)}.");
                        continue;
                    }

                    shortcutFilesFound++;
                    log.Debug($"Reading {shortcutsPath}.");

                    Dictionary<string, Dictionary<string, string>> parsedShortcuts;
                    try
                    {
                        parsedShortcuts = ParseShortcutsVdf(shortcutsPath);
                    }
                    catch (Exception exception)
                    {
                        ignoredShortcuts++;
                        log.Error($"Failed to read {shortcutsPath}.", exception);
                        continue;
                    }

                    foreach (var shortcut in parsedShortcuts)
                    {
                        var data = shortcut.Value;
                        var appName = GetValue(data, "AppName");
                        var exePath = NormalizeFilePath(GetValue(data, "Exe"));
                        var iconPath = NormalizeIconPath(GetValue(data, "icon"));
                        var appIdText = GetValue(data, "appid") ?? "0";

                        uint appId;
                        if (!uint.TryParse(appIdText, NumberStyles.Integer, CultureInfo.InvariantCulture, out appId))
                        {
                            appId = 0;
                        }

                        if (string.IsNullOrWhiteSpace(appName) || string.IsNullOrWhiteSpace(exePath))
                        {
                            ignoredShortcuts++;
                            log.Debug($"Shortcut {shortcut.Key} skipped: missing name or executable.");
                            continue;
                        }

                        var isInstalled = File.Exists(exePath);
                        var runGameId = ((ulong)appId << 32) | 0x02000000UL;
                        var gameActions = new List<GameAction>
                        {
                            new GameAction
                            {
                                Type = GameActionType.URL,
                                Path = $"steam://rungameid/{runGameId}",
                                IsPlayAction = true,
                                Name = "Play via Steam"
                            },
                            new GameAction
                            {
                                Type = GameActionType.File,
                                Path = exePath,
                                IsPlayAction = false,
                                Name = "Launch directly"
                            }
                        };

                        var gameId = appId == 0
                            ? $"nonsteam_{Path.GetFileName(userFolder)}_{shortcut.Key}"
                            : $"nonsteam_{appId}";

                        games.Add(new GameMetadata
                        {
                            Name = appName,
                            GameId = gameId,
                            Source = new MetadataNameProperty("Steam"),
                            Platforms = new HashSet<MetadataProperty>
                            {
                                new MetadataNameProperty("PC (Windows)")
                            },
                            InstallDirectory = isInstalled ? Path.GetDirectoryName(exePath) : null,
                            IsInstalled = isInstalled,
                            Icon = !string.IsNullOrEmpty(iconPath) && File.Exists(iconPath)
                                ? new MetadataFile(iconPath)
                                : null,
                            GameActions = gameActions
                        });

                        log.Debug($"Imported: {appName}.");
                    }
                }
            }
            catch (Exception exception)
            {
                const string message = "The import was interrupted by an unexpected error.";
                log.Error(message, exception);
                AddErrorNotification("SteamNonSteamImporter.Import", $"{message} {exception.Message}");
            }

            log.Info(
                $"Import completed: {games.Count} game(s), " +
                $"{ignoredShortcuts} skipped, {shortcutFilesFound} file(s) across " +
                $"{userFoldersScanned} user folder(s).");

            return games;
        }

        private Dictionary<string, Dictionary<string, string>> ParseShortcutsVdf(string filePath)
        {
            var shortcuts = new Dictionary<string, Dictionary<string, string>>();

            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new BinaryReader(stream, Encoding.UTF8))
            {
                if (stream.Length == 0)
                {
                    return shortcuts;
                }

                var rootType = reader.ReadByte();
                var rootName = ReadNullTerminatedString(reader);
                if (rootType != VdfObject || !string.Equals(rootName, "shortcuts", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException("The file does not contain the 'shortcuts' VDF root.");
                }

                while (stream.Position < stream.Length)
                {
                    var valueType = reader.ReadByte();
                    if (valueType == VdfEnd)
                    {
                        break;
                    }

                    var shortcutIndex = ReadNullTerminatedString(reader);
                    if (valueType != VdfObject)
                    {
                        SkipValue(reader, valueType);
                        continue;
                    }

                    shortcuts[shortcutIndex] = ReadObjectValues(reader);
                }
            }

            log.Debug($"VDF parsed: {shortcuts.Count} shortcut(s) in {Path.GetFileName(filePath)}.");
            return shortcuts;
        }

        private Dictionary<string, string> ReadObjectValues(BinaryReader reader)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            while (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                var valueType = reader.ReadByte();
                if (valueType == VdfEnd)
                {
                    break;
                }

                var name = ReadNullTerminatedString(reader);
                switch (valueType)
                {
                    case VdfObject:
                        SkipObject(reader);
                        break;
                    case VdfString:
                        values[name] = ReadNullTerminatedString(reader);
                        break;
                    case VdfInt32:
                        EnsureBytesAvailable(reader, 4);
                        values[name] = reader.ReadUInt32().ToString(CultureInfo.InvariantCulture);
                        break;
                    case VdfUInt64:
                        EnsureBytesAvailable(reader, 8);
                        values[name] = reader.ReadUInt64().ToString(CultureInfo.InvariantCulture);
                        break;
                    case VdfInt64:
                        EnsureBytesAvailable(reader, 8);
                        values[name] = reader.ReadInt64().ToString(CultureInfo.InvariantCulture);
                        break;
                    default:
                        SkipValue(reader, valueType);
                        break;
                }
            }

            return values;
        }

        private void SkipObject(BinaryReader reader)
        {
            while (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                var valueType = reader.ReadByte();
                if (valueType == VdfEnd)
                {
                    return;
                }

                ReadNullTerminatedString(reader);
                SkipValue(reader, valueType);
            }

            throw new EndOfStreamException("Unexpected end of stream while reading a VDF object.");
        }

        private void SkipValue(BinaryReader reader, byte valueType)
        {
            switch (valueType)
            {
                case VdfObject:
                    SkipObject(reader);
                    break;
                case VdfString:
                    ReadNullTerminatedString(reader);
                    break;
                case VdfInt32:
                case VdfFloat32:
                case VdfPointer:
                case VdfColor:
                    EnsureBytesAvailable(reader, 4);
                    reader.BaseStream.Seek(4, SeekOrigin.Current);
                    break;
                case VdfWideString:
                    ReadNullTerminatedWideString(reader);
                    break;
                case VdfUInt64:
                case VdfInt64:
                    EnsureBytesAvailable(reader, 8);
                    reader.BaseStream.Seek(8, SeekOrigin.Current);
                    break;
                default:
                    throw new InvalidDataException($"Unsupported VDF type: 0x{valueType:X2}.");
            }
        }

        private static string ReadNullTerminatedString(BinaryReader reader)
        {
            var bytes = new List<byte>();
            while (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                var value = reader.ReadByte();
                if (value == 0)
                {
                    return Encoding.UTF8.GetString(bytes.ToArray());
                }

                bytes.Add(value);
            }

            throw new EndOfStreamException("VDF string is missing a null terminator.");
        }

        private static void ReadNullTerminatedWideString(BinaryReader reader)
        {
            while (reader.BaseStream.Position + 1 < reader.BaseStream.Length)
            {
                if (reader.ReadUInt16() == 0)
                {
                    return;
                }
            }

            throw new EndOfStreamException("UTF-16 VDF string is missing a null terminator.");
        }

        private static void EnsureBytesAvailable(BinaryReader reader, int count)
        {
            if (reader.BaseStream.Position + count > reader.BaseStream.Length)
            {
                throw new EndOfStreamException("Unexpected end of stream while reading the VDF file.");
            }
        }

        private string GetSteamUserDataPath()
        {
            var configuredUserDataPath = NormalizeDirectoryPath(Settings.UserDataPath);
            if (!string.IsNullOrEmpty(configuredUserDataPath))
            {
                if (Directory.Exists(configuredUserDataPath))
                {
                    log.Debug($"Using configured userdata folder: {configuredUserDataPath}.");
                    return configuredUserDataPath;
                }

                log.Warning("The configured userdata folder does not exist; automatic detection will be used.");
            }

            var steamPath = NormalizeDirectoryPath(Settings.SteamPath);
            if (!string.IsNullOrEmpty(steamPath) && !Directory.Exists(steamPath))
            {
                log.Warning("The configured Steam folder does not exist; automatic detection will be used.");
                steamPath = null;
            }

            if (string.IsNullOrEmpty(steamPath))
            {
                steamPath = DetectSteamPath();
            }

            if (string.IsNullOrEmpty(steamPath))
            {
                return null;
            }

            var userDataPath = Path.Combine(steamPath, "userdata");
            if (!Directory.Exists(userDataPath))
            {
                log.Warning($"Steam was found, but the userdata folder does not exist under {steamPath}.");
                return null;
            }

            log.Debug($"Detected userdata folder: {userDataPath}.");
            return userDataPath;
        }

        private string DetectSteamPath()
        {
            var candidates = new[]
            {
                Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null)?.ToString(),
                Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam", "InstallPath", null)?.ToString(),
                Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath", null)?.ToString(),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam")
            };

            foreach (var candidate in candidates)
            {
                var normalized = NormalizeDirectoryPath(candidate);
                if (!string.IsNullOrEmpty(normalized) && Directory.Exists(normalized))
                {
                    log.Debug($"Steam detected at {normalized}.");
                    return normalized;
                }
            }

            return null;
        }

        private static string NormalizeDirectoryPath(string path)
        {
            return string.IsNullOrWhiteSpace(path)
                ? null
                : path.Trim().Trim('"').Replace('/', '\\');
        }

        private static string NormalizeFilePath(string path)
        {
            return string.IsNullOrWhiteSpace(path)
                ? null
                : path.Trim().Trim('"');
        }

        private static string NormalizeIconPath(string path)
        {
            var normalized = NormalizeFilePath(path);
            if (string.IsNullOrEmpty(normalized))
            {
                return null;
            }

            var commaIndex = normalized.LastIndexOf(',');
            if (commaIndex > 2)
            {
                int iconIndex;
                if (int.TryParse(normalized.Substring(commaIndex + 1), out iconIndex))
                {
                    normalized = normalized.Substring(0, commaIndex);
                }
            }

            return normalized;
        }

        private static string GetValue(Dictionary<string, string> values, string key)
        {
            string value;
            return values.TryGetValue(key, out value) ? value : null;
        }

        private void AddErrorNotification(string id, string message)
        {
            PlayniteApi.Notifications.Add(new NotificationMessage(id, message, NotificationType.Error));
        }
    }
}
