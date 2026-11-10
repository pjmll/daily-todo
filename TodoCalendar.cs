using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace DailyTodo
{
    internal sealed class TodoCalendar
    {
        private static readonly CultureInfo Chinese = CultureInfo.GetCultureInfo("zh-CN");
        private static readonly Brush DayBackground = new SolidColorBrush(Color.FromRgb(246, 242, 224));
        private static readonly Brush CompletedBackground = new SolidColorBrush(Color.FromRgb(226, 239, 211));
        private static readonly Brush SelectedBackground = new SolidColorBrush(Color.FromRgb(210, 235, 238));
        private static readonly Brush Accent = new SolidColorBrush(Color.FromRgb(60, 96, 65));
        private static readonly Brush TodayOutline = new SolidColorBrush(Color.FromRgb(143, 171, 123));
        private static readonly Brush Muted = new SolidColorBrush(Color.FromRgb(83, 103, 76));

        private readonly UniformGrid days;
        private readonly TextBlock monthTitle;
        private readonly TextBlock monthTotal;
        private readonly TextBlock dayTitle;
        private readonly TextBlock daySummary;
        private readonly StackPanel records;
        private readonly Button previous;
        private readonly Button next;
        private readonly Style buttonStyle;
        private DateTime today = DateTime.Today;
        private DateTime selected = DateTime.Today;
        private ILookup<DateTime, TodoTask> completed;
        private IList<TodoTask> pendingToday;

        public TodoCalendar(Window window)
        {
            days = (UniformGrid)window.FindName("CalendarDays");
            monthTitle = (TextBlock)window.FindName("CalendarMonth");
            monthTotal = (TextBlock)window.FindName("CalendarMonthTotal");
            dayTitle = (TextBlock)window.FindName("CalendarDayTitle");
            daySummary = (TextBlock)window.FindName("CalendarDaySummary");
            records = (StackPanel)window.FindName("CalendarDayRecords");
            previous = (Button)window.FindName("CalendarPrevious");
            next = (Button)window.FindName("CalendarNext");
            buttonStyle = (Style)window.Resources["ActionButton"];

            previous.Click += delegate { SelectDate(selected.AddMonths(-1)); };
            next.Click += delegate { SelectDate(selected.AddMonths(1)); };
            ((Button)window.FindName("CalendarGoToday")).Click += delegate { SelectDate(today); };
        }

        public void Refresh(DateTime day, IList<TodoTask> tasks, IList<TodoTask> todayTasks)
        {
            if (selected == today) selected = day;
            today = day;
            pendingToday = todayTasks;
            completed = tasks.Where(task => task.Completed.HasValue).Reverse().ToLookup(task => task.Completed.Value);
            RenderMonth();
            RenderDay();
        }

        private void SelectDate(DateTime date)
        {
            bool differentMonth = selected.Month != date.Month || selected.Year != date.Year;
            selected = date;
            if (differentMonth) RenderMonth();
            else
            {
                foreach (var cell in days.Children.OfType<Button>())
                    StyleCell(cell, (DateTime)cell.Tag);
            }
            RenderDay();
        }

        private void RenderMonth()
        {
            var first = new DateTime(selected.Year, selected.Month, 1);
            int numberOfDays = DateTime.DaysInMonth(first.Year, first.Month);
            monthTitle.Text = first.ToString("yyyy 年 M 月", Chinese);
            previous.IsEnabled = first.Year != 1 || first.Month != 1;
            next.IsEnabled = first.Year != 9999 || first.Month != 12;
            days.Children.Clear();

            int leadingDays = ((int)first.DayOfWeek + 6) % 7;
            for (int index = 0; index < leadingDays; index++)
                days.Children.Add(new Border());

            int total = 0;
            for (int offset = 0; offset < numberOfDays; offset++)
            {
                var date = first.AddDays(offset);
                int count = completed[date].Count();
                total += count;
                days.Children.Add(CreateDay(date, count));
            }
            monthTotal.Text = "本月完成 " + total + " 项";
        }

        private Button CreateDay(DateTime date, int count)
        {
            var label = new StackPanel();
            label.Children.Add(new TextBlock
            {
                Text = date.Day.ToString(Chinese), FontSize = 13, TextAlignment = TextAlignment.Center,
                FontWeight = date == today ? FontWeights.Bold : FontWeights.Normal
            });
            label.Children.Add(new TextBlock
            {
                Text = count > 0 ? "✓ " + count : date == today ? "今天" : "",
                FontSize = 10, Foreground = Accent, TextAlignment = TextAlignment.Center
            });
            var cell = new Button
            {
                Tag = date, Content = label, Style = buttonStyle,
                Height = 39, Margin = new Thickness(2), Padding = new Thickness(2),
                ToolTip = date.ToString("yyyy年M月d日", Chinese) + " · 完成 " + count + " 项"
            };
            AutomationProperties.SetName(cell, (string)cell.ToolTip);
            StyleCell(cell, date);
            cell.Click += delegate
            {
                SelectDate(date);
                daySummary.BringIntoView();
            };
            return cell;
        }

        private void StyleCell(Button cell, DateTime date)
        {
            bool active = date == selected;
            cell.Background = active ? SelectedBackground : completed.Contains(date) ? CompletedBackground : DayBackground;
            cell.BorderBrush = active ? Accent : date == today ? TodayOutline : Brushes.Transparent;
            AutomationProperties.SetItemStatus(cell, active ? "已选中" : "");
        }

        private void RenderDay()
        {
            var done = completed[selected];
            int count = done.Count();
            bool isToday = selected == today;
            dayTitle.Text = selected.ToString("M月d日  dddd", Chinese);
            daySummary.Text = isToday ? "已完成 " + count + " 项  ·  待完成 " + pendingToday.Count + " 项" :
                count == 0 ? "当天没有完成记录" : "已完成 " + count + " 项";

            records.Children.Clear();
            foreach (var task in done) AddRecord(task, true);
            if (isToday)
            {
                foreach (var task in pendingToday) AddRecord(task, false);
            }
        }

        private void AddRecord(TodoTask task, bool done)
        {
            var row = new TextBlock
            {
                Text = (done ? "✓  " : "○  ") + task.Title,
                TextWrapping = TextWrapping.Wrap, FontSize = 13,
                Margin = new Thickness(0, 5, 0, 0), Foreground = done ? Accent : Muted
            };
            AutomationProperties.SetName(row, (done ? "已完成 " : "待完成 ") + task.Title);
            records.Children.Add(row);
        }
    }
}
