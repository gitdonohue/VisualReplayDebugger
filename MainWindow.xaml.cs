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
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Threading.Tasks;
using System.Windows.Input;
using VisualReplayDebugger.Panels;

using FontAwesomeIcon = FontAwesome.Sharp.IconChar;

namespace VisualReplayDebugger;

public partial class MainWindow : Window
{
    public SelectionGroup<Entity> EntitySelection { get; private set; } = new();
    public SelectionGroup<Entity> VisibleEntities { get; private set; } = new();

    public WatchedBool Play { get; } = new();        

    public event Action FocusOnSelected;
    public event Action JumpToNext;
    public event Action JumpToPrevious;
    public event Action FindCalled;
    public event Action CopyCalled;

    internal ReplayCaptureReader Replay;
    internal event Action<ReplayCaptureReader> ReplayChanged;
    internal TimelineWindow TimelineWindow;
    internal TimelineController TimelineController;
    internal ColorProvider ColorProvider { get; } = new();

    public MainWindow()
    {
        InitializeComponent();

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
        PropertiesTimelinesWindow.Content = new PropertiesTimelinesPanel(this);
        LogsWindow.Content = new LogsPanel(this);
        DrawsLogsWindow.Content = new DrawLogsPanel(this);
        ViewportWindow.Content = new ViewportPanel(this);
        PropertiesWindow.Content = new PropertiesPanel(this);
        VideoWindow.Content = new VideoPanel(this); 

        // Keyboard Actions
        RoutedCommand focusCommand = new RoutedCommand();
        focusCommand.InputGestures.Add(new KeyGesture(Key.F2));
        CommandBindings.Add(new CommandBinding(focusCommand, (o,e) => TriggerFocusOnSelected()));

        RoutedCommand jumpNextCommand = new RoutedCommand();
        jumpNextCommand.InputGestures.Add(new KeyGesture(Key.F3));
        CommandBindings.Add(new CommandBinding(jumpNextCommand, (o, e) => TriggerJumpToNext()));

        RoutedCommand jumpPrevCommand = new RoutedCommand();
        jumpPrevCommand.InputGestures.Add(new KeyGesture(Key.F3, ModifierKeys.Shift));
        CommandBindings.Add(new CommandBinding(jumpPrevCommand, (o, e) => TriggerJumpToPrevious()));

        RoutedCommand findCommand = new RoutedCommand();
        findCommand.InputGestures.Add(new KeyGesture(Key.F, ModifierKeys.Control));
        CommandBindings.Add(new CommandBinding(findCommand, (o, e) => TriggerFind()));

        // global keyboard handling
        EventManager.RegisterClassHandler(typeof(Control), PreviewKeyDownEvent, new RoutedEventHandler(KeyboardHandler));

        this.AllowDrop = true;
        EventManager.RegisterClassHandler(typeof(Control), DropEvent, new RoutedEventHandler(DragDropHandler));

        // Popup on exceptions
        this.Dispatcher.UnhandledException += (o, e) =>
        {
            if (!System.Diagnostics.Debugger.IsAttached)
            {
                MessageBox.Show($"Exception: {e.Exception.Message}\n{e.Exception.StackTrace.Split("\r\n").FirstOrDefault()}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                e.Handled = true;
            }
        };

        string replayPath = Environment.GetCommandLineArgs().Skip(1).FirstOrDefault();
        if (!string.IsNullOrEmpty(replayPath))
        {
            LoadReplay(replayPath);
        }
    }

    internal void DragDropHandler(object sender, RoutedEventArgs e)
    {
        if (e is DragEventArgs dragEvent)
        {
            string[] fileList = (string[])dragEvent.Data.GetData(DataFormats.FileDrop, false);
            if (fileList.Length > 0)
            {
                string fileToLoad = fileList[0];
                LoadReplay(fileToLoad);
                e.Handled = true;
            }
        }
    }

    internal void KeyboardHandler(object sender, RoutedEventArgs e)
    {
        if (e is System.Windows.Input.KeyEventArgs keyArgs)
        {
            if (keyArgs.Key == Key.Right) 
            {
                stepFrameForward();
                e.Handled = true;
            }
            if (keyArgs.Key == Key.Left)
            {
                stepFrameReverse();
                e.Handled = true;
            }
        }
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

        //var testExport = new MenuItem() { Header = "_TestSerializer" };
        //fileMenu.Items.Add(testExport);
        //testExport.Click += (o, e) => mainWindow.LoadReplay(SerializeTest.TestReplayFileExport());

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

        var showpropertiestimelines = new MenuItem() { Header = "PropertiesTimelines" };
        showpropertiestimelines.Click += (o, e) => mainWindow.ShowOrDuplicatePanel(mainWindow.PropertiesTimelinesWindow);
        viewmenu.Items.Add(showpropertiestimelines);

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

        //viewmenu.Items.Add(new Separator());

        //var saveLayout = new MenuItem() { Header = "_Save Layout" };
        //saveLayout.Click += (o, e) => new XmlLayoutSerializer(mainWindow.DockManager).Serialize("layoutserialization.xml");
        //viewmenu.Items.Add(saveLayout);

        //var loadLayout = new MenuItem() { Header = "Load _Layout" };
        //loadLayout.Click += (o, e) => new XmlLayoutSerializer(mainWindow.DockManager).Deserialize("layoutserialization.xml");
        //viewmenu.Items.Add(loadLayout);
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

        var rewindButton = new Button() { Content = IconProvider.GetIcon(FontAwesomeIcon.FastBackward), Width = h, Height = h };
        rewindButton.Click += (o, e) => { TimelineController.Rewind(); };
        TimeControlsPanel.Children.Add(rewindButton);

        var step_bwd = new Button() { Content = IconProvider.GetIcon(FontAwesomeIcon.StepBackward), Width = h, Height = h };
        step_bwd.Click += (o, e) => stepFrameReverse();
        TimeControlsPanel.Children.Add(step_bwd);

        var playButton = new ToggleButton() { Content = IconProvider.GetIcon(FontAwesomeIcon.Play), Width = h, Height = h };
        playButton.Click += (o, e) => { Play.Set(playButton.IsChecked.Value); };
        Play.Changed += () => { playButton.IsChecked = Play; playButton.Content = IconProvider.GetIcon(Play ? FontAwesomeIcon.Stop : FontAwesomeIcon.Play); };
        TimeControlsPanel.Children.Add(playButton);

        var step_fwd = new Button() { Content = IconProvider.GetIcon(FontAwesomeIcon.StepForward), Width = h, Height = h };
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
                this.Title = $"Visual Replay Debugger - {pathName} (prepping...)";

                Replay = replay;
                ReplayChanged?.Invoke(replay);

                EntitySelection.Clear();
                EntitySelection.Add(Replay.Entities.Values.FirstOrDefault());
                VisibleEntities.Set(Replay.Entities.Values);

                this.TimelineWindow.Fill();

                this.Title = $"Visual Replay Debugger - {pathName}";
                this.Cursor = Cursors.Arrow;
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
            int min = Replay.FrameTimes.Length;
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
    public void TriggerJumpToNext() => JumpToNext?.Invoke();
    public void TriggerJumpToPrevious() => JumpToPrevious?.Invoke();
    public void TriggerFind() => FindCalled?.Invoke();

    private void CopyCommandHandler(object sender, ExecutedRoutedEventArgs e)
    {
        CopyCalled?.Invoke();
    }

}
