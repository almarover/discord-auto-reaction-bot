using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace DiscordReactionManager_WinUI;

public static class ReactionPreview
{
    public static void Render(StackPanel host, string sourceText)
    {
        host.Children.Clear();
        List<string> tokens = AppServices.ParseReactions(sourceText);
        if (tokens.Count == 0)
        {
            host.Children.Add(new TextBlock
            {
                Text = "输入 Reaction 后会在这里逐项预览。",
                Foreground = (Brush)Application.Current.Resources["SecondaryTextBrush"],
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(4, 8, 4, 8),
            });
            return;
        }

        foreach (string token in tokens)
        {
            Grid row = new()
            {
                ColumnSpacing = 12,
                Padding = new Thickness(10, 8, 10, 8),
                Background = (Brush)Application.Current.Resources["PanelBrush"],
                CornerRadius = new CornerRadius(5),
            };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Border previewBox = new()
            {
                Width = 38,
                Height = 38,
                CornerRadius = new CornerRadius(5),
                Background = (Brush)Application.Current.Resources["CardBrush"],
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            if (ulong.TryParse(token, out ulong emojiId) && emojiId > 0)
            {
                Image image = new()
                {
                    Width = 30,
                    Height = 30,
                    Stretch = Stretch.Uniform,
                    Source = new BitmapImage(new Uri($"https://cdn.discordapp.com/emojis/{emojiId}.png?size=64&quality=lossless")),
                };
                previewBox.Child = image;
            }
            else
            {
                previewBox.Child = new TextBlock
                {
                    Text = token,
                    FontSize = token.StartsWith(':') ? 12 : 24,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                };
            }

            StackPanel description = new() { VerticalAlignment = VerticalAlignment.Center };
            description.Children.Add(new TextBlock
            {
                Text = token,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
            description.Children.Add(new TextBlock
            {
                Text = emojiId > 0 ? "Custom Emoji ID" : token.StartsWith(':') ? "Emoji 名称" : "普通 Emoji",
                FontSize = 12,
                Foreground = (Brush)Application.Current.Resources["SecondaryTextBrush"],
            });
            Grid.SetColumn(description, 1);
            row.Children.Add(previewBox);
            row.Children.Add(description);
            host.Children.Add(row);
        }
    }
}
