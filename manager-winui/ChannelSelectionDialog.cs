using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace DiscordReactionManager_WinUI;

public sealed record ChannelSelectionResult(bool AllChannels, List<string> ChannelIds);

public static class ChannelSelectionDialog
{
    public static async Task<ChannelSelectionResult?> ShowAsync(
        XamlRoot xamlRoot,
        string title,
        IReadOnlyList<ChannelOption> channels,
        IReadOnlyCollection<string> selectedChannelIds)
    {
        HashSet<string> selected = selectedChannelIds.ToHashSet(StringComparer.Ordinal);
        CheckBox allChannels = new()
        {
            Content = "全部可访问文字频道",
            IsChecked = selected.Count == 0,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        };
        TextBlock validation = new()
        {
            Text = "请至少选择一个频道，或者启用“全部可访问文字频道”。",
            Foreground = (Brush)Application.Current.Resources["DangerBrush"],
            TextWrapping = TextWrapping.Wrap,
            Visibility = Visibility.Collapsed,
        };
        StackPanel channelRows = new() { Spacing = 5 };
        List<CheckBox> boxes = [];
        foreach (ChannelOption channel in channels)
        {
            CheckBox box = new()
            {
                Content = $"#{channel.Name}",
                Tag = channel.Id,
                IsChecked = selected.Contains(channel.Id),
                IsEnabled = allChannels.IsChecked != true,
            };
            boxes.Add(box);
            channelRows.Children.Add(box);
        }

        void UpdateEnabledState()
        {
            bool enabled = allChannels.IsChecked != true;
            boxes.ForEach(box => box.IsEnabled = enabled);
            validation.Visibility = Visibility.Collapsed;
        }
        allChannels.Checked += (_, _) => UpdateEnabledState();
        allChannels.Unchecked += (_, _) => UpdateEnabledState();

        StackPanel content = new() { Spacing = 12, MinWidth = 420 };
        content.Children.Add(new TextBlock
        {
            Text = channels.Count == 0
                ? "尚未同步到文字频道。请先启动 Bot，等待连接成功后再打开此窗口。"
                : "只在勾选的频道中执行本组 Reaction。",
            Foreground = (Brush)Application.Current.Resources["SecondaryTextBrush"],
            TextWrapping = TextWrapping.Wrap,
        });
        content.Children.Add(allChannels);
        content.Children.Add(validation);
        content.Children.Add(channelRows);

        ContentDialog dialog = new()
        {
            Title = title,
            Content = new ScrollViewer
            {
                Content = content,
                MaxHeight = 460,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            },
            PrimaryButtonText = "应用",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = xamlRoot,
        };
        dialog.PrimaryButtonClick += (_, args) =>
        {
            if (allChannels.IsChecked != true && boxes.All(box => box.IsChecked != true))
            {
                validation.Visibility = Visibility.Visible;
                args.Cancel = true;
            }
        };

        ContentDialogResult result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            return null;
        }
        bool useAllChannels = allChannels.IsChecked == true;
        return new ChannelSelectionResult(
            useAllChannels,
            useAllChannels
                ? []
                : boxes.Where(box => box.IsChecked == true).Select(box => (string)box.Tag).ToList());
    }

    public static string Summary(
        IReadOnlyCollection<string> channelIds,
        IReadOnlyList<ChannelOption> channels)
    {
        if (channelIds.Count == 0)
        {
            return "全部可访问文字频道";
        }
        Dictionary<string, string> names = channels.ToDictionary(channel => channel.Id, channel => channel.Name);
        List<string> labels = channelIds
            .Select(id => names.TryGetValue(id, out string? name) ? $"#{name}" : $"频道 {id}")
            .ToList();
        return labels.Count <= 3
            ? string.Join("、", labels)
            : $"{string.Join("、", labels.Take(3))} 等 {labels.Count} 个频道";
    }
}
