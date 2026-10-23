using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Diagnostics;

namespace DiscordReactionManager_WinUI.Pages;

public sealed partial class LogsPage : Page
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };

    public LogsPage()
    {
        InitializeComponent();
        _timer.Tick += (_, _) => RefreshLogs();
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        RefreshLogs();
        _timer.Start();
    }

    private void Page_Unloaded(object sender, RoutedEventArgs e) => _timer.Stop();

    private void RefreshLogs()
    {
        if (PauseSwitch.IsOn) return;
        IEnumerable<string> lines = AppServices.Bot.LogSnapshot();
        string filter = (FilterCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "all";
        if (filter == "error") lines = lines.Where(line => line.Contains("error", StringComparison.OrdinalIgnoreCase) || line.Contains("failed", StringComparison.OrdinalIgnoreCase) || line.Contains("失败"));
        else if (filter == "reaction") lines = lines.Where(line => line.Contains("Reaction", StringComparison.OrdinalIgnoreCase));
        string[] visible = lines.TakeLast(2000).ToArray();
        LogText.Text = string.Join(Environment.NewLine, visible);
        LogStatusText.Text = $"当前显示 {visible.Length} 行 · 完整日志：{AppServices.LogPath}";
    }

    private void FilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LogText is not null) RefreshLogs();
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        AppServices.Bot.ClearLogs();
        RefreshLogs();
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{AppServices.LogPath}\"") { UseShellExecute = true });
    }
}
