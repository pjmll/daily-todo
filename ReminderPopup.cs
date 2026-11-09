using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DailyTodo
{
    internal sealed class ReminderPopup : Window
    {
        private readonly TodoBoard board;
        private readonly StackPanel rows;
        private readonly Dictionary<Guid, Border> displayed = new Dictionary<Guid, Border>();

        public ReminderPopup(TodoBoard board)
        {
            this.board = board;
            Title = "每日待办提醒";
            Width = 390;
            MaxHeight = 420;
            SizeToContent = SizeToContent.Height;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            ShowActivated = false;
            Topmost = true;
            Background = board.Window.Background;
            Foreground = board.Window.Foreground;
            FontFamily = board.Window.FontFamily;
            FontSize = board.Window.FontSize;
            Icon = board.Window.Icon;

            var content = new StackPanel { Margin = new Thickness(14) };
            content.Children.Add(new TextBlock
            {
                Text = "到提醒时间了", FontSize = 19, FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 9)
            });
            rows = new StackPanel();
            content.Children.Add(new ScrollViewer
            {
                Content = rows, MaxHeight = 300, VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            });
            Content = content;
            SizeChanged += delegate { Position(); };
        }

        public void AddReminders(IList<TodoTask> tasks)
        {
            foreach (var task in tasks)
            {
                Border old;
                if (displayed.TryGetValue(task.Id, out old)) rows.Children.Remove(old);
                var row = CreateRow(task);
                displayed[task.Id] = row;
                rows.Children.Add(row);
            }
            Position();
            if (!IsVisible) Show();
        }

        private Border CreateRow(TodoTask task)
        {
            var body = new StackPanel();
            body.Children.Add(new TextBlock
            {
                Text = task.Title, TextWrapping = TextWrapping.Wrap, FontWeight = FontWeights.SemiBold
            });
            var actions = new StackPanel
            {
                Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 7, 0, 0)
            };
            var snooze = new Button
            {
                Content = "10 分钟后提醒", Style = (Style)board.Window.Resources["ActionButton"],
                FontSize = 12, Padding = new Thickness(8, 5, 8, 5)
            };
            var complete = new Button
            {
                Content = "标记完成", Style = (Style)board.Window.Resources["PrimaryButton"],
                FontSize = 12, Padding = new Thickness(8, 5, 8, 5), Margin = new Thickness(6, 0, 0, 0)
            };
            snooze.Click += delegate { if (board.SnoozeReminder(task, this)) Dismiss(task.Id); };
            complete.Click += delegate { if (board.CompleteReminder(task, this)) Dismiss(task.Id); };
            actions.Children.Add(snooze);
            actions.Children.Add(complete);
            body.Children.Add(actions);
            return new Border
            {
                Child = body, Background = Brushes.White, CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10), Margin = new Thickness(0, 0, 0, 6)
            };
        }

        private void Dismiss(Guid id)
        {
            rows.Children.Remove(displayed[id]);
            displayed.Remove(id);
            if (displayed.Count == 0) Close();
        }

        private void Position()
        {
            var area = SystemParameters.WorkArea;
            Left = Math.Max(area.Left, area.Right - Width - 12);
            Top = Math.Max(area.Top, area.Bottom - Math.Max(ActualHeight, 100) - 12);
        }
    }
}
