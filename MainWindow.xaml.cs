// (c) 2021 Charles Donohue
// This code is licensed under MIT license (see LICENSE file for details)

using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using SelectionSet;
using ReplayCapture;
using WatchedVariable;
using Timeline;
using TimelineControls;
using FontAwesome.WPF;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Threading.Tasks;
using System.Windows.Input;
using VisualReplayDebugger.Panels;

namespace VisualReplayDebugger
{
    public partial class MainWindow : Window
    {
        public SelectionGroup<Entity> EntitySelection { get; private set; } = new();
        public SelectionGroup<Entity> VisibleEntities { get; private set; } = new();

        public WatchedBool Play { get; } = new();        

        public event Action FocusOnSelected;

        internal ReplayCaptureReader Replay;
        internal event Action<ReplayCaptureReader> ReplayChanged;
        internal TimelineWindow TimelineWindow;
        internal TimelineController TimelineController;

        public MainWindow()
        {
            InitializeComponent();

            //this.DockManager.Theme.

            this.Title = "Visual Replay Debugger";

            var timeline = new Timeline.Timeline();
            TimelineWindow = new TimelineWindow(timeline);
            TimelineController = new TimelineController(timeline);

            Play.Changed += () => TimelineController.Play(Play.Value);
            TimelineController.Stopped += () => Play.Set(false);
            EntitySelection.Changed += () => Play.Set(false);

            BuildMainMenu(this);
            BuildTimelineControlTop();
            BuildTimelineControlBottom();

            TimelinesToolWindow.Content = new EntityTimelinesPanel(this);
            GraphsWindow.Content = new GraphsPanel(this);
            LogsWindow.Content = new LogsPanel(this);
            DrawsLogsWindow.Content = new DrawLogsPanel(this);
            ViewportWindow.Content = new ViewportPanel(this);
            PropertiesWindow.Content = new PropertiesPanel(this);

            // Keyboard Actions
            RoutedCommand leftCommand = new RoutedCommand();
            leftCommand.InputGestures.Add(new KeyGesture(Key.Left));
            CommandBindings.Add(new CommandBinding(leftCommand, (o,e) => stepFrameReverse()));

            RoutedCommand rightCommand = new RoutedCommand();
            rightCommand.InputGestures.Add(new KeyGesture(Key.Right));
            CommandBindings.Add(new CommandBinding(rightCommand, (o,e) => stepFrameForward()));

            RoutedCommand focusCommand = new RoutedCommand();
            focusCommand.InputGestures.Add(new KeyGesture(Key.F2));
            CommandBindings.Add(new CommandBinding(focusCommand, (o,e) => TriggerFocusOnSelected()));

            string replayPath = Environment.GetCommandLineArgs().Skip(1).FirstOrDefault();
            if (!string.IsNullOrEmpty(replayPath))
            {
                LoadReplay(replayPath);
            }

            // Popup on exceptions
            this.Dispatcher.UnhandledException += (o, e) =>
            {
                if (!System.Diagnostics.Debugger.IsAttached)
                {
                    MessageBox.Show($"Exception: {e.Exception.Message}\n{e.Exception.StackTrace.Split("\r\n").FirstOrDefault()}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    e.Handled = true;
                }
            };
        }

        public static void BuildMainMenu(MainWindow mainWindow)
        {
            var menu = mainWindow.MainMenu;

            var fileMenu = new MenuItem() { Header = "_File" };
            menu.Items.Add(fileMenu);

            var fileopen = new MenuItem() { Header = "_Open" };
            fileMenu.Items.Add(fileopen);
            fileopen.Click += (o, e) =>
            {
                var openFileDialog = new Microsoft.Win32.OpenFileDialog();
                if (openFileDialog.ShowDialog() == true)
                {
                    mainWindow.LoadReplay(openFileDialog.FileName);
                }
            };

            var exit = new MenuItem() { Header = "_Exit" };
            fileMenu.Items.Add(exit);
            exit.Click += (o, e) => System.Windows.Application.Current.Shutdown();

            var viewmenu = new MenuItem() { Header = "_View" };
            menu.Items.Add(viewmenu);

            var showgraphs = new MenuItem() { Header = "_Graphs" };
            showgraphs.Click += (o, e) => mainWindow.ShowOrDuplicatePanel(mainWindow.GraphsWindow);
            viewmenu.Items.Add(showgraphs);

            var showtimelines = new MenuItem() { Header = "_Timelines" };
            showtimelines.Click += (o, e) => mainWindow.ShowOrDuplicatePanel(mainWindow.TimelinesToolWindow);
            viewmenu.Items.Add(showtimelines);

            var showlogs = new MenuItem() { Header = "_Logs" };
            showlogs.Click += (o, e) => mainWindow.ShowOrDuplicatePanel(mainWindow.LogsWindow);
            viewmenu.Items.Add(showlogs);

            var showdraws = new MenuItem() { Header = "_Draws" };
            showdraws.Click += (o, e) => mainWindow.ShowOrDuplicatePanel(mainWindow.DrawsLogsWindow);
            viewmenu.Items.Add(showdraws);

            var showviewport = new MenuItem() { Header = "_Viewport" };
            showviewport.Click += (o, e) => mainWindow.ShowOrDuplicatePanel(mainWindow.ViewportWindow);
            viewmenu.Items.Add(showviewport);

            var showproperties = new MenuItem() { Header = "_Properties" };
            showproperties.Click += (o, e) => mainWindow.ShowOrDuplicatePanel(mainWindow.PropertiesWindow);
            viewmenu.Items.Add(showproperties);
        }

        public void ShowOrDuplicatePanel(AvalonDock.Layout.LayoutAnchorable original)
        {
            if (!original.IsVisible)
            {
                original.IsVisible = true;
            }
            else
            {
                this.DuplicatePanel(original);
            }
        }

        public void DuplicatePanel(AvalonDock.Layout.LayoutAnchorable original) 
        {
            if (original.Content is DockPanelWithToolbar originalPanel)
            {
                var newPanel = new AvalonDock.Layout.LayoutAnchorable();
                newPanel.Title = original.Title;
                newPanel.Content = Activator.CreateInstance(originalPanel.GetType(), this);
                newPanel.AddToLayout(this.DockManager, AvalonDock.Layout.AnchorableShowStrategy.Most);
            }
        }

        public void BuildTimelineControlTop()
        {
            const int h = 24;

            var rewindButton = new Button() { Content = GetIcon(FontAwesomeIcon.FastBackward), Width = h, Height = h };
            rewindButton.Click += (o, e) => { TimelineController.Rewind(); };
            TimeControlsPanel.Children.Add(rewindButton);

            var step_bwd = new Button() { Content = GetIcon(FontAwesomeIcon.StepBackward), Width = h, Height = h };
            step_bwd.Click += (o, e) => stepFrameReverse();
            TimeControlsPanel.Children.Add(step_bwd);

            var playButton = new ToggleButton() { Content = GetIcon(FontAwesomeIcon.Play), Width = h, Height = h };
            playButton.Click += (o, e) => { Play.Set(playButton.IsChecked.Value); };
            Play.Changed += () => { playButton.IsChecked = Play; playButton.Content = GetIcon(Play ? FontAwesomeIcon.Stop : FontAwesomeIcon.Play); };
            TimeControlsPanel.Children.Add(playButton);

            var step_fwd = new Button() { Content = GetIcon(FontAwesomeIcon.StepForward), Width = h, Height = h };
            step_fwd.Click += (o, e) => stepFrameForward();
            TimeControlsPanel.Children.Add(step_fwd);

            var timelabel = new Label() { Background = Brushes.AliceBlue, Height = h };
            timelabel.Content = Timeline.Timeline.TimeString(0);
            TimelineWindow.Timeline.Changed += () => { timelabel.Content = Timeline.Timeline.TimeString(TimelineWindow.Timeline.Cursor); };
            TimeControlsPanel.Children.Add(timelabel);

            var framelabel = new Label() { Background = Brushes.AliceBlue, Height = h };
            framelabel.Content = $"Frame: {0:D5}";
            TimelineWindow.Timeline.Changed += () => { framelabel.Content = $"Frame: {Replay?.GetFrameForTime(TimelineWindow.Timeline.Cursor) ?? 0:D5}"; };
            TimeControlsPanel.Children.Add(framelabel);

            // Time scrubber
            var timelineViewTop = new TimelineViewControl(TimelineWindow, preferredHeight: h);
            TimeControlsPanel.Children.Add(timelineViewTop);
        }

        public void BuildTimelineControlBottom()
        {
            var timelineViewBottom = new TimelineViewControl(TimelineWindow, preferredHeight: 20);
            var timelineWindowViewBottom = new TimelineWindowControl(TimelineWindow, preferredHeight: 15);
            TimeScrubPanel.Children.Add(timelineViewBottom);
            TimeScrubPanel.Children.Add(timelineWindowViewBottom);
        }


        public static Image GetIcon(FontAwesomeIcon icon, int width = 14, int height = 14) => new ImageAwesome { Icon = icon, Width = width, Height = height };

        public void LoadReplay(string path)
        {
            string pathName = path;
            this.Cursor = Cursors.AppStarting;
            this.Title = $"Visual Replay Debugger - {pathName} (loading...)";
            Task.Run(() =>
            {
                var replay = new ReplayCaptureReader(path);
                Application.Current.Dispatcher.Invoke(new Action(() =>
                {
                    this.Title = $"Visual Replay Debugger - {pathName}";
                    this.Cursor = Cursors.Arrow;
                    Replay = replay;
                    ReplayChanged?.Invoke(replay);

                    EntitySelection.Clear();
                    EntitySelection.Add(Replay.Entities.FirstOrDefault());
                    VisibleEntities.Set(Replay.Entities);

                    this.TimelineWindow.Fill();
                }));
            });
        }

        public void stepFrameForward()
        {
            TimelineController.Stop();
            if (Replay != null)
            {
                int currentFrame = Replay.GetFrameForTime(TimelineWindow.Timeline.Cursor);
                double nextFrameTime = Replay.GetTimeForFrame(currentFrame + 1);
                if ((nextFrameTime - TimelineWindow.Timeline.Cursor) < float.Epsilon) { nextFrameTime = Replay.GetTimeForFrame(currentFrame + 2); }
                TimelineWindow.Timeline.Cursor = nextFrameTime + 0.00001;
            }
        }

        public void stepFrameReverse()
        {
            TimelineController.Stop();
            if (Replay != null)
            {
                double currentFrameTime = Replay.GetFrameTimes(TimelineWindow.Timeline.Cursor, out double previousFrameTime, out double _);
                TimelineWindow.Timeline.Cursor = ((TimelineWindow.Timeline.Cursor - currentFrameTime) < 1E-3 ? previousFrameTime : currentFrameTime) + 0.00001;
            }
        }

        public void SetTimeRangeToSelected()
        {
            if (Replay != null && !EntitySelection.Empty)
            {
                int min = Replay.FrameTimes.Count;
                int max = 0;
                foreach (var entity in EntitySelection.SelectionSet)
                {
                    var range = Replay.GetEntityLifeTime(entity);
                    if (range.Start < min) { min = range.Start; }
                    if (range.End > max) { max = range.End; }
                }
                TimelineWindow.Start = Replay.GetTimeForFrame(min);
                TimelineWindow.End = Replay.GetTimeForFrame(max);
            }
        }

        public void TriggerFocusOnSelected() => FocusOnSelected?.Invoke();
    }
}
