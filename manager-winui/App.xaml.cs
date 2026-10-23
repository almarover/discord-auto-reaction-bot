using Microsoft.UI.Xaml;
using System.Threading;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace DiscordReactionManager_WinUI;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    private Mutex? _instanceMutex;
    public MainWindow? MainWindow { get; private set; }
    
    /// <summary>
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        _instanceMutex = new Mutex(true, "DiscordReactionManager.WinUI.SingleInstance", out bool isFirstInstance);
        if (!isFirstInstance)
        {
            Exit();
            return;
        }

        AppServices.Initialize();
        MainWindow = new MainWindow();
        MainWindow.Activate();

        bool startup = Environment.GetCommandLineArgs().Any(item => item.Equals("--startup", StringComparison.OrdinalIgnoreCase));
        if (AppServices.Settings.AutoStartBot)
        {
            _ = AppServices.Bot.StartAsync();
        }
        if (startup && AppServices.Settings.LaunchHidden)
        {
            MainWindow.HideToTray();
        }
    }
}
