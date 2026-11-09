using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace DailyTodo
{
    internal sealed class RepeatPicker : StackPanel
    {
        private const int Custom = 3;
        private static readonly string[] WeekdayNames = { "周一", "周二", "周三", "周四", "周五", "周六", "周日" };
        private static readonly Brush Muted = new SolidColorBrush(Color.FromRgb(82, 104, 93));
        private readonly ComboBox options;
        private readonly CheckBox[] weekdays;
        private readonly Border weekdayRow;

        public event Action Changed;
        public bool IsRepeating { get { return options.SelectedIndex > 0; } }
        public bool IsValid { get { return options.SelectedIndex != Custom || Days != 0; } }

        public int Days
        {
            get
            {
                switch (options.SelectedIndex)
                {
                    case 1: return TodoTask.EveryDay;
                    case 2: return TodoTask.Weekdays;
                    case Custom:
                        int days = 0;
                        for (int index = 0; index < weekdays.Length; index++)
                            if (weekdays[index].IsChecked == true) days |= 1 << index;
                        return days;
                    default: return 0;
                }
            }
        }

        public RepeatPicker(int initialDays, FrameworkElement trailing = null)
        {
            options = new ComboBox
            {
                ItemsSource = new[] { "不重复", "每天", "工作日", "每周指定" },
                SelectedIndex = initialDays == 0 ? 0 : initialDays == TodoTask.EveryDay ? 1 :
                    initialDays == TodoTask.Weekdays ? 2 : Custom,
                Width = 104, Height = 28, FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Left, VerticalContentAlignment = VerticalAlignment.Center
            };

            var header = new DockPanel();
            if (trailing != null)
            {
                DockPanel.SetDock(trailing, Dock.Right);
                header.Children.Add(trailing);
            }
            var label = new Label
            {
                Content = "重复", Target = options, FontSize = 12, Foreground = Muted,
                VerticalAlignment = VerticalAlignment.Center, Padding = new Thickness(0, 0, 5, 0)
            };
            DockPanel.SetDock(label, Dock.Left);
            header.Children.Add(label);
            header.Children.Add(options);
            Children.Add(header);

            var week = new UniformGrid { Columns = 7, Rows = 1 };
            weekdays = new CheckBox[WeekdayNames.Length];
            for (int index = 0; index < weekdays.Length; index++)
            {
                var day = new CheckBox
                {
                    Content = WeekdayNames[index], IsChecked = (initialDays & (1 << index)) != 0,
                    FontSize = 12, VerticalContentAlignment = VerticalAlignment.Center
                };
                day.Checked += delegate { NotifyChanged(); };
                day.Unchecked += delegate { NotifyChanged(); };
                weekdays[index] = day;
                week.Children.Add(day);
            }
            weekdayRow = new Border
            {
                Child = week, CornerRadius = new CornerRadius(7), Margin = new Thickness(0, 6, 0, 0),
                Padding = new Thickness(8, 7, 8, 7),
                Background = new SolidColorBrush(Color.FromRgb(239, 245, 240))
            };
            Children.Add(weekdayRow);
            options.SelectionChanged += delegate
            {
                UpdateWeekdayVisibility();
                NotifyChanged();
            };
            UpdateWeekdayVisibility();
        }

        public void Reset()
        {
            options.SelectedIndex = 0;
            foreach (var day in weekdays) day.IsChecked = false;
        }

        public static string Describe(int repeatDays)
        {
            if (repeatDays == TodoTask.EveryDay) return "每天重复";
            if (repeatDays == TodoTask.Weekdays) return "工作日重复";
            return "每" + string.Join("、", WeekdayNames.Where((name, index) => (repeatDays & (1 << index)) != 0));
        }

        private void UpdateWeekdayVisibility()
        {
            weekdayRow.Visibility = options.SelectedIndex == Custom ? Visibility.Visible : Visibility.Collapsed;
        }

        private void NotifyChanged()
        {
            var changed = Changed;
            if (changed != null) changed();
        }
    }
}
