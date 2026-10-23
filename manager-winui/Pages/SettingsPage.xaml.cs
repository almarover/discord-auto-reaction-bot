// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using System.Diagnostics;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace DiscordReactionManager_WinUI.Pages;

public sealed partial class SettingsPage : Page
{
    public SettingsPage()
    {
        InitializeComponent();
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        AutoManagerSwitch.IsOn = AppServices.Settings.AutoStartManager;
        AutoBotSwitch.IsOn = AppServices.Settings.AutoStartBot;
        LaunchHiddenSwitch.IsOn = AppServices.Settings.LaunchHidden;
        CloseToTraySwitch.IsOn = AppServices.Settings.CloseToTray;
        ProjectPathText.Text = AppServices.ProjectDirectory;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        AppServices.Settings.AutoStartManager = AutoManagerSwitch.IsOn;
        AppServices.Settings.AutoStartBot = AutoBotSwitch.IsOn;
        AppServices.Settings.LaunchHidden = LaunchHiddenSwitch.IsOn;
        AppServices.Settings.CloseToTray = CloseToTraySwitch.IsOn;
        try
        {
            AppServices.ApplyManagerAutostart(AutoManagerSwitch.IsOn);
            AppServices.SaveSettings();
            MessageBar.Message = "设置已保存。";
            MessageBar.Severity = InfoBarSeverity.Success;
        }
        catch (Exception error)
        {
            MessageBar.Message = $"保存失败：{error.Message}";
            MessageBar.Severity = InfoBarSeverity.Error;
        }
        MessageBar.IsOpen = true;
    }

    private void OpenProject_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{AppServices.ProjectDirectory}\"") { UseShellExecute = true });
    }

}
