using Playnite.SDK;
using System;
using System.Windows;
using System.Windows.Controls;

namespace SteamNonSteamImporter
{
    public partial class SteamNonSteamImporterSettingsView : UserControl
    {
        public SteamNonSteamImporterSettingsView()
        {
            InitializeComponent();
        }

        private SteamNonSteamImporterSettingsViewModel ViewModel =>
            DataContext as SteamNonSteamImporterSettingsViewModel;

        private void SelectSteamFolder_Click(object sender, RoutedEventArgs e)
        {
            var path = API.Instance.Dialogs.SelectFolder();
            if (!string.IsNullOrWhiteSpace(path) && ViewModel != null)
            {
                ViewModel.Settings.SteamPath = path;
            }
        }

        private void SelectUserDataFolder_Click(object sender, RoutedEventArgs e)
        {
            var path = API.Instance.Dialogs.SelectFolder();
            if (!string.IsNullOrWhiteSpace(path) && ViewModel != null)
            {
                ViewModel.Settings.UserDataPath = path;
            }
        }

        private void CopyLogs_Click(object sender, RoutedEventArgs e)
        {
            var text = ViewModel?.GetLogText();
            if (!string.IsNullOrEmpty(text))
            {
                Clipboard.SetText(text);
            }
        }

        private void ClearLogs_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.ClearLogs();
        }
    }
}
