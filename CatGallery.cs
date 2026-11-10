using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace DailyTodo
{
    internal sealed class CatGallery : Window
    {
        public CatGallery(Window owner, CatCollection collection)
        {
            Owner = owner;
            Title = "伙伴小屋";
            Width = 430;
            SizeToContent = SizeToContent.Height;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
            FontFamily = owner.FontFamily;
            FontSize = owner.FontSize;
            Background = owner.Background;
            Foreground = owner.Foreground;

            var content = new StackPanel { Margin = new Thickness(18, 14, 18, 16) };
            content.Children.Add(new TextBlock
            {
                Text = "伙伴小屋", FontSize = 22, FontWeight = FontWeights.Bold
            });
            content.Children.Add(new TextBlock
            {
                Text = "已收集 " + collection.UnlockedCount + " / " + collection.Catalog.Count + " 只",
                FontSize = 13, Margin = new Thickness(0, 4, 0, 3)
            });
            var status = new TextBlock
            {
                Text = collection.HasStorageError ? "暂时无法保存或读取伙伴记录。" :
                    collection.NewCats != null ? "新伙伴 · " + collection.NewCats :
                    "连续 " + CatCollection.DaysPerCat + " 天每天完成至少 1 项待办，即可随机解锁一位伙伴。",
                TextWrapping = TextWrapping.Wrap, FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(83, 103, 76)),
                Margin = new Thickness(0, 0, 0, 9)
            };
            content.Children.Add(status);

            var cards = new UniformGrid { Columns = 4 };
            foreach (var skin in collection.Catalog)
            {
                bool unlocked = collection.IsUnlocked(skin);
                bool selected = collection.Selected == skin;
                var face = new Image
                {
                    Source = skin.Idle, Width = 65, Height = 50
                };
                DeskPet.SetScaling(face, skin);
                var preview = new StackPanel();
                preview.Children.Add(face);
                preview.Children.Add(new TextBlock
                {
                    Text = skin.Name, HorizontalAlignment = HorizontalAlignment.Center,
                    FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 4, 0, 1)
                });
                preview.Children.Add(new TextBlock
                {
                    Text = selected ? "正在陪伴" : unlocked ? "点击切换" : "待解锁",
                    HorizontalAlignment = HorizontalAlignment.Center, FontSize = 10
                });
                var card = new Button
                {
                    Content = preview, Style = (Style)owner.Resources["ActionButton"], IsEnabled = unlocked,
                    Background = selected ? new SolidColorBrush(Color.FromRgb(218, 234, 197)) :
                        new SolidColorBrush(Color.FromRgb(255, 253, 245)),
                    Margin = new Thickness(3), Padding = new Thickness(3, 7, 3, 7)
                };
                card.Click += delegate
                {
                    if (collection.Select(skin)) DialogResult = true;
                    else status.Text = "暂时无法保存选择，请稍后重试。";
                };
                cards.Children.Add(card);
            }
            content.Children.Add(new ScrollViewer
            {
                Content = cards, MaxHeight = 330, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            });

            content.Children.Add(new Button
            {
                Content = "关闭", IsCancel = true, HorizontalAlignment = HorizontalAlignment.Right,
                Style = (Style)owner.Resources["ActionButton"], Margin = new Thickness(0, 8, 0, 0)
            });
            Content = content;
        }
    }
}
