using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace DiscordReactionManager_WinUI.Pages;

public sealed partial class ConfigPage : Page
{
    public sealed record TargetListItem(int Index, string Label, string UserId);

    private readonly Dictionary<string, NumberBox> _weightInputs = [];
    private readonly HashSet<string> _selectedChannelIds = new(StringComparer.Ordinal);
    private bool _loading;
    private int _editingIndex = -1;

    public ConfigPage() => InitializeComponent();

    private void Page_Loaded(object sender, RoutedEventArgs e) => LoadServers();

    private GuildOption? SelectedServer => ServerCombo.SelectedItem as GuildOption;

    private GuildConfig? CurrentGuild
    {
        get
        {
            if (SelectedServer is not GuildOption option)
            {
                return null;
            }
            return AppServices.EnsureGuild(option.Id, option.Name);
        }
    }

    private void LoadServers()
    {
        _loading = true;
        AppServices.RefreshRuntime();
        AppServices.ReloadConfig();
        List<GuildOption> servers = AppServices.GuildOptions();
        ServerCombo.ItemsSource = servers;
        ServerCombo.SelectedIndex = servers.Count > 0 ? 0 : -1;
        _loading = false;
        LoadCurrentGuild();
    }

    private void ServerCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loading)
        {
            LoadCurrentGuild();
        }
    }

    private void LoadCurrentGuild()
    {
        GuildConfig? guild = CurrentGuild;
        List<TargetListItem> items = [];
        if (guild is not null)
        {
            for (int index = 0; index < guild.Targets.Count; index++)
            {
                TargetConfig target = guild.Targets[index];
                items.Add(new TargetListItem(index, string.IsNullOrWhiteSpace(target.Label) ? "未备注" : target.Label, target.UserId));
            }
        }
        TargetList.ItemsSource = items;
        TargetCountText.Text = $"共 {items.Count} 个用户";
        ServerSummaryText.Text = SelectedServer is null ? "尚无服务器" : $"公共 Reaction：{guild?.CommonReactions.Count ?? 0} 个";
        ResetEditor();
    }

    private void TargetList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TargetList.SelectedItem is not TargetListItem item || CurrentGuild is not GuildConfig guild || item.Index >= guild.Targets.Count)
        {
            return;
        }
        LoadTarget(item.Index, guild.Targets[item.Index]);
    }

    private void LoadTarget(int index, TargetConfig target)
    {
        _loading = true;
        _editingIndex = index;
        UserIdText.Text = target.UserId;
        UserLabelText.Text = target.Label;
        DedicatedReactionText.Text = string.Join(Environment.NewLine, target.Reactions);
        ReactionPreview.Render(DedicatedPreviewRows, DedicatedReactionText.Text);
        _selectedChannelIds.Clear();
        target.ChannelIds.ForEach(id => _selectedChannelIds.Add(id));
        UpdateTargetChannelSummary();
        RandomSwitch.IsOn = target.RandomEnabled;
        NoReactionSwitch.IsOn = target.NoReactionEnabled;
        NoReactionProbabilityBox.Value = target.NoReactionProbability;
        NoReactionProbabilityBox.IsEnabled = target.NoReactionEnabled;
        EditingStateText.Text = $"当前：{(string.IsNullOrWhiteSpace(target.Label) ? target.UserId : target.Label)}";
        BuildProbabilityRows(target.ReactionWeights);
        _loading = false;
    }

    private void NewTarget_Click(object sender, RoutedEventArgs e) => ResetEditor();
    private void ClearEditor_Click(object sender, RoutedEventArgs e) => ResetEditor();

    private void ResetEditor()
    {
        _loading = true;
        _editingIndex = -1;
        TargetList.SelectedItem = null;
        UserIdText.Text = "";
        UserLabelText.Text = "";
        DedicatedReactionText.Text = "";
        ReactionPreview.Render(DedicatedPreviewRows, "");
        _selectedChannelIds.Clear();
        UpdateTargetChannelSummary();
        RandomSwitch.IsOn = false;
        NoReactionSwitch.IsOn = false;
        NoReactionProbabilityBox.Value = 0;
        NoReactionProbabilityBox.IsEnabled = false;
        EditingStateText.Text = "新用户";
        BuildProbabilityRows(new Dictionary<string, double>());
        _loading = false;
    }

    private void ReactionText_TextChanged(object sender, TextChangedEventArgs e)
    {
        ReactionPreview.Render(DedicatedPreviewRows, DedicatedReactionText.Text);
        if (!_loading)
        {
            Dictionary<string, double> current = _weightInputs.ToDictionary(pair => pair.Key, pair => pair.Value.Value);
            BuildProbabilityRows(current);
        }
    }

    private async void ChooseTargetChannels_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedServer is not GuildOption option)
        {
            ShowMessage("请先选择服务器。", InfoBarSeverity.Warning);
            return;
        }
        ChannelSelectionResult? result = await ChannelSelectionDialog.ShowAsync(
            XamlRoot,
            "选择专属 Reaction 生效频道",
            AppServices.ChannelOptions(option.Id),
            _selectedChannelIds);
        if (result is null) return;
        _selectedChannelIds.Clear();
        result.ChannelIds.ForEach(id => _selectedChannelIds.Add(id));
        UpdateTargetChannelSummary();
    }

    private void UpdateTargetChannelSummary()
    {
        if (SelectedServer is not GuildOption option)
        {
            TargetChannelSummaryText.Text = "尚无服务器";
            return;
        }
        TargetChannelSummaryText.Text = ChannelSelectionDialog.Summary(
            _selectedChannelIds,
            AppServices.ChannelOptions(option.Id));
    }

    private void RandomSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_loading)
        {
            BuildProbabilityRows(_weightInputs.ToDictionary(pair => pair.Key, pair => pair.Value.Value));
        }
    }

    private void NoReactionSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        NoReactionProbabilityBox.IsEnabled = NoReactionSwitch.IsOn;
    }

    private List<(string Token, string Source)> ReactionPool()
    {
        List<(string Token, string Source)> result = [];
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (string token in AppServices.ParseReactions(DedicatedReactionText.Text))
        {
            if (seen.Add(token)) result.Add((token, "专属"));
        }
        return result;
    }

    private void BuildProbabilityRows(IReadOnlyDictionary<string, double> weights)
    {
        List<(string Token, string Source)> pool = ReactionPool();
        ProbabilityRows.Children.Clear();
        _weightInputs.Clear();
        double defaultWeight = pool.Count == 0 ? 0 : 100.0 / pool.Count;

        foreach ((string token, string source) in pool)
        {
            Grid row = new() { ColumnSpacing = 12, Padding = new Thickness(12, 8, 12, 8), Background = (Brush)Application.Current.Resources["PanelBrush"], CornerRadius = new CornerRadius(5) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
            TextBlock tokenText = new() { Text = token, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
            TextBlock sourceText = new() { Text = source, VerticalAlignment = VerticalAlignment.Center, Foreground = (Brush)Application.Current.Resources["SecondaryTextBrush"] };
            NumberBox number = new() { Minimum = 0, Maximum = 100, SmallChange = 0.1, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact, Value = weights.TryGetValue(token, out double saved) ? saved : defaultWeight, IsEnabled = RandomSwitch.IsOn };
            number.ValueChanged += (_, _) => UpdateProbabilityTotal();
            Grid.SetColumn(sourceText, 1);
            Grid.SetColumn(number, 2);
            row.Children.Add(tokenText);
            row.Children.Add(sourceText);
            row.Children.Add(number);
            ProbabilityRows.Children.Add(row);
            _weightInputs[token] = number;
        }
        UpdateProbabilityTotal();
    }

    private void EqualWeights_Click(object sender, RoutedEventArgs e)
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
        ProbabilityTotalText.Text = $"总计 {total:0.##}%";
        ProbabilityTotalText.Foreground = new SolidColorBrush(Math.Abs(total - 100) < 0.005
            ? Color.FromArgb(255, 54, 230, 138)
            : Color.FromArgb(255, 255, 107, 107));
    }

    private void SaveTarget_Click(object sender, RoutedEventArgs e)
    {
        if (CurrentGuild is not GuildConfig guild)
        {
            ShowMessage("请先选择服务器。", InfoBarSeverity.Warning);
            return;
        }
        string userId = UserIdText.Text.Trim();
        if (!ulong.TryParse(userId, out ulong numericId) || numericId == 0)
        {
            ShowMessage("用户 ID 必须是有效的 Discord 数字 ID。", InfoBarSeverity.Error);
            return;
        }

        int duplicateIndex = guild.Targets.FindIndex(target => target.UserId == userId);
        if (duplicateIndex >= 0 && duplicateIndex != _editingIndex)
        {
            ShowMessage("该用户 ID 已经存在。", InfoBarSeverity.Error);
            return;
        }

        List<string> dedicated = AppServices.ParseReactions(DedicatedReactionText.Text);
        Dictionary<string, double> weights = _weightInputs.ToDictionary(pair => pair.Key, pair => Math.Round(pair.Value.Value, 4));
        if (RandomSwitch.IsOn)
        {
            if (weights.Count == 0)
            {
                ShowMessage("随机模式至少需要一个专属 Reaction。", InfoBarSeverity.Error);
                return;
            }
            if (Math.Abs(weights.Values.Sum() - 100) >= 0.005)
            {
                ShowMessage("随机概率合计必须精确为 100%。", InfoBarSeverity.Error);
                return;
            }
        }
        double noReactionProbability = NoReactionProbabilityBox.Value;
        if (double.IsNaN(noReactionProbability) || noReactionProbability < 0 || noReactionProbability > 100)
        {
            ShowMessage("不进行 Reaction 的概率必须在 0% 到 100% 之间。", InfoBarSeverity.Error);
            return;
        }

        TargetConfig target = new()
        {
            UserId = userId,
            Label = UserLabelText.Text.Trim(),
            Reactions = dedicated,
            RandomEnabled = RandomSwitch.IsOn,
            ReactionWeights = weights,
            ChannelIds = _selectedChannelIds.ToList(),
            NoReactionEnabled = NoReactionSwitch.IsOn,
            NoReactionProbability = Math.Round(noReactionProbability, 4),
        };
        if (_editingIndex >= 0 && _editingIndex < guild.Targets.Count) guild.Targets[_editingIndex] = target;
        else guild.Targets.Add(target);
        AppServices.SaveConfig();
        LoadCurrentGuild();
        ShowMessage("目标用户配置已保存，Bot 会自动重新载入。", InfoBarSeverity.Success);
    }

    private async void DeleteTarget_Click(object sender, RoutedEventArgs e)
    {
        if (TargetList.SelectedItem is not TargetListItem item || CurrentGuild is not GuildConfig guild || item.Index >= guild.Targets.Count)
        {
            ShowMessage("请先选择要删除的用户。", InfoBarSeverity.Warning);
            return;
        }
        ContentDialog dialog = new() { Title = "删除目标用户？", Content = $"将删除 {item.Label}（{item.UserId}）的配置。", PrimaryButtonText = "删除", CloseButtonText = "取消", DefaultButton = ContentDialogButton.Close, XamlRoot = XamlRoot };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        guild.Targets.RemoveAt(item.Index);
        AppServices.SaveConfig();
        LoadCurrentGuild();
        ShowMessage("用户已删除。", InfoBarSeverity.Success);
    }

    private void ShowMessage(string message, InfoBarSeverity severity)
    {
        MessageBar.Message = message;
        MessageBar.Severity = severity;
        MessageBar.IsOpen = true;
    }
}
