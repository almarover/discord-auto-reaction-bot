using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using DiscordReactionManager_WinUI.Pages;
using Windows.UI;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace DiscordReactionManager_WinUI;

public sealed partial class MainWindow : Window
{
    private readonly TrayService _tray;
    private bool _allowClose;

    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        AppWindow.SetIcon("Assets/AppIcon.ico");
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1420, 900));
        AppWindow.Closing += AppWindow_Closing;
        _tray = new TrayService(this);
        SelectNavigation("home");
    }

    private void NavButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string tag)
        {
            SelectNavigation(tag);
        }
    }

    private void SelectNavigation(string tag)
    {
        Button[] buttons = [HomeNavButton, PublicNavButton, ConfigNavButton, LogsNavButton, SettingsNavButton];
        Rectangle[] indicators = [HomeIndicator, PublicIndicator, ConfigIndicator, LogsIndicator, SettingsIndicator];
        foreach (Button button in buttons)
        {
            button.Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
        }
        foreach (Rectangle indicator in indicators)
        {
            indicator.Visibility = Visibility.Collapsed;
        }

        (Button selectedButton, Rectangle selectedIndicator, Type pageType) = tag switch
        {
            "public" => (PublicNavButton, PublicIndicator, typeof(PublicPage)),
            "config" => (ConfigNavButton, ConfigIndicator, typeof(ConfigPage)),
            "logs" => (LogsNavButton, LogsIndicator, typeof(LogsPage)),
            "settings" => (SettingsNavButton, SettingsIndicator, typeof(SettingsPage)),
            _ => (HomeNavButton, HomeIndicator, typeof(HomePage)),
        };
        selectedButton.Background = (Brush)Application.Current.Resources["CardHoverBrush"];
        selectedIndicator.Visibility = Visibility.Visible;
        if (NavFrame.CurrentSourcePageType != pageType)
        {
            NavFrame.Navigate(pageType);
        }
    }

    private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (!_allowClose && AppServices.Settings.CloseToTray)
        {
            args.Cancel = true;
            HideToTray();
        }
    }

    public void HideToTray()
    {
        AppWindow.Hide();
    }

    public void ShowFromTray()
    {
        AppWindow.Show();
        Activate();
    }

    public void ExitManager()
    {
        _allowClose = true;
        _tray.Dispose();
        AppServices.Bot.Dispose();
        Close();
    }
}
