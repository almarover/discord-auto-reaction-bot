using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace DiscordReactionManager_WinUI.Pages;

public sealed partial class PublicPage : Page
{
    private readonly Dictionary<string, NumberBox> _weightInputs = [];
    private readonly HashSet<string> _selectedChannelIds = new(StringComparer.Ordinal);
    private bool _loading;

    public PublicPage() => InitializeComponent();

    private void Page_Loaded(object sender, RoutedEventArgs e) => LoadServers();

    private void LoadServers()
    {
        _loading = true;
        AppServices.RefreshRuntime();
        AppServices.ReloadConfig();
        List<GuildOption> servers = AppServices.GuildOptions();
        ServerCombo.ItemsSource = servers;
        ServerCombo.SelectedIndex = servers.Count > 0 ? 0 : -1;
        _loading = false;
        LoadSelectedServer();
    }

    private GuildOption? SelectedServer => ServerCombo.SelectedItem as GuildOption;

    private void ServerCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loading) LoadSelectedServer();
    }

    private void LoadSelectedServer()
    {
        _loading = true;
        _selectedChannelIds.Clear();
        if (SelectedServer is not GuildOption option)
        {
            ReactionText.Text = "";
            ReactionPreview.Render(PublicPreviewRows, "");
            ChannelSummaryText.Text = "尚无服务器";
            BuildProbabilityRows(new Dictionary<string, double>());
            _loading = false;
            return;
        }

        GuildConfig guild = AppServices.EnsureGuild(option.Id, option.Name);
        guild.CommonChannelIds.ForEach(id => _selectedChannelIds.Add(id));
        ServerEnabledSwitch.IsOn = guild.Enabled;
        ReactionText.Text = string.Join(Environment.NewLine, guild.CommonReactions);
        PublicRandomSwitch.IsOn = guild.CommonRandomEnabled;
        PublicNoReactionSwitch.IsOn = guild.CommonNoReactionEnabled;
        PublicNoReactionProbabilityBox.Value = guild.CommonNoReactionProbability;
        PublicNoReactionProbabilityBox.IsEnabled = guild.CommonNoReactionEnabled;
        ReactionPreview.Render(PublicPreviewRows, ReactionText.Text);
        BuildProbabilityRows(guild.CommonReactionWeights);
        UpdateChannelSummary();
        _loading = false;
    }

    private async void ChooseChannels_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedServer is not GuildOption option)
        {
            ShowMessage("请先选择服务器。", InfoBarSeverity.Warning);
            return;
        }
        List<ChannelOption> channels = AppServices.ChannelOptions(option.Id);
        ChannelSelectionResult? result = await ChannelSelectionDialog.ShowAsync(
            XamlRoot,
            "选择公共 Reaction 生效频道",
            channels,
            _selectedChannelIds);
        if (result is null) return;
        _selectedChannelIds.Clear();
        result.ChannelIds.ForEach(id => _selectedChannelIds.Add(id));
        UpdateChannelSummary();
    }

    private void UpdateChannelSummary()
    {
        if (SelectedServer is not GuildOption option)
        {
            ChannelSummaryText.Text = "尚无服务器";
            return;
        }
        ChannelSummaryText.Text = ChannelSelectionDialog.Summary(
            _selectedChannelIds,
            AppServices.ChannelOptions(option.Id));
    }

    private void ReactionText_TextChanged(object sender, TextChangedEventArgs e)
    {
        ReactionPreview.Render(PublicPreviewRows, ReactionText.Text);
        if (!_loading)
        {
            Dictionary<string, double> current = _weightInputs.ToDictionary(pair => pair.Key, pair => pair.Value.Value);
            BuildProbabilityRows(current);
        }
    }

    private void PublicRandomSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_loading) BuildProbabilityRows(_weightInputs.ToDictionary(pair => pair.Key, pair => pair.Value.Value));
    }

    private void PublicNoReactionSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        PublicNoReactionProbabilityBox.IsEnabled = PublicNoReactionSwitch.IsOn;
    }

    private void BuildProbabilityRows(IReadOnlyDictionary<string, double> weights)
    {
        List<string> reactions = AppServices.ParseReactions(ReactionText.Text);
        PublicProbabilityRows.Children.Clear();
        _weightInputs.Clear();
        double defaultWeight = reactions.Count == 0 ? 0 : 100.0 / reactions.Count;

        foreach (string reaction in reactions)
        {
            Grid row = new() { ColumnSpacing = 12, Padding = new Thickness(12, 8, 12, 8), Background = (Brush)Application.Current.Resources["PanelBrush"], CornerRadius = new CornerRadius(5) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
            TextBlock tokenText = new() { Text = reaction, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
            NumberBox number = new()
            {
                Minimum = 0,
                Maximum = 100,
                SmallChange = 0.1,
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
                Value = weights.TryGetValue(reaction, out double saved) ? saved : defaultWeight,
                IsEnabled = PublicRandomSwitch.IsOn,
            };
            number.ValueChanged += (_, _) => UpdateProbabilityTotal();
            Grid.SetColumn(number, 1);
            row.Children.Add(tokenText);
            row.Children.Add(number);
            PublicProbabilityRows.Children.Add(row);
            _weightInputs[reaction] = number;
        }
        UpdateProbabilityTotal();
    }

    private void PublicEqualWeights_Click(object sender, RoutedEventArgs e)
    {
        if (_weightInputs.Count == 0) return;
        double baseValue = Math.Floor(10000.0 / _weightInputs.Count) / 100.0;
        double assigned = 0;
        int index = 0;
        foreach (NumberBox box in _weightInputs.Values)
        {
            index++;
            box.Value = index == _weightInputs.Count ? Math.Round(100.0 - assigned, 2) : baseValue;
            assigned += box.Value;
        }
        UpdateProbabilityTotal();
    }

    private void UpdateProbabilityTotal()
    {
        double total = _weightInputs.Values.Sum(box => double.IsNaN(box.Value) ? 0 : box.Value);
        PublicProbabilityTotalText.Text = $"总计 {total:0.##}%";
        PublicProbabilityTotalText.Foreground = new SolidColorBrush(Math.Abs(total - 100) < 0.005
            ? Color.FromArgb(255, 54, 230, 138)
            : Color.FromArgb(255, 255, 107, 107));
    }

    private void Reload_Click(object sender, RoutedEventArgs e) => LoadServers();

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedServer is not GuildOption option)
        {
            ShowMessage("请先选择服务器。", InfoBarSeverity.Warning);
            return;
        }
        List<string> reactions = AppServices.ParseReactions(ReactionText.Text);
        Dictionary<string, double> weights = _weightInputs.ToDictionary(
            pair => pair.Key,
            pair => Math.Round(double.IsNaN(pair.Value.Value) ? 0 : pair.Value.Value, 4));
        if (PublicRandomSwitch.IsOn)
        {
            if (reactions.Count == 0)
            {
                ShowMessage("公共随机模式至少需要一个 Reaction。", InfoBarSeverity.Error);
                return;
            }
            if (Math.Abs(weights.Values.Sum() - 100) >= 0.005)
            {
                ShowMessage("公共 Reaction 随机概率合计必须精确为 100%。", InfoBarSeverity.Error);
                return;
            }
        }
        double noReactionProbability = PublicNoReactionProbabilityBox.Value;
        if (double.IsNaN(noReactionProbability) || noReactionProbability < 0 || noReactionProbability > 100)
        {
            ShowMessage("不进行 Reaction 的概率必须在 0% 到 100% 之间。", InfoBarSeverity.Error);
            return;
        }

        GuildConfig guild = AppServices.EnsureGuild(option.Id, option.Name);
        guild.Enabled = ServerEnabledSwitch.IsOn;
        guild.CommonReactions = reactions;
        guild.CommonChannelIds = _selectedChannelIds.ToList();
        guild.CommonRandomEnabled = PublicRandomSwitch.IsOn;
        guild.CommonReactionWeights = weights;
        guild.CommonNoReactionEnabled = PublicNoReactionSwitch.IsOn;
        guild.CommonNoReactionProbability = Math.Round(noReactionProbability, 4);
        AppServices.SaveConfig();
        ShowMessage($"已保存 {guild.CommonReactions.Count} 个公共 Reaction。", InfoBarSeverity.Success);
    }

    private void ShowMessage(string message, InfoBarSeverity severity)
    {
        MessageBar.Message = message;
        MessageBar.Severity = severity;
        MessageBar.IsOpen = true;
    }
}
