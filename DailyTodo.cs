using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xml;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace DailyTodo
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            bool startInTray = args.Contains("--tray");
            bool firstInstance;
            using (var instance = new Mutex(true, @"Local\DailyTodo.A7A12984", out firstInstance))
            {
                if (!firstInstance)
                {
                    if (!startInTray) DesktopTray.OpenExisting();
                    return;
                }

                var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DailyTodo");
                var store = new TaskStore(Path.Combine(folder, "tasks.json"));
                List<TodoTask> tasks;
                try
                {
                    tasks = store.Load();
                }
                catch (Exception error)
                {
                    if (!TaskStore.IsStorageError(error)) throw;
                    MessageBox.Show("无法读取待办记录。请检查文件后重新打开。\n\n" + store.FilePath,
                        "每日待办", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var application = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
                var board = new TodoBoard(store, tasks);
                using (var tray = new DesktopTray(application, board))
                {
                    if (startInTray)
                    {
                        application.MainWindow = board.Window;
                        new WindowInteropHelper(board.Window).EnsureHandle();
                        application.Run();
                    }
                    else application.Run(board.Window);
                }
            }
        }
    }

    internal sealed class DesktopTray : IDisposable
    {
        private const int OpenWindowMessage = 0x8049;
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private readonly Window window;
        private readonly TodoBoard board;
        private readonly System.Drawing.Icon icon;
        private readonly Forms.ContextMenuStrip menu;
        private readonly Forms.NotifyIcon tray;
        private ReminderPopup popup;
        private bool exiting;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindow(string className, string title);
        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
        [DllImport("user32.dll")]
        private static extern bool AllowSetForegroundWindow(uint processId);
        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);

        public static void OpenExisting()
        {
            var handle = FindWindow(null, "每日待办");
            uint processId;
            if (handle == IntPtr.Zero || GetWindowThreadProcessId(handle, out processId) == 0) return;
            AllowSetForegroundWindow(processId);
            PostMessage(handle, OpenWindowMessage, IntPtr.Zero, IntPtr.Zero);
        }

        public DesktopTray(Application application, TodoBoard board)
        {
            this.board = board;
            window = board.Window;
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("DailyTodo.ico"))
                icon = new System.Drawing.Icon(stream, 32, 32);
            menu = new Forms.ContextMenuStrip();
            menu.Items.Add("打开每日待办", null, delegate { OpenWindow(); });
            var startup = new Forms.ToolStripMenuItem("登录 Windows 后在托盘运行");
            menu.Items.Add(startup);
            menu.Opening += delegate
            {
                try
                {
                    using (var key = Registry.CurrentUser.OpenSubKey(RunKey))
                        startup.Checked = key != null && key.GetValue("DailyTodo") is string;
                    startup.Enabled = true;
                }
                catch (Exception error)
                {
                    if (!IsRegistryError(error)) throw;
                    startup.Enabled = false;
                }
            };
            startup.Click += delegate
            {
                try
                {
                    using (var key = Registry.CurrentUser.CreateSubKey(RunKey))
                    {
                        if (startup.Checked) key.DeleteValue("DailyTodo", false);
                        else key.SetValue("DailyTodo", "\"" + Assembly.GetExecutingAssembly().Location + "\" --tray");
                    }
                }
                catch (Exception error)
                {
                    if (!IsRegistryError(error)) throw;
                    MessageBox.Show("无法更改开机启动设置。", "每日待办", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
            menu.Items.Add("退出", null, delegate
            {
                exiting = true;
                application.Shutdown();
            });
            tray = new Forms.NotifyIcon
            {
                Icon = icon, Text = "每日待办", ContextMenuStrip = menu, Visible = true
            };
            tray.MouseDoubleClick += (sender, args) =>
            {
                if (args.Button == Forms.MouseButtons.Left) OpenWindow();
            };
            board.RemindersDue += ShowReminders;
            application.SessionEnding += delegate { exiting = true; };
            window.SourceInitialized += delegate
            {
                var source = HwndSource.FromHwnd(new WindowInteropHelper(window).Handle);
                source.AddHook(ReceiveWindowMessage);
            };
            window.Closing += (sender, args) =>
            {
                if (exiting || !board.HasActiveReminders) return;
                args.Cancel = true;
                window.Hide();
            };
        }

        private IntPtr ReceiveWindowMessage(IntPtr handle, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (message == OpenWindowMessage)
            {
                window.Dispatcher.BeginInvoke(new Action(OpenWindow));
                handled = true;
            }
            return IntPtr.Zero;
        }

        private void OpenWindow()
        {
            if (exiting) return;
            if (!window.IsVisible) window.Show();
            if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;
            window.Activate();
        }

        private void ShowReminders(IList<TodoTask> tasks)
        {
            if (exiting) return;
            if (popup == null)
            {
                popup = new ReminderPopup(board);
                popup.Closed += delegate { popup = null; };
            }
            popup.AddReminders(tasks);
        }

        private static bool IsRegistryError(Exception error)
        {
            return error is IOException || error is UnauthorizedAccessException ||
                   error is System.Security.SecurityException;
        }

        public void Dispose()
        {
            tray.Dispose();
            menu.Dispose();
            icon.Dispose();
        }
    }

    [DataContract]
    internal sealed class TodoTask
    {
        [DataMember(IsRequired = true)] public Guid Id { get; set; }
        [DataMember(IsRequired = true)] public string Title { get; set; }
        [DataMember(IsRequired = true)] public DateTime Due { get; set; }
        [DataMember(EmitDefaultValue = false)] public DateTime? Completed { get; set; }
        [DataMember(EmitDefaultValue = false)] public bool RepeatsDaily { get; set; }
        [DataMember(EmitDefaultValue = false)] public int RepeatWeekdays { get; set; }
        [DataMember(EmitDefaultValue = false)] public Guid? SeriesId { get; set; }
        [DataMember(EmitDefaultValue = false)] public int? ReminderMinute { get; set; }
        [DataMember(EmitDefaultValue = false)] public DateTime? LastReminderDate { get; set; }
        [DataMember(EmitDefaultValue = false)] public long? SnoozedUntilUtcTicks { get; set; }

        public const int EveryDay = 127;
        public const int Weekdays = 31;
        public int RepeatDays
        {
            get { return RepeatsDaily ? EveryDay : RepeatWeekdays; }
            set
            {
                RepeatsDaily = value == EveryDay;
                RepeatWeekdays = RepeatsDaily ? 0 : value;
            }
        }
        public bool IsRecurring { get { return RepeatDays != 0; } }

        public bool OccursOn(DateTime day)
        {
            int weekday = ((int)day.DayOfWeek + 6) % 7;
            return day >= Due && (RepeatDays & (1 << weekday)) != 0;
        }

        public TodoTask WithReminder(DateTime? day, long? snoozedUntil = null)
        {
            var updated = (TodoTask)MemberwiseClone();
            updated.LastReminderDate = day;
            updated.SnoozedUntilUtcTicks = snoozedUntil;
            return updated;
        }
    }

    internal sealed class TaskStore
    {
        public string FilePath { get; private set; }
        private readonly DataContractJsonSerializer serializer = new DataContractJsonSerializer(
            typeof(List<TodoTask>), new DataContractJsonSerializerSettings
            {
                DateTimeFormat = new DateTimeFormat("yyyy-MM-dd", CultureInfo.InvariantCulture)
            });

        public TaskStore(string filePath)
        {
            FilePath = filePath;
        }

        public List<TodoTask> Load()
        {
            try
            {
                using (var stream = File.OpenRead(FilePath))
                using (var reader = JsonReaderWriterFactory.CreateJsonReader(stream, XmlDictionaryReaderQuotas.Max))
                {
                    reader.MoveToContent();
                    if (reader.GetAttribute("type") != "array")
                        throw new InvalidDataException("Expected a JSON array.");
                    var tasks = (List<TodoTask>)serializer.ReadObject(reader);
                    var ids = new HashSet<Guid>();
                    if (reader.MoveToContent() != XmlNodeType.None || tasks == null ||
                        tasks.Any(task => task == null || task.Id == Guid.Empty ||
                        !ids.Add(task.Id) || string.IsNullOrWhiteSpace(task.Title) ||
                        task.ReminderMinute < 0 || task.ReminderMinute >= 1440 ||
                        task.RepeatWeekdays < 0 || task.RepeatWeekdays > TodoTask.EveryDay ||
                        task.SnoozedUntilUtcTicks < 0 || task.SnoozedUntilUtcTicks > DateTime.MaxValue.Ticks))
                        throw new InvalidDataException("Invalid task list.");
                    return tasks;
                }
            }
            catch (FileNotFoundException) { return new List<TodoTask>(); }
            catch (DirectoryNotFoundException) { return new List<TodoTask>(); }
        }

        public void Save(List<TodoTask> tasks)
        {
            SaveAtomically(FilePath, serializer, tasks);
        }

        internal static void SaveAtomically(string path, DataContractJsonSerializer serializer, object value)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write))
                {
                    serializer.WriteObject(stream, value);
                    stream.Flush(true);
                }
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
            }
            finally
            {
                try { File.Delete(temporary); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        public static bool IsStorageError(Exception error)
        {
            return error is IOException || error is InvalidDataException || error is UnauthorizedAccessException ||
                   error is SerializationException || error is FormatException ||
                   error is XmlException;
        }
    }

    internal sealed class TodoBoard
    {
        private static readonly CultureInfo Chinese = CultureInfo.GetCultureInfo("zh-CN");
        private static readonly Brush Ink = new SolidColorBrush(Color.FromRgb(53, 71, 51));
        private static readonly Brush Muted = new SolidColorBrush(Color.FromRgb(83, 103, 76));
        private static readonly Brush RowBorder = new SolidColorBrush(Color.FromRgb(233, 231, 207));
        private static readonly Brush LateText = new SolidColorBrush(Color.FromRgb(150, 53, 34));
        private static readonly Brush LateBackground = new SolidColorBrush(Color.FromRgb(253, 237, 229));
        private static readonly Brush AccentText = new SolidColorBrush(Color.FromRgb(59, 90, 63));
        private static readonly Brush AccentBackground = new SolidColorBrush(Color.FromRgb(222, 237, 209));
        private static readonly Brush NeutralBackground = new SolidColorBrush(Color.FromRgb(238, 239, 218));

        private readonly TaskStore store;
        private readonly TextBox quickTitle;
        private readonly Button addButton;
        private readonly RepeatPicker repeatQuick;
        private readonly TextBlock quickHint;
        private readonly ComboBox quickWhen;
        private readonly DatePicker quickDate;
        private readonly TextBox quickReminder;
        private readonly RadioButton todayTab;
        private readonly RadioButton upcomingTab;
        private readonly RadioButton repeatTab;
        private readonly RadioButton doneTab;
        private readonly RadioButton calendarTab;
        private readonly TodoCalendar calendar;
        private readonly StackPanel taskList;
        private readonly ScrollViewer taskScroll;
        private readonly Grid progressTrack;
        private readonly Border progressFill;
        private readonly DeskPet cat;
        private readonly CatCollection cats;
        private List<TodoTask> tasks;
        private DateTime shownDay;
        private double progress;
        private bool compactLayout;

        public Window Window { get; private set; }
        public event Action<IList<TodoTask>> RemindersDue;
        public bool HasActiveReminders
        {
            get { return tasks.Any(task => !task.Completed.HasValue && task.ReminderMinute.HasValue); }
        }

        public TodoBoard(TaskStore store, List<TodoTask> tasks)
        {
            this.store = store;
            this.tasks = tasks;
            var assembly = Assembly.GetExecutingAssembly();
            using (var stream = assembly.GetManifestResourceStream("MainWindow.xaml"))
                Window = (Window)XamlReader.Load(stream);
            using (var stream = assembly.GetManifestResourceStream("DailyTodo.ico"))
                Window.Icon = BitmapFrame.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);

            quickTitle = Find<TextBox>("QuickTitle");
            addButton = Find<Button>("AddButton");
            quickHint = new TextBlock
            {
                Text = "按 Enter 快速添加", Foreground = Muted, FontSize = 12, MaxWidth = 184,
                VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap
            };
            repeatQuick = new RepeatPicker(0, quickHint);
            Find<ContentControl>("QuickRepeatHost").Content = repeatQuick;
            quickWhen = Find<ComboBox>("QuickWhen");
            quickDate = Find<DatePicker>("QuickDate");
            quickReminder = Find<TextBox>("QuickReminder");
            todayTab = Find<RadioButton>("TodayTab");
            upcomingTab = Find<RadioButton>("UpcomingTab");
            repeatTab = Find<RadioButton>("RepeatTab");
            doneTab = Find<RadioButton>("DoneTab");
            calendarTab = Find<RadioButton>("CalendarTab");
            calendar = new TodoCalendar(Window);
            taskList = Find<StackPanel>("TaskList");
            taskScroll = Find<ScrollViewer>("TaskScroll");
            progressTrack = Find<Grid>("ProgressTrack");
            progressFill = Find<Border>("ProgressFill");
            cat = new DeskPet(Find<Image>("ProgressCat"));
            cats = new CatCollection(Path.Combine(Path.GetDirectoryName(store.FilePath), "cats.json"));
            Find<Button>("CatTapButton").Click += delegate { cat.Play(false); };
            Find<Button>("CatGalleryButton").Click += delegate
            {
                new CatGallery(Window, cats).ShowDialog();
                cats.AcknowledgeNewCats();
                Refresh(DateTime.Today);
            };

            quickTitle.TextChanged += delegate
            {
                Find<TextBlock>("Placeholder").Visibility = quickTitle.Text.Length == 0
                    ? Visibility.Visible : Visibility.Collapsed;
                UpdateQuickAdd();
            };
            KeyEventHandler addOnEnter = (sender, args) =>
            {
                if (args.Key != Key.Enter) return;
                args.Handled = true;
                AddTask();
            };
            quickTitle.KeyDown += addOnEnter;
            quickReminder.KeyDown += addOnEnter;
            quickWhen.SelectionChanged += delegate
            {
                quickDate.Visibility = quickWhen.SelectedIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
                UpdateQuickAdd();
            };
            quickDate.SelectedDateChanged += delegate { UpdateQuickAdd(); };
            repeatQuick.Changed += UpdateQuickAdd;
            quickReminder.TextChanged += delegate
            {
                Find<TextBlock>("ReminderPlaceholder").Visibility = quickReminder.Text.Length == 0
                    ? Visibility.Visible : Visibility.Collapsed;
                UpdateQuickAdd();
            };
            addButton.Click += delegate { AddTask(); };
            todayTab.Checked += ChangeView;
            upcomingTab.Checked += ChangeView;
            repeatTab.Checked += ChangeView;
            doneTab.Checked += ChangeView;
            calendarTab.Checked += ChangeView;
            progressTrack.SizeChanged += delegate { UpdateProgressBar(); };

            var pin = Find<Button>("PinButton");
            pin.Click += delegate
            {
                Window.Topmost = !Window.Topmost;
                pin.Content = Window.Topmost ? "取消置顶" : "窗口置顶";
                pin.Background = Window.Topmost ? AccentBackground : Brushes.White;
                pin.ToolTip = Window.Topmost ? "恢复普通窗口层级" : "让窗口显示在其他窗口上方";
            };
            Window.InputBindings.Add(new KeyBinding(
                ApplicationCommands.New, new KeyGesture(Key.N, ModifierKeys.Control)));
            Window.CommandBindings.Add(new CommandBinding(
                ApplicationCommands.New, delegate
                {
                    if (calendarTab.IsChecked == true) todayTab.IsChecked = true;
                    quickTitle.Focus();
                }));

            var clock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            clock.Tick += delegate { CheckClock(); };
            Window.Activated += delegate { CheckClock(); };
            Window.Loaded += delegate { quickTitle.Focus(); };
            Window.SizeChanged += (sender, args) => UpdateDensity(args.NewSize.Height);
            Window.Closed += delegate { clock.Stop(); cat.Stop(); };
            Refresh(DateTime.Today);
            clock.Start();
        }

        private T Find<T>(string name) where T : FrameworkElement
        {
            return (T)Window.FindName(name);
        }

        private void ChangeView(object sender, RoutedEventArgs args)
        {
            Refresh(DateTime.Today);
            taskScroll.ScrollToTop();
            Find<ScrollViewer>("CalendarView").ScrollToTop();
        }

        private void CheckClock()
        {
            var now = DateTime.Now;
            if (now.Date != shownDay) Refresh(now.Date);
            CheckReminders(now);
        }

        private void CheckReminders(DateTime now)
        {
            var notify = RemindersDue;
            if (notify == null || !HasActiveReminders) return;

            var completedSeries = CompletedSeriesOn(now.Date);
            long utcTicks = now.ToUniversalTime().Ticks;
            var due = new List<TodoTask>();
            List<TodoTask> next = null;
            for (int index = 0; index < tasks.Count; index++)
            {
                var task = tasks[index];
                if (task.Completed.HasValue || !task.ReminderMinute.HasValue || task.Due > now.Date)
                    continue;
                DateTime reminderDate;
                if (task.SnoozedUntilUtcTicks.HasValue)
                {
                    if (task.SnoozedUntilUtcTicks.Value > utcTicks) continue;
                    reminderDate = task.LastReminderDate.GetValueOrDefault(task.Due);
                }
                else
                {
                    if (task.IsRecurring && (!task.OccursOn(now.Date) || completedSeries.Contains(task.Id)))
                        continue;
                    reminderDate = task.IsRecurring ? now.Date : task.Due;
                    if (task.LastReminderDate >= reminderDate || now < reminderDate.AddMinutes(task.ReminderMinute.Value))
                        continue;
                }

                if (next == null) next = new List<TodoTask>(tasks);
                var updated = task.WithReminder(reminderDate);
                next[index] = updated;
                due.Add(updated);
            }

            if (next != null && Commit(next, background: true)) notify(due);
        }

        private TodoTask GetCurrentReminder(TodoTask alert)
        {
            return tasks.FirstOrDefault(task => task.Id == alert.Id && !task.Completed.HasValue &&
                task.ReminderMinute.HasValue && task.ReminderMinute == alert.ReminderMinute &&
                task.Due == alert.Due && task.RepeatDays == alert.RepeatDays &&
                task.LastReminderDate.HasValue && task.LastReminderDate == alert.LastReminderDate &&
                !task.SnoozedUntilUtcTicks.HasValue &&
                (!task.IsRecurring || !HasCompletedOccurrence(task.Id, task.LastReminderDate.Value)));
        }

        public bool SnoozeReminder(TodoTask alert, Window owner)
        {
            var current = GetCurrentReminder(alert);
            if (current == null) return true;
            var next = new List<TodoTask>(tasks);
            next[next.IndexOf(current)] = current.WithReminder(current.LastReminderDate, DateTime.UtcNow.AddMinutes(10).Ticks);
            return Commit(next, owner);
        }

        public bool CompleteReminder(TodoTask alert, Window owner)
        {
            var current = GetCurrentReminder(alert);
            if (current == null) return true;
            if (current.IsRecurring) return CompleteOccurrence(current, current.LastReminderDate.Value, owner);
            if (!ReplaceTask(current, current.Title, current.Due, DateTime.Today, 0, current.ReminderMinute, owner))
                return false;
            cat.Play(true);
            return true;
        }

        private DateTime? QuickDueDate()
        {
            var today = DateTime.Today;
            if (quickWhen.SelectedIndex == 2) return quickDate.SelectedDate;
            return quickWhen.SelectedIndex == 1 ? today.AddDays(1) : today;
        }

        private void UpdateQuickAdd()
        {
            int? minute;
            bool validTime = TryReadReminder(quickReminder.Text, out minute);
            bool hasDate = QuickDueDate().HasValue;
            addButton.IsEnabled = validTime && hasDate && repeatQuick.IsValid && !string.IsNullOrWhiteSpace(quickTitle.Text);
            quickHint.Text = !validTime ? "请输入 24 小时时间，例如 09:30" :
                !hasDate ? "请在日历中选择日期" : !repeatQuick.IsValid ? "请至少勾选一天" : "按 Enter 快速添加";
            quickHint.Foreground = validTime && repeatQuick.IsValid ? Muted : LateText;
        }

        private void AddTask()
        {
            var title = quickTitle.Text.Trim();
            var due = QuickDueDate();
            int? minute;
            if (title.Length == 0 || !due.HasValue || !repeatQuick.IsValid ||
                !TryReadReminder(quickReminder.Text, out minute)) return;

            var task = new TodoTask
            {
                Id = Guid.NewGuid(), Title = title, Due = due.Value.Date, ReminderMinute = minute,
                RepeatDays = repeatQuick.Days
            };
            var next = new List<TodoTask>(tasks) { task };
            if (!Commit(next)) return;
            quickTitle.Clear();
            repeatQuick.Reset();
            quickWhen.SelectedIndex = 0;
            quickDate.SelectedDate = null;
            quickReminder.Clear();
            var today = DateTime.Today;
            if (task.Due > today) upcomingTab.IsChecked = true;
            else if (task.IsRecurring && !task.OccursOn(today)) repeatTab.IsChecked = true;
            else todayTab.IsChecked = true;
            cat.Play(false);
            quickTitle.Focus();
        }

        private bool Commit(List<TodoTask> next, Window owner = null, bool background = false)
        {
            try
            {
                store.Save(next);
            }
            catch (Exception error)
            {
                if (!TaskStore.IsStorageError(error)) throw;
                if (!background)
                    MessageBox.Show(owner ?? Window, "保存失败，请检查文件夹是否可写后重试。\n\n" + store.FilePath,
                        "每日待办", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            tasks = next;
            if (!background) Refresh(DateTime.Today);
            return true;
        }

        private bool ReplaceTask(TodoTask previous, string title, DateTime due, DateTime? completed,
            int repeatDays, int? reminderMinute, Window owner = null)
        {
            int index = tasks.FindIndex(task => task.Id == previous.Id);
            if (index < 0) return false;
            var current = tasks[index];
            bool scheduleChanged = current.ReminderMinute != reminderMinute || current.Due != due ||
                current.RepeatDays != repeatDays;
            var next = new List<TodoTask>(tasks);
            next[index] = new TodoTask
            {
                Id = current.Id, Title = title, Due = due, Completed = completed,
                RepeatDays = repeatDays, SeriesId = current.SeriesId, ReminderMinute = reminderMinute,
                LastReminderDate = scheduleChanged ? null : current.LastReminderDate,
                SnoozedUntilUtcTicks = scheduleChanged || completed.HasValue ? null : current.SnoozedUntilUtcTicks
            };
            return Commit(next, owner);
        }

        private void ToggleCompletion(TodoTask task)
        {
            if (task.SeriesId.HasValue)
            {
                RemoveTask(task);
                return;
            }

            if (!task.IsRecurring)
            {
                var completed = task.Completed.HasValue ? (DateTime?)null : DateTime.Today;
                if (ReplaceTask(task, task.Title, task.Due, completed, 0, task.ReminderMinute) && completed.HasValue)
                    cat.Play(true);
                return;
            }

            CompleteOccurrence(task, DateTime.Today);
        }

        private bool HasCompletedOccurrence(Guid id, DateTime occurrence)
        {
            return tasks.Any(entry => entry.SeriesId == id && entry.Due == occurrence && entry.Completed.HasValue);
        }

        private bool CompleteOccurrence(TodoTask task, DateTime occurrence, Window owner = null)
        {
            int index = tasks.FindIndex(entry => entry.Id == task.Id);
            if (index < 0) return true;
            var current = tasks[index];
            if (!current.OccursOn(occurrence) || HasCompletedOccurrence(current.Id, occurrence)) return true;
            var next = new List<TodoTask>(tasks)
            {
                new TodoTask
                {
                    Id = Guid.NewGuid(), Title = current.Title, Due = occurrence,
                    Completed = DateTime.Today, SeriesId = current.Id
                }
            };
            if (current.LastReminderDate == occurrence && current.SnoozedUntilUtcTicks.HasValue)
                next[index] = current.WithReminder(occurrence);
            if (!Commit(next, owner)) return false;
            cat.Play(true);
            return true;
        }

        private void RemoveTask(TodoTask task)
        {
            var next = new List<TodoTask>(tasks);
            next.RemoveAll(entry => entry.Id == task.Id);
            Commit(next);
        }

        private void UpdateProgressBar()
        {
            progressFill.Width = Math.Floor(progressTrack.ActualWidth * progress);
        }

        private void UpdateDensity(double height)
        {
            bool compact = height < 560;
            if (compact == compactLayout) return;
            compactLayout = compact;

            Find<Grid>("MainLayout").Margin = compact ? new Thickness(16, 10, 16, 11) : new Thickness(20, 16, 20, 18);
            Find<DockPanel>("Masthead").Margin = new Thickness(0, 0, 0, compact ? 8 : 12);
            var sectionSpacing = new Thickness(0, 0, 0, compact ? 7 : 11);
            Find<Border>("ViewNavigation").Margin = sectionSpacing;
            var summary = Find<Border>("TodaySummary");
            summary.Margin = sectionSpacing;
            summary.Padding = compact ? new Thickness(12, 4, 12, 6) : new Thickness(14, 9, 14, 11);
            Find<TextBlock>("ProgressTitle").Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
            Find<TextBlock>("RemainingLabel").FontSize = compact ? 22 : 28;
            var catImage = Find<Image>("ProgressCat");
            catImage.Width = compact ? 52 : 65;
            catImage.Height = compact ? 40 : 50;
            var capture = Find<Border>("QuickCapture");
            capture.Margin = sectionSpacing;
            capture.Padding = new Thickness(compact ? 9 : 12);
            quickTitle.Height = compact ? 34 : 39;
        }

        private HashSet<Guid> CompletedSeriesOn(DateTime day)
        {
            return new HashSet<Guid>(tasks.Where(task => task.Due == day && task.Completed.HasValue && task.SeriesId.HasValue)
                .Select(task => task.SeriesId.Value));
        }

        private void Refresh(DateTime day)
        {
            shownDay = day;
            var open = tasks.Where(task => !task.Completed.HasValue).OrderBy(task => task.Due).ToList();
            var completedSeries = CompletedSeriesOn(day);
            var today = open.Where(task => task.IsRecurring
                    ? task.OccursOn(day) && !completedSeries.Contains(task.Id) : task.Due <= day)
                .OrderBy(task => task.IsRecurring ? day : task.Due).ToList();
            int finished = tasks.Count(task => task.Completed == day);
            int total = today.Count + finished;
            progress = total == 0 ? 0 : (double)finished / total;
            Find<TextBlock>("DateLabel").Text = shownDay.ToString("yyyy年M月d日  dddd", Chinese);
            Find<TextBlock>("ProgressLabel").Text = total == 0 ? "今天暂无任务" : string.Format("完成 {0} / {1} 项", finished, total);
            Find<TextBlock>("RemainingLabel").Text = today.Count.ToString(Chinese);
            UpdateProgressBar();
            cats.Refresh(tasks, day);
            UpdateCatDisplay();

            bool showCalendar = calendarTab.IsChecked == true;
            bool showToday = todayTab.IsChecked == true;
            Find<Border>("TodaySummary").Visibility = showToday ? Visibility.Visible : Visibility.Collapsed;
            Find<Border>("QuickCapture").Visibility = showCalendar ? Visibility.Collapsed : Visibility.Visible;
            Find<Grid>("ListHeader").Visibility = showCalendar ? Visibility.Collapsed : Visibility.Visible;
            Find<ScrollViewer>("CalendarView").Visibility = showCalendar ? Visibility.Visible : Visibility.Collapsed;
            taskScroll.Visibility = showCalendar ? Visibility.Collapsed : Visibility.Visible;
            if (showCalendar)
            {
                Find<TextBlock>("CountLabel").Text = "";
                Find<StackPanel>("EmptyPanel").Visibility = Visibility.Collapsed;
                calendar.Refresh(day, tasks, today);
                return;
            }

            List<TodoTask> visible;
            string listTitle;
            string emptyTitle;
            string emptyHint;
            if (upcomingTab.IsChecked == true)
            {
                visible = open.Where(task => task.Due > shownDay).ToList();
                listTitle = "未来计划";
                emptyTitle = "暂无未来计划";
                emptyHint = "添加待办时选一个未来日期，就会显示在这里。";
            }
            else if (repeatTab.IsChecked == true)
            {
                visible = open.Where(task => task.IsRecurring).ToList();
                listTitle = "重复事项";
                emptyTitle = "暂无重复事项";
                emptyHint = "添加待办时可设置每天、工作日或每周指定日期。";
            }
            else if (doneTab.IsChecked == true)
            {
                visible = tasks.Where(task => task.Completed.HasValue).Reverse()
                    .OrderByDescending(task => task.Completed).ToList();
                listTitle = "完成记录";
                emptyTitle = "暂无完成记录";
                emptyHint = "勾选一项待办后，就能在这里找到它。";
            }
            else
            {
                visible = today;
                listTitle = "今日清单";
                emptyTitle = finished == 0 ? "今天还没有待办" : "今天的待办已完成";
                emptyHint = finished == 0 ? "从上方添加一件想做的事。" : "已完成的事项可以在「已完成」里查看。";
            }

            var focusedRow = taskList.Children.OfType<Border>().FirstOrDefault(row => row.IsKeyboardFocusWithin);
            int focusIndex = focusedRow == null ? -1 : taskList.Children.IndexOf(focusedRow);
            taskList.Children.Clear();
            foreach (var task in visible) taskList.Children.Add(CreateRow(task));
            Find<TextBlock>("ListTitle").Text = listTitle;
            int overdue = showToday ? today.Count(task => !task.IsRecurring && task.Due < day) : 0;
            var countPill = Find<Border>("CountPill");
            countPill.Visibility = showToday && overdue == 0 ? Visibility.Collapsed : Visibility.Visible;
            countPill.Background = overdue > 0 ? LateBackground : NeutralBackground;
            var countLabel = Find<TextBlock>("CountLabel");
            countLabel.Foreground = overdue > 0 ? LateText : Muted;
            countLabel.Text = overdue > 0 ? overdue + " 项逾期" : visible.Count + " 项";
            Find<StackPanel>("EmptyPanel").Visibility = visible.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            if (visible.Count == 0)
            {
                var pet = cats.Selected;
                var picture = Find<Image>("EmptyCat");
                DeskPet.SetScaling(picture, pet);
                picture.Source = showToday && finished > 0 ? pet.Happy : pet.Sleeping;
            }
            Find<TextBlock>("EmptyTitle").Text = emptyTitle;
            Find<TextBlock>("EmptyHint").Text = emptyHint;
            if (focusIndex >= 0)
            {
                if (visible.Count == 0) quickTitle.Focus();
                else
                {
                    var row = (Border)taskList.Children[Math.Min(focusIndex, visible.Count - 1)];
                    var cells = ((Grid)row.Child).Children;
                    if (!cells[0].Focus()) ((StackPanel)cells[2]).Children[0].Focus();
                }
            }
        }

        private void UpdateCatDisplay()
        {
            var skin = cats.Selected;
            cat.ShowSkin(skin);
            var tap = Find<Button>("CatTapButton");
            AutomationProperties.SetName(tap, "和" + skin.Name + "互动");
            tap.ToolTip = skin.Name + " · 点一下打招呼";
            var gallery = Find<Button>("CatGalleryButton");
            gallery.Content = cats.NewCats == null ? "伙伴小屋" : "伙伴小屋 · 新";
            gallery.ToolTip = cats.NewCats == null ? "查看和切换已解锁的伙伴" : "新伙伴 · " + cats.NewCats;
            string goal = cats.UnlockedCount == cats.Catalog.Count ? "全员到齐" :
                cats.DaysUntilReward == 0 ? "伙伴待解锁" : "再做 " + cats.DaysUntilReward + " 天解锁";
            Find<TextBlock>("CatStreak").Text = "连续 " + cats.Streak + " 天 · " + goal;
        }

        private Border CreateRow(TodoTask task)
        {
            bool done = task.Completed.HasValue;
            bool late = !done && !task.IsRecurring && task.Due < shownDay;
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var check = new CheckBox
            {
                IsChecked = done, VerticalAlignment = VerticalAlignment.Top,
                Style = (Style)Window.Resources["TaskCheck"],
                HorizontalAlignment = HorizontalAlignment.Left,
                ToolTip = task.SeriesId.HasValue ? "撤销当天完成" : done ? "恢复为未完成" : "标记完成",
                Visibility = task.IsRecurring && todayTab.IsChecked != true
                    ? Visibility.Collapsed : Visibility.Visible
            };
            AutomationProperties.SetName(check, check.ToolTip + " " + task.Title);
            check.Click += delegate
            {
                check.IsChecked = done;
                ToggleCompletion(task);
            };
            grid.Children.Add(check);

            string date = (done ? task.Completed.Value : task.Due).ToString("yyyy-MM-dd", Chinese);
            var details = new StackPanel { Margin = new Thickness(0, 3, 7, 0) };
            var title = new TextBlock
            {
                Text = task.Title, TextWrapping = TextWrapping.Wrap,
                Foreground = done ? Muted : Ink, FontWeight = done ? FontWeights.Normal : FontWeights.SemiBold,
                TextDecorations = done ? TextDecorations.Strikethrough : null
            };
            details.Children.Add(title);
            var badges = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
            if (late) badges.Children.Add(TaskBadge("逾期 · " + date, LateText, LateBackground));
            else if (done) badges.Children.Add(TaskBadge("完成于 " + date, Muted, NeutralBackground));
            else if (task.Due > shownDay || (task.IsRecurring && todayTab.IsChecked != true))
                badges.Children.Add(TaskBadge(task.IsRecurring ? date + " 开始" : date, Muted, NeutralBackground));
            if (task.IsRecurring || task.SeriesId.HasValue)
                badges.Children.Add(TaskBadge(task.IsRecurring ? RepeatPicker.Describe(task.RepeatDays) : "重复事项", AccentText, AccentBackground));
            if (!done && task.ReminderMinute.HasValue)
                badges.Children.Add(TaskBadge("提醒 " + FormatReminder(task.ReminderMinute.Value), AccentText, AccentBackground));
            if (badges.Children.Count > 0) details.Children.Add(badges);
            Grid.SetColumn(details, 1);
            grid.Children.Add(details);

            var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top };
            var edit = ActionButton("编辑");
            edit.Click += delegate { EditTask(task); };
            var remove = ActionButton(task.IsRecurring ? "停止重复" : "×");
            if (!task.IsRecurring) remove.FontSize = 16;
            var removeLabel = task.IsRecurring ? "停止重复" : "删除待办";
            remove.ToolTip = removeLabel;
            AutomationProperties.SetName(remove, removeLabel);
            remove.Click += delegate
            {
                string message = task.IsRecurring ? "要停止重复这项待办吗？" : "要删除这项待办吗？";
                if (task.SeriesId.HasValue && task.Due == DateTime.Today &&
                    tasks.Any(entry => entry.Id == task.SeriesId && entry.OccursOn(task.Due)))
                    message = "删除这条完成记录？这项待办会重新显示在「今天」。";
                if (MessageBox.Show(Window, message, removeLabel,
                    MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
                    RemoveTask(task);
            };
            actions.Children.Add(edit);
            actions.Children.Add(remove);
            Grid.SetColumn(actions, 2);
            grid.Children.Add(actions);

            return new Border
            {
                Child = grid, BorderBrush = RowBorder, BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(1, 7, 0, 7)
            };
        }

        private static Border TaskBadge(string text, Brush foreground, Brush background)
        {
            return new Border
            {
                Background = background, CornerRadius = new CornerRadius(5),
                Margin = new Thickness(0, 0, 5, 2), Padding = new Thickness(6, 2, 6, 2),
                Child = new TextBlock { Text = text, FontSize = 11, Foreground = foreground }
            };
        }

        private Button ActionButton(string caption)
        {
            return new Button
            {
                Content = caption, Style = (Style)Window.Resources["ActionButton"],
                Padding = new Thickness(8, 4, 8, 4), FontSize = 12, MinWidth = 29
            };
        }

        private static string FormatReminder(int minute)
        {
            return TimeSpan.FromMinutes(minute).ToString(@"hh\:mm", CultureInfo.InvariantCulture);
        }

        private static bool TryReadReminder(string text, out int? minute)
        {
            minute = null;
            if (string.IsNullOrWhiteSpace(text)) return true;
            DateTime parsed;
            if (!DateTime.TryParseExact(text.Trim().Replace('：', ':'), new[] { "H:mm", "HH:mm" },
                CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
                return false;
            minute = parsed.Hour * 60 + parsed.Minute;
            return true;
        }

        private void EditTask(TodoTask task)
        {
            var dialog = new Window
            {
                Owner = Window, Title = "编辑待办", Width = 418,
                SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner, ShowInTaskbar = false,
                FontFamily = Window.FontFamily, FontSize = Window.FontSize, Language = Window.Language,
                Background = Window.Background, Foreground = Ink
            };
            var fields = new StackPanel { Margin = new Thickness(20) };
            var title = new TextBox
            {
                Text = task.Title, MaxLength = 200, MinHeight = 35, Padding = new Thickness(8, 6, 8, 6),
                TextWrapping = TextWrapping.Wrap, MaxHeight = 120, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Style = (Style)Window.Resources["Field"]
            };
            fields.Children.Add(new Label { Content = "事项", Target = title, Padding = new Thickness(0, 0, 0, 5) });
            fields.Children.Add(title);
            var date = new DatePicker
            {
                SelectedDate = task.Due, SelectedDateFormat = DatePickerFormat.Short, Width = 150,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            var dateText = new Style(typeof(DatePickerTextBox));
            dateText.Setters.Add(new Setter(TextBox.IsReadOnlyProperty, true));
            date.Resources.Add(typeof(DatePickerTextBox), dateText);
            var dateLabel = new Label
            {
                Content = task.IsRecurring ? "开始日期" : "计划日期", Target = date,
                Padding = new Thickness(0, 12, 0, 5)
            };
            if (!task.SeriesId.HasValue)
            {
                fields.Children.Add(dateLabel);
                fields.Children.Add(date);
            }

            var repeat = new RepeatPicker(task.RepeatDays) { Margin = new Thickness(0, 12, 0, 0) };
            if (!task.Completed.HasValue)
                fields.Children.Add(repeat);

            var reminder = new TextBox
            {
                Text = task.ReminderMinute.HasValue ? FormatReminder(task.ReminderMinute.Value) : "",
                Width = 150, MinHeight = 30, MaxLength = 8, Padding = new Thickness(8, 4, 8, 4),
                HorizontalAlignment = HorizontalAlignment.Left, Style = (Style)Window.Resources["Field"]
            };
            const string reminderHint = "例如 09:30，留空可关闭提醒。";
            var hint = new TextBlock
            {
                Text = reminderHint, Margin = new Thickness(0, 5, 0, 0), FontSize = 12, Foreground = Muted
            };
            if (!task.SeriesId.HasValue)
            {
                fields.Children.Add(new Label
                {
                    Content = "提醒时间（可选）", Target = reminder, Padding = new Thickness(0, 12, 0, 5)
                });
                fields.Children.Add(reminder);
                fields.Children.Add(hint);
                fields.Children.Add(new TextBlock
                {
                    Text = "有提醒时，关闭窗口会转到系统托盘。", Margin = new Thickness(0, 4, 0, 0),
                    FontSize = 12, Foreground = Muted
                });
            }

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 16, 0, 0)
            };
            var cancel = ActionButton("取消");
            cancel.IsCancel = true;
            var save = new Button
            {
                Content = "保存", Style = (Style)Window.Resources["PrimaryButton"],
                IsDefault = true, MinWidth = 62, Margin = new Thickness(8, 0, 0, 0)
            };
            Action validate = delegate
            {
                int? minute;
                bool validTime = TryReadReminder(reminder.Text, out minute);
                save.IsEnabled = !string.IsNullOrWhiteSpace(title.Text) && validTime && repeat.IsValid;
                hint.Text = validTime ? reminderHint : "请输入有效的 24 小时时间，例如 09:30。";
                hint.Foreground = validTime ? Muted : LateText;
                if (validTime && !repeat.IsValid)
                {
                    hint.Text = "请至少勾选一个重复日期。";
                    hint.Foreground = LateText;
                }
            };
            title.TextChanged += delegate { validate(); };
            reminder.TextChanged += delegate { validate(); };
            repeat.Changed += delegate
            {
                dateLabel.Content = repeat.IsRepeating ? "开始日期" : "计划日期";
                validate();
            };
            save.Click += delegate
            {
                int? minute;
                if (!repeat.IsValid || !TryReadReminder(reminder.Text, out minute)) return;
                var updatedDate = date.SelectedDate.GetValueOrDefault(task.Due).Date;
                if (ReplaceTask(task, title.Text.Trim(), updatedDate, task.Completed, repeat.Days, minute, dialog))
                    dialog.DialogResult = true;
            };
            buttons.Children.Add(cancel);
            buttons.Children.Add(save);
            fields.Children.Add(buttons);
            dialog.Content = fields;
            dialog.Loaded += delegate { title.Focus(); title.SelectAll(); };
            dialog.ShowDialog();
        }
    }
}
