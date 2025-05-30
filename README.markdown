# SteamNonSteamImporter

A Playnite plugin that imports non-Steam games from Steam shortcuts into your Playnite library.

## Overview

**SteamNonSteamImporter** is a library plugin for [Playnite](https://playnite.link/) that automatically detects and imports non-Steam games added as shortcuts in Steam. It parses the `shortcuts.vdf` file(s) in your Steam user data directory, converts the shortcuts into Playnite game entries, and allows you to launch them directly via Steam using the `steam://rungameid/` URL scheme.

## Features

- **Automatic Steam Folder Detection**: The plugin automatically detects your Steam installation path through the Windows registry. If detection fails, it falls back to the default path `C:\Program Files (x86)\Steam`.
- **Customizable Paths**: You can manually specify the Steam folder and user data path by editing the `settings.json` file located in Playnite's `extension_data` directory (e.g., `C:\Users\<YourUser>\AppData\Roaming\Playnite\extension_data\8d105bff-eac4-45c5-90ba-c77fdd66b882\settings.json`).
- **Non-Steam Game Import**: Imports non-Steam games with metadata like name, executable path, icon (if available), and installation status.
- **Steam Launch Integration**: Adds a "Play via Steam" action to launch games directly through Steam.
- **Error Handling**: Provides detailed logging and notifications in Playnite if there are issues with folder detection or file parsing.

## Installation

1. Download the latest release from the [Releases](https://github.com/<YourGitHubUsername>/SteamNonSteamImporter/releases) and follow instructions on Playnite.
2. Launch Playnite, go to **Add-ons** → **Extensions**, and ensure the plugin is enabled.
3. Update your library by clicking the refresh button or navigating to **Library** → **Games** → **Update All**.

## Configuration

The plugin automatically detects your Steam folder and user data path. However, if you encounter issues or want to use a custom path (e.g., for a specific Steam user ID), you can configure it manually:

1. Navigate to the plugin's `extension_data` directory:
   - `C:\Users\<YourUser>\AppData\Roaming\Playnite\extension_data\8d105bff-eac4-45c5-90ba-c77fdd66b882`
2. Open or create the `settings.json` file in a text editor (e.g., Notepad).
3. Edit the file to specify your custom paths. Example:
   ```json
   {
     "SteamPath": "D:\\Games\\Steam",
     "UserDataPath": "D:\\Games\\Steam\\userdata\\54808062"
   }
   ```
   - `SteamPath`: The path to your Steam installation. Leave empty (`""`) to use automatic detection.
   - `UserDataPath`: The path to the Steam user data folder (or a specific user ID folder). Leave empty to use the default (`<SteamPath>\userdata`).
   - **Note**: Use double backslashes (`\\`) in paths as shown above.
4. Save the file, restart Playnite, and update your library.

If the specified paths do not exist, the plugin will fall back to automatic detection and log a warning in Playnite's log file (`C:\Users\<YourUser>\AppData\Local\Playnite\playnite.log`).

## Troubleshooting

- **No Games Imported**:
  - Ensure Steam has non-Steam games added as shortcuts.
  - Verify that the `shortcuts.vdf` file exists in your Steam user data directory (e.g., `C:\Program Files (x86)\Steam\userdata\<UserID>\config\shortcuts.vdf`).
  - Check the `settings.json` file for correct paths.
- **Path Detection Issues**:
  - Confirm that your Steam installation path is correctly set in the Windows registry (`HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam` or `HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam`).
  - Manually specify the paths in `settings.json` if automatic detection fails.
- **Logs**:
  - Check Playnite's log file at `C:\Users\<YourUser>\AppData\Local\Playnite\playnite.log` for detailed error messages.

## Contributing

Contributions are welcome! Feel free to open an issue or submit a pull request on GitHub.

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.

## Acknowledgments

- Built for Playnite 10 using .NET Framework 4.8.
- Thanks to the Playnite community for their support and feedback.
