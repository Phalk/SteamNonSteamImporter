# Steam Non-Steam Importer

A Playnite library plugin that imports non-Steam shortcuts stored in `userdata/<SteamId>/config/shortcuts.vdf`.

## Requirements

- Visual Studio with the **.NET Framework 4.6.2** targeting pack.
- NuGet restore for `PlayniteSDK` 6.11.0.

## Development

1. Open `SteamNonSteamImporter.sln`.
2. Restore NuGet packages.
3. Build the `Debug` configuration.
4. In Playnite, add `bin\Debug` under **Settings > For developers > External extensions**.

The project does not include `bin`, `obj`, `.vs`, `packages`, `.user` files, or generated `.pext` packages. These are created locally and excluded by `.gitignore`.

## Logs

- Detailed logging in the settings panel is enabled by default.
- The settings panel keeps up to 300 entries from the current session.
- Writing to Playnite's `extensions.log` remains disabled by default and only occurs when the corresponding option is enabled.

## Cleanup

Run `Clean-Project.cmd` to remove generated build artifacts and the local package folder.
