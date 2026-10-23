using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace DiscordReactionManager_WinUI.Pages;

public sealed partial class HomePage : Page
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };

    public HomePage()
    {
        InitializeComponent();
        _timer.Tick += (_, _) => RefreshStatus();
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        TokenBox.Password = AppServices.LoadToken();
        RefreshStatus();
        _timer.Start();
    }

    private void Page_Unloaded(object sender, RoutedEventArgs e) => _timer.Stop();

    private void RefreshStatus()
    {
        AppServices.RefreshRuntime();
        RuntimeState runtime = AppServices.Runtime;
        bool processRunning = AppServices.Bot.IsRunning;
        bool connected = processRunning && runtime.Running;
        string stateText = connected ? "运行中" : processRunning ? "启动中" : "未运行";
        HeaderStatusText.Text = stateText;
        BotStateText.Text = stateText;
        ServerCountText.Text = connected ? runtime.Guilds.Count.ToString() : "0";
        BotIdentityText.Text = connected && !string.IsNullOrWhiteSpace(runtime.Bot.Username)
            ? $"{runtime.Bot.Username}\nID: {runtime.Bot.Id}"
            : processRunning ? "正在连接 Discord…" : "尚未连接 Discord";
        RuntimeDetailText.Text = connected
            ? $"Bot 已连接 {runtime.Guilds.Count} 个服务器，配置更改保存后会自动生效。"
            : processRunning ? "Bot 进程已启动，正在等待 Discord 连接。" : "Bot 当前未运行；可以修改配置，随后再启动。";
        Color statusColor = connected
            ? Color.FromArgb(255, 54, 230, 138)
            : processRunning ? Color.FromArgb(255, 240, 180, 70) : Color.FromArgb(255, 168, 166, 160);
        Color backgroundColor = connected
            ? Color.FromArgb(255, 36, 59, 49)
            : processRunning ? Color.FromArgb(255, 62, 52, 34) : Color.FromArgb(255, 47, 48, 46);
        SolidColorBrush color = new(statusColor);
        HeaderStatusText.Foreground = color;
        StatusDot.Fill = color;
        HeaderStatusBorder.Background = new SolidColorBrush(backgroundColor);
    }

    private void SaveToken_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TokenBox.Password))
        {
            ShowMessage("Token 不能为空。", InfoBarSeverity.Error);
            return;
        }
        AppServices.SaveToken(TokenBox.Password);
        ShowMessage("Token 已保存到 .env。", InfoBarSeverity.Success);
    }

    private async void ViewServers_Click(object sender, RoutedEventArgs e)
    {
        AppServices.RefreshRuntime();
        StackPanel list = new() { Spacing = 10 };
        if (AppServices.Runtime.Guilds.Count == 0)
        {
            list.Children.Add(new TextBlock { Text = "当前没有已连接服务器。" });
        }
        foreach (RuntimeGuild guild in AppServices.Runtime.Guilds)
        {
            list.Children.Add(new TextBlock { Text = $"{guild.Name}\nID: {guild.Id}", TextWrapping = TextWrapping.Wrap });
        }
        ContentDialog dialog = new()
        {
            Title = $"已连接服务器（{AppServices.Runtime.Guilds.Count}）",
            Content = new ScrollViewer { Content = list, MaxHeight = 420 },
            CloseButtonText = "关闭",
            XamlRoot = XamlRoot,
        };
        await dialog.ShowAsync();
    }

    private async void StartBot_Click(object sender, RoutedEventArgs e)
    {
        bool started = await AppServices.Bot.StartAsync();
        RefreshStatus();
        ShowMessage(started ? "Bot 启动命令已发送。" : "Bot 启动失败，请查看日志。", started ? InfoBarSeverity.Success : InfoBarSeverity.Error);
    }

    private async void StopBot_Click(object sender, RoutedEventArgs e)
    {
        await AppServices.Bot.StopAsync();
        RefreshStatus();
        ShowMessage("Bot 已停止。", InfoBarSeverity.Informational);
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => ((App)Application.Current).MainWindow?.HideToTray();
    private void Exit_Click(object sender, RoutedEventArgs e) => ((App)Application.Current).MainWindow?.ExitManager();

    private void ShowMessage(string message, InfoBarSeverity severity)
    {
        MessageBar.Message = message;
        MessageBar.Severity = severity;
        MessageBar.IsOpen = true;
    }
}
