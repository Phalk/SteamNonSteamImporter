using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text; // For Encoding.UTF8
using System.Linq; // Keep this for Any() if you still use it in GameMetadata section
using Newtonsoft.Json; // Para parsing do JSON

namespace SteamNonSteamImporter
{
    public class SteamNonSteamImporter : LibraryPlugin
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        // Classe para desserializar o settings.json
        private class PluginSettings
        {
            public string SteamPath { get; set; } = string.Empty;
            public string UserDataPath { get; set; } = string.Empty;
        }

        private readonly PluginSettings settings;

        public override Guid Id { get; } = Guid.Parse("8d105bff-eac4-45c5-90ba-c77fdd66b882");
        public override string Name => "Steam Non-Steam Importer";

        public SteamNonSteamImporter(IPlayniteAPI api) : base(api)
        {
            Properties = new LibraryPluginProperties
            {
                HasSettings = false
            };

            // Carregar configurações do settings.json
            settings = LoadSettings();
            logger.Info("SteamNonSteamImporter inicializado.");
        }

        private PluginSettings LoadSettings()
        {
            string settingsPath = Path.Combine(GetPluginUserDataPath(), "settings.json");
            logger.Info($"Tentando carregar configurações de: {settingsPath}");

            try
            {
                if (File.Exists(settingsPath))
                {
                    string jsonContent = File.ReadAllText(settingsPath);
                    var loadedSettings = JsonConvert.DeserializeObject<PluginSettings>(jsonContent);
                    logger.Info("Configurações carregadas com sucesso.");
                    return loadedSettings ?? new PluginSettings();
                }
                else
                {
                    logger.Warn("Arquivo settings.json não encontrado. Criando um novo com valores padrão.");
                    var defaultSettings = new PluginSettings();
                    File.WriteAllText(settingsPath, JsonConvert.SerializeObject(defaultSettings, Formatting.Indented));
                    return defaultSettings;
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Erro ao carregar settings.json. Usando valores padrão.");
                PlayniteApi.Notifications.Add(new NotificationMessage(
                    "SteamNonSteamImporterSettingsError",
                    $"Erro ao carregar configurações: {ex.Message}. Usando valores padrão.",
                    NotificationType.Error));
                return new PluginSettings();
            }
        }

        public override IEnumerable<GameMetadata> GetGames(LibraryGetGamesArgs args)
        {
            var games = new List<GameMetadata>();
            logger.Info("Iniciando importação de jogos não Steam (parse manual).");

            string steamUserDataPath = GetSteamUserDataPath();
            logger.Info($"Caminho do Steam userdata detectado: {steamUserDataPath ?? "Não encontrado"}");

            if (string.IsNullOrEmpty(steamUserDataPath) || !Directory.Exists(steamUserDataPath))
            {
                PlayniteApi.Notifications.Add(new NotificationMessage(
                    "SteamNonSteamImporterError",
                    "Não foi possível localizar a pasta 'userdata' do Steam.",
                    NotificationType.Error));
                logger.Error($"Caminho do Steam userdata não encontrado ou não existe: {steamUserDataPath}");
                return games;
            }

            try
            {
                foreach (var userFolder in Directory.GetDirectories(steamUserDataPath))
                {
                    logger.Info($"Explorando pasta de usuário: {userFolder}");
                    string shortcutsPath = Path.Combine(userFolder, "config", "shortcuts.vdf");

                    if (File.Exists(shortcutsPath))
                    {
                        logger.Info($"Arquivo shortcuts.vdf encontrado em: {shortcutsPath}. Processando manualmente.");

                        try
                        {
                            var parsedShortcuts = ParseShortcutsVdf(shortcutsPath);

                            if (parsedShortcuts == null || parsedShortcuts.Count == 0)
                            {
                                logger.Info($"Nenhum atalho não Steam encontrado ou o arquivo '{shortcutsPath}' está vazio/inválido após o parse manual.");
                                continue;
                            }

                            foreach (var shortcutData in parsedShortcuts.Values)
                            {
                                string appName = shortcutData.ContainsKey("AppName") ? shortcutData["AppName"] : null;
                                string exePath = shortcutData.ContainsKey("Exe") ? shortcutData["Exe"].Trim('"') : null;
                                string appIdString = shortcutData.ContainsKey("appid") ? shortcutData["appid"] : "0";

                                uint appId = 0;
                                if (!uint.TryParse(appIdString, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out appId))
                                {
                                    logger.Warn($"Não foi possível converter 'appid' para uint: {appIdString}. Usando 0.");
                                }

                                string iconPath = shortcutData.ContainsKey("icon") ? shortcutData["icon"].Trim('"') : null;

                                if (string.IsNullOrEmpty(appName) || string.IsNullOrEmpty(exePath))
                                {
                                    logger.Warn($"Atalho ignorado por falta de AppName ou Exe. Caminho: {shortcutsPath}");
                                    continue;
                                }

                                bool isInstalled = File.Exists(exePath);

                                // Calculate the 64-bit rungameid from the 32-bit appId
                                ulong rungameId = ((ulong)appId << 32) | 0x02000000UL;

                                var game = new GameMetadata
                                {
                                    Name = appName,
                                    GameId = $"nonsteam_{appId}",
                                    Source = new MetadataNameProperty("Steam"),
                                    Platforms = new HashSet<MetadataProperty> { new MetadataNameProperty("PC (Windows)") },
                                    InstallDirectory = Path.GetDirectoryName(exePath),
                                    IsInstalled = isInstalled,
                                    Icon = !string.IsNullOrEmpty(iconPath) && File.Exists(iconPath) ? new MetadataFile(iconPath) : null,
                                    GameActions = new List<GameAction>
                                    {
                                        new GameAction
                                        {
                                            Type = GameActionType.URL, // Alterado para URL
                                            Path = $"steam://rungameid/{rungameId}", // Usando o rungameId calculado
                                            IsPlayAction = true,
                                            Name = "Play via Steam" // Nome da ação atualizado para refletir o método de lançamento
                                        }
                                    }
                                };

                                games.Add(game);
                                logger.Info($"Jogo importado: {appName}, Exe: {exePath}, Instalado: {isInstalled}");
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.Error(ex, $"Erro ao processar o arquivo shortcuts.vdf manualmente: {shortcutsPath}");
                            PlayniteApi.Notifications.Add(new NotificationMessage(
                               "SteamVdfParseError",
                               $"Erro ao processar o arquivo shortcuts.vdf: {ex.Message}",
                               NotificationType.Error));
                        }
                    }
                    else
                    {
                        logger.Info($"Arquivo shortcuts.vdf não encontrado em: {shortcutsPath}");
                    }
                }
            }
            catch (Exception ex)
            {
                PlayniteApi.Notifications.Add(new NotificationMessage(
                    "SteamNonSteamImporterError",
                    $"Erro geral ao importar jogos não Steam: {ex.Message}",
                    NotificationType.Error));
                logger.Error(ex, "Erro geral na importação de jogos.");
            }

            logger.Info($"Total de jogos não Steam importados: {games.Count}");
            return games;
        }

        // --- Manual VDF Parsing Logic ---
        private Dictionary<string, Dictionary<string, string>> ParseShortcutsVdf(string filePath)
        {
            var shortcuts = new Dictionary<string, Dictionary<string, string>>();
            logger.Debug($"ParseShortcutsVdf: Iniciando parse para {filePath}");

            try
            {
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                using (var reader = new BinaryReader(fs, Encoding.UTF8))
                {
                    logger.Debug($"ParseShortcutsVdf: Tamanho do arquivo: {reader.BaseStream.Length} bytes.");

                    if (reader.BaseStream.Length < 1)
                    {
                        logger.Warn($"ParseShortcutsVdf: Arquivo VDF '{filePath}' está vazio.");
                        return null;
                    }

                    byte firstByte = reader.ReadByte();
                    logger.Debug($"ParseShortcutsVdf: Posição inicial: {fs.Position - 1}, Primeiro byte: {firstByte} (0x{firstByte:X2})");
                    if (firstByte != 0x00)
                    {
                        logger.Warn($"ParseShortcutsVdf: Arquivo VDF '{filePath}' não inicia com o tipo de objeto esperado (0x00). Encontrado: 0x{firstByte:X2}.");
                        return null;
                    }

                    string rootKey = ReadNullTerminatedString(reader);
                    logger.Debug($"ParseShortcutsVdf: Posição após rootKey: {fs.Position}, Chave raiz: '{rootKey}'");
                    if (!rootKey.Equals("shortcuts", StringComparison.OrdinalIgnoreCase))
                    {
                        logger.Warn($"ParseShortcutsVdf: Chave raiz inesperada no VDF '{filePath}'. Esperado 'shortcuts', encontrado '{rootKey}'.");
                        return null;
                    }

                    if (reader.BaseStream.Position >= reader.BaseStream.Length)
                    {
                        logger.Warn($"ParseShortcutsVdf: Fim de arquivo inesperado após chave 'shortcuts' em '{filePath}'.");
                        return null;
                    }
                    byte shortcutsObjectTypeByte = reader.ReadByte();
                    logger.Debug($"ParseShortcutsVdf: Posição após shortcutsObjectTypeByte: {fs.Position}, Byte do tipo de objeto 'shortcuts': {shortcutsObjectTypeByte} (0x{shortcutsObjectTypeByte:X2})");
                    if (shortcutsObjectTypeByte != 0x00)
                    {
                        logger.Warn($"ParseShortcutsVdf: Objeto 'shortcuts' em '{filePath}' não inicia com o tipo de objeto esperado (0x00). Encontrado: 0x{shortcutsObjectTypeByte:X2}.");
                        return null;
                    }

                    logger.Debug("ParseShortcutsVdf: Entrando no loop de leitura de atalhos individuais.");
                    int shortcutCount = 0;
                    while (reader.BaseStream.Position < reader.BaseStream.Length)
                    {
                        logger.Debug($"ParseShortcutsVdf: --- Início do loop de atalho, Posição atual: {fs.Position} ---");

                        byte nextByte = reader.ReadByte();
                        logger.Debug($"ParseShortcutsVdf: Posição após nextByte (para decisão): {fs.Position}, Byte lido: {nextByte} (0x{nextByte:X2})");

                        if (nextByte == 0x08)
                        {
                            logger.Debug("ParseShortcutsVdf: Fim do objeto 'shortcuts' (0x08) encontrado. Saindo do loop de atalhos.");
                            break;
                        }

                        reader.BaseStream.Seek(-1, SeekOrigin.Current);
                        logger.Debug($"ParseShortcutsVdf: Revertendo posição do stream para {fs.Position}. Byte 0x{nextByte:X2} será lido novamente como parte da chave.");

                        string shortcutIndex = ReadNullTerminatedString(reader);
                        logger.Debug($"ParseShortcutsVdf: Posição após shortcutIndex: {fs.Position}, Atalho index: '{shortcutIndex}'");

                        if (string.IsNullOrEmpty(shortcutIndex))
                        {
                            logger.Debug($"ParseShortcutsVdf: Índice de atalho vazio encontrado na posição {fs.Position}. Verificando fim do objeto 'shortcuts'.");
                            if (reader.BaseStream.Position < reader.BaseStream.Length)
                            {
                                byte checkByte = reader.ReadByte();
                                if (checkByte == 0x08)
                                {
                                    logger.Debug("ParseShortcutsVdf: Fim do objeto 'shortcuts' (0x08) encontrado após índice vazio. Saindo do loop de atalhos.");
                                    break;
                                }
                                else
                                {
                                    logger.Warn($"ParseShortcutsVdf: Índice vazio seguido de byte inesperado 0x{checkByte:X2} na posição {fs.Position - 1}. Pulando.");
                                    reader.BaseStream.Seek(-1, SeekOrigin.Current);
                                }
                            }
                            continue;
                        }

                        var currentShortcut = new Dictionary<string, string>();
                        shortcutCount++;

                        logger.Debug($"ParseShortcutsVdf: Entrando no loop de propriedades para atalho '{shortcutIndex}'.");
                        while (reader.BaseStream.Position < reader.BaseStream.Length)
                        {
                            byte propertyType = reader.ReadByte();
                            logger.Debug($"ParseShortcutsVdf: Posição após propertyType: {fs.Position}, Byte lido para tipo de propriedade: {propertyType} (0x{propertyType:X2})");

                            if (propertyType == 0x08)
                            {
                                logger.Debug($"ParseShortcutsVdf: Fim do objeto de atalho '{shortcutIndex}' (0x08) encontrado. Saindo do loop de propriedades.");
                                break;
                            }

                            string propertyName = ReadNullTerminatedString(reader);
                            logger.Debug($"ParseShortcutsVdf: Posição após propertyName: {fs.Position}, Nome da propriedade: '{propertyName}'");
                            string propertyValue = null;

                            switch (propertyType)
                            {
                                case 0x00:
                                    logger.Debug($"ParseShortcutsVdf: Encontrado objeto aninhado para propriedade '{propertyName}'. Processando...");
                                    SkipObject(reader);
                                    logger.Debug($"ParseShortcutsVdf: Objeto aninhado para '{propertyName}' pulado.");
                                    break;
                                case 0x01:
                                    propertyValue = ReadNullTerminatedString(reader);
                                    logger.Debug($"ParseShortcutsVdf: Posição após stringValue: {fs.Position}, Valor da propriedade (string): '{propertyValue}'");
                                    break;
                                case 0x02:
                                    if (reader.BaseStream.Position + 4 > reader.BaseStream.Length)
                                    {
                                        logger.Warn($"ParseShortcutsVdf: Fim de arquivo inesperado ao tentar ler 4 bytes para o inteiro '{propertyName}' em '{filePath}'.");
                                        return null;
                                    }
                                    byte[] intBytes = reader.ReadBytes(4);
                                    propertyValue = BitConverter.ToUInt32(intBytes, 0).ToString(System.Globalization.CultureInfo.InvariantCulture);
                                    logger.Debug($"ParseShortcutsVdf: Posição após intValue: {fs.Position}, Valor da propriedade (uint): '{propertyValue}'");
                                    break;
                                default:
                                    logger.Error($"ParseShortcutsVdf: ERRO: Tipo de propriedade VDF desconhecido ({propertyType}) para '{propertyName}' em '{filePath}'. Pulando e encerrando parse para este arquivo.");
                                    return null;
                            }

                            if (propertyType != 0x00 && propertyName != null && propertyValue != null)
                            {
                                currentShortcut[propertyName] = propertyValue;
                                logger.Debug($"ParseShortcutsVdf: Propriedade adicionada: '{propertyName}' = '{propertyValue}'");
                            }
                        }
                        shortcuts[shortcutIndex] = currentShortcut;
                        logger.Debug($"ParseShortcutsVdf: Atalho '{shortcutIndex}' adicionado ao dicionário principal.");
                    }
                    logger.Debug($"ParseShortcutsVdf: Finalizado o parse de '{filePath}'. Total de atalhos encontrados: {shortcutCount}");
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"ParseShortcutsVdf: Erro inesperado durante o parse manual do VDF '{filePath}'.");
                return null;
            }

            return shortcuts;
        }

        private void SkipObject(BinaryReader reader)
        {
            logger.Debug($"SkipObject: Iniciando skip de objeto na posição {reader.BaseStream.Position}.");
            int depth = 1;
            while (reader.BaseStream.Position < reader.BaseStream.Length && depth > 0)
            {
                byte typeByte = reader.ReadByte();
                if (typeByte != 0x08)
                {
                    ReadNullTerminatedString(reader);
                    if (typeByte == 0x00)
                    {
                        depth++;
                        logger.Debug($"SkipObject: Encontrado objeto aninhado. Profundidade: {depth}.");
                    }
                    else if (typeByte == 0x01)
                    {
                        ReadNullTerminatedString(reader);
                    }
                    else if (typeByte == 0x02)
                    {
                        reader.ReadBytes(4);
                    }
                }
                else
                {
                    depth--;
                    logger.Debug($"SkipObject: Fim de objeto (0x08) encontrado. Profundidade: {depth}.");
                }
            }
            if (depth != 0)
            {
                logger.Warn($"SkipObject: Erro ao pular objeto, profundidade final não é 0. Algo deu errado na leitura.");
            }
            logger.Debug($"SkipObject: Objeto pulado. Posição final: {reader.BaseStream.Position}.");
        }

        private string ReadNullTerminatedString(BinaryReader reader)
        {
            var bytes = new List<byte>();
            byte b;
            long initialPosition = reader.BaseStream.Position;

            while (reader.BaseStream.Position < reader.BaseStream.Length && (b = reader.ReadByte()) != 0x00)
            {
                bytes.Add(b);
            }
            string result = Encoding.UTF8.GetString(bytes.ToArray());
            logger.Debug($"ReadNullTerminatedString: Leu '{result}' da posição {initialPosition} até {reader.BaseStream.Position} (bytes: {string.Join(" ", bytes.Select(x => x.ToString("X2")))})");
            return result;
        }

        private string GetSteamUserDataPath()
        {
            try
            {
                // Primeiro, tenta o caminho do Steam definido no settings.json
                string steamPath = settings.SteamPath;
                if (!string.IsNullOrEmpty(steamPath))
                {
                    if (!Directory.Exists(steamPath))
                    {
                        logger.Warn($"Caminho do Steam definido no settings.json não existe: {steamPath}. Tentando fallback.");
                        PlayniteApi.Notifications.Add(new NotificationMessage(
                            "SteamNonSteamImporterPathWarning",
                            $"Caminho do Steam definido não existe: {steamPath}. Usando padrão.",
                            NotificationType.Info));
                    }
                    else
                    {
                        logger.Info($"Usando caminho do Steam definido no settings.json: {steamPath}");
                    }
                }

                // Se não houver caminho definido ou se for inválido, usa o fallback
                if (string.IsNullOrEmpty(steamPath) || !Directory.Exists(steamPath))
                {
                    steamPath = (Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam", "InstallPath", null)
                                ?? Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath", null))
                                ?.ToString();

                    if (!string.IsNullOrEmpty(steamPath))
                    {
                        logger.Info($"Caminho do Steam encontrado no registro: {steamPath}");
                    }
                    else
                    {
                        logger.Warn("Caminho do Steam não encontrado no registro. Tentando caminho padrão.");
                        steamPath = @"C:\Program Files (x86)\Steam";
                        if (Directory.Exists(steamPath))
                        {
                            logger.Info($"Caminho padrão do Steam encontrado: {steamPath}");
                        }
                        else
                        {
                            logger.Error("Caminho padrão do Steam não existe.");
                            steamPath = null;
                        }
                    }
                }

                if (string.IsNullOrEmpty(steamPath))
                {
                    logger.Error("Caminho do Steam não pôde ser determinado.");
                    return null;
                }

                // Agora, tenta o caminho do user ID definido no settings.json
                string userDataPath = settings.UserDataPath;
                if (!string.IsNullOrEmpty(userDataPath))
                {
                    if (!Directory.Exists(userDataPath))
                    {
                        logger.Warn($"Caminho do user ID do Steam definido no settings.json não existe: {userDataPath}. Tentando fallback.");
                        PlayniteApi.Notifications.Add(new NotificationMessage(
                            "SteamNonSteamImporterPathWarning",
                            $"Caminho do user ID do Steam definido não existe: {userDataPath}. Usando padrão.",
                            NotificationType.Info));
                    }
                    else
                    {
                        logger.Info($"Usando caminho do user ID do Steam definido no settings.json: {userDataPath}");
                        return userDataPath;
                    }
                }

                // Fallback para o caminho padrão do user ID (steamPath/userdata)
                userDataPath = Path.Combine(steamPath, "userdata");
                if (Directory.Exists(userDataPath))
                {
                    logger.Info($"Caminho padrão do user ID do Steam encontrado: {userDataPath}");
                    return userDataPath;
                }

                logger.Error("Caminho do user ID do Steam não pôde ser determinado.");
                return null;
            }
            catch (Exception ex)
            {
                PlayniteApi.Notifications.Add(new NotificationMessage(
                    "SteamNonSteamImporterPathError",
                    $"Erro ao determinar o caminho do Steam: {ex.Message}",
                    NotificationType.Error));
                logger.Error(ex, "Erro ao determinar o caminho do Steam.");
                return null;
            }
        }
    }
}