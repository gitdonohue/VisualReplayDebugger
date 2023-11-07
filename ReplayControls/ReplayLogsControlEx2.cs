// (c) 2021 Charles Donohue
// This code is licensed under MIT license (see LICENSE file for details)

//#define VERBOSE

using ReplayCapture;
using SelectionSet;
using System;
using System.Collections.Generic;
using System.Drawing.Text;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Timeline;
using WatchedVariable;

namespace VisualReplayDebugger;

// Much faster than than AnavlonEdit or VirtualizingStackPanel, simpler color management and overlay.
public class ReplayLogsControlEx2 : UserControl, IDisposable
{
    public WatchedVariable<string> FilterText { get; } = new();
    public WatchedVariable<string> SearchText { get; } = new();
    public WatchedBool ShowSelectedLogsOnly { get; } = new(false);
    public WatchedBool EntitySelectionLocked { get; } = new(false);
    
    private IEnumerable<Entity> SelectedEntities => EntitySelectionLocked ? LockedSelection : EntitySelection.SelectionSet;

    private readonly List<Entity> LockedSelection = new();
    public SelectionGroup<string> LogCategoryFilter { get; } = new();
    public SelectionGroup<ReplayCapture.Color> LogColorFilter { get; } = new();
    public ScrollViewer ScrollOwner { get; set; }

    ReplayCaptureReader replay;
    public ReplayCaptureReader Replay
    {
        get => replay;
        set
        {
            replay = value;
            if (replay != null)
            {
                AllLogs = Replay.LogEntries.Select(x => new LogEntryRecord(x.frame, x.val.entity, x.val.category, x.val.message, 
                    LogHeaderFormat(x.frame, x.val.entity, x.val.category, x.val.message), 
                    LogFormat(x.frame, x.val.entity, x.val.category, x.val.message), 
                    x.val.color)).ToArray();
            }
            EntitySelectionLocked.Set(false);
            RefreshFilteredLogs();
            RefreshLogs();
        }
    }

    record LogEntryRecord(int frame, Entity entity, string category, string log, string logHeader, string formattedLog, ReplayCapture.Color color);

    private LogEntryRecord[] AllLogs = new LogEntryRecord[0];
    private LogEntryRecord[] FilteredLogs = new LogEntryRecord[0];
    private LogEntryRecord[] ActiveLogs = new LogEntryRecord[0];

    public record TextEntry
    {
        public string Text;
        public int Index;
    }

    private readonly SelectionGroup<Entity> EntitySelection;
    private readonly SelectionGroup<Entity> HiddenSelection;
    private readonly SelectionGroup<Entity> StarredSelection;
    private readonly ITimelineWindow TimelineWindow;

    private readonly SelectionSpans SelectionSpans = new();
    private int LastSelectionIndex = -1;

    public ReplayLogsControlEx2(ReplayCaptureReader replay, ITimelineWindow timelineWindow, SelectionGroup<Entity> entitySelection, SelectionGroup<Entity> hiddenSelection, SelectionGroup<Entity> starredSelection)
    {
        Replay = replay;
        EntitySelection = entitySelection;
        HiddenSelection = hiddenSelection;
        StarredSelection = starredSelection;
        TimelineWindow = timelineWindow;

        TimelineWindow.Changed += TimelineWindow_Changed;
        EntitySelection.Changed += EntitySelection_Changed;
        StarredSelection.Changed += Selection_Changed;
        HiddenSelection.Changed += Selection_Changed;
        
        EntitySelectionLocked.Changed += RefreshLogsAndFilters;
        FilterText.Changed += RefreshLogsAndFilters;
        ShowSelectedLogsOnly.Changed += RefreshLogsAndFilters;
        LogCategoryFilter.Changed += RefreshLogsAndFilters;
        LogColorFilter.Changed += RefreshLogsAndFilters;
        
        this.IsVisibleChanged += (o, e) => RefreshLogsAndFilters();

        this.MouseDown += OnMouseDown;
        //EventManager.RegisterClassHandler(typeof(Control), MouseDownEvent, new RoutedEventHandler(MouseDownHandler)); // Workaround for missing mouse events
        this.MouseDoubleClick += OnMouseDoubleClick;
        //EventManager.RegisterClassHandler(typeof(Control), MouseDoubleClickEvent, new RoutedEventHandler(MouseDoubleClickHandler)); // Workaround for missing mouse events

        SelectionSpans.Changed += InvalidateVisualLocal;

        SearchText.Changed += RefreshLogsAndFilters;
        SearchText.Changed += JumpToNextSearchResult;

        // Selected only and selection lock buttons can change their relative states.
        ShowSelectedLogsOnly.Changed += () => { if (!ShowSelectedLogsOnly) { EntitySelectionLocked.Set(false); } };
        EntitySelectionLocked.Changed += () => { if (EntitySelectionLocked) { ShowSelectedLogsOnly.Set(true); } };

        // Keyboard Actions
        RoutedCommand focusCommand = new RoutedCommand();
        focusCommand.InputGestures.Add(new KeyGesture(Key.F3));
        CommandBindings.Add(new CommandBinding(focusCommand, (o, e) => JumpToNextSearchResult()));
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }

    private void InvalidateVisualLocal()
    {
        DebugLog("LogsControl InvalidateVisualLocal");
        InvalidateVisual();
    }

    public string GetText()
    {
        return string.Join('\n',ActiveLogs.Select(x=> $"{x.logHeader} {x.formattedLog}"));
    }

    private string LogHeaderFormat(int frame, Entity entity, string category, string log)
    {
        if (entity == null) return log;
        string categoryLabel = category;
        if (entity.CategoryName != category && !string.IsNullOrEmpty(category))
        {
            categoryLabel = $"{entity.CategoryName}/{category}";
        }
        else if (string.IsNullOrEmpty(category))
        {
            categoryLabel = entity.CategoryName;
        }
        return $"{Timeline.Timeline.TimeString(Replay.GetTimeForFrame(frame))} ({frame}) {categoryLabel} [{entity.Name}] -";
    }
    private string LogFormat(int frame, Entity entity, string category, string log) => log;

    private IEnumerable<LogEntryRecord> CollectLogs()
    {
        var windowRange = Replay.GetFramesForTimes(TimelineWindow.Start, TimelineWindow.End);
        int currentFrame = Replay.GetFrameForTime(TimelineWindow.Timeline.Cursor);

        DebugLog($"CollectLogs at frame {currentFrame} ({TimelineWindow.Start},{TimelineWindow.End},{TimelineWindow.Timeline.Cursor})");

        var currentFrameEntry = new LogEntryRecord(currentFrame, default(Entity), string.Empty, string.Empty, $"{Timeline.Timeline.TimeString(Replay.GetTimeForFrame(currentFrame))} ({currentFrame})", "------------------------", ReplayCapture.Color.Black );

        bool currentTimeEntryShown = false;
        foreach (var logEntry in FilteredLogs)
        {
            int frame = logEntry.frame;
            if (frame < windowRange.Start)
            {
                // skip
            }
            else if (frame > windowRange.End)
            {
                // skip and break
                break;
            }
            else
            {
                if (frame > currentFrame && !currentTimeEntryShown)
                {
                    yield return currentFrameEntry;
                    currentTimeEntryShown = true;
                }

                // In range
                yield return logEntry;
            }
        }

        if (!currentTimeEntryShown)
        {
            yield return currentFrameEntry;
        }
    }

    public void RefreshFilteredLogs()
    {
        if (Replay == null) return;

        var filter = new SearchContext(FilterText.Value);
        FilteredLogs = AllLogs.Where(x =>
            (!ShowSelectedLogsOnly || SelectedEntities.Contains(x.entity))
            && (!HiddenSelection.Contains(x.entity))
            && (LogCategoryFilter.Empty || !LogCategoryFilter.Contains(x.category))
            && (LogColorFilter.Empty || !LogColorFilter.Contains(x.color))
            && (filter.Match(x.logHeader) || filter.Match(x.formattedLog)))
            .ToArray();
    }

    private int CurrentTimeActiveLogLineStart;
    private int CurrentTimeActiveLogLineEnd;

    public void RefreshLogs()
    {
        if (Replay == null) return;
        if (this.IsVisible == false) return;

        SelectionSpans.Clear();
        LastSelectionIndex = -1;
        
        Task.Run(() => 
        {
            // Off ui thread
            ActiveLogs = CollectLogs().ToArray();

            // On ui thread
            Application.Current.Dispatcher.Invoke(() =>
            {
                // Code to run on the GUI thread.
                double h = ActiveLogs.Length * LineHeight;
                if (h < ViewportHeight) h = ViewportHeight;
                this.Height = h;
        
                InvalidateVisualLocal(); 
            });
        });
    }

    public void RefreshLogsAndFilters() { RefreshFilteredLogs(); RefreshLogs(); }

    double _viewportHeight = 0;
    double ViewportHeight
    {
        get => _viewportHeight;
        set
        {
            if (value != _viewportHeight)
            {
                _viewportHeight = value;
                InvalidateVisualLocal();
            }
        }
    }

    double _verticalOffset = 0;
    double VerticalOffset
    {
        get => _verticalOffset;
        set
        {
            if (value != _verticalOffset)
            {
                _verticalOffset = value;
                InvalidateVisualLocal();
            }
        }
    }


    int _lastFrame = -1;
    private void TimelineWindow_Changed()
    {
        RefreshLogs();

        int currentFrame = Replay?.GetFrameForTime(TimelineWindow.Timeline.Cursor) ?? 0;

        CurrentTimeActiveLogLineStart = Array.FindIndex(ActiveLogs, x => x.frame == currentFrame);
        CurrentTimeActiveLogLineEnd = Array.FindLastIndex(ActiveLogs, x => x.frame == currentFrame);

        // Auto scrolling
        if (_lastFrame != currentFrame)
        {
            int lineNumAtCursorFrame = 0;
            foreach (var log in ActiveLogs)
            {
                if (_lastFrame < currentFrame)
                {
                    // searching forwards
                    if (log.frame > currentFrame) break;
                }
                else
                {
                    // searching backwards
                    if (log.frame >= currentFrame) break;
                }
                ++lineNumAtCursorFrame;
            }
            if (lineNumAtCursorFrame > 0 && (_lastFrame < currentFrame)) { lineNumAtCursorFrame -= 1; }
            _lastFrame = currentFrame;
            ScrollToLine(lineNumAtCursorFrame);
        }
    }

    private void ScrollToLine(int lineIndex)
    {
        DebugLog($"LogsControl Scrolling to line: {lineIndex+1}");
        if (IsJumpingToTime) return;
        
        double y_min = VerticalOffset;
        double y_max = y_min + ViewportHeight;
        double lineYPos = lineIndex * LineHeight;            
        if (lineYPos > (y_max - LineHeight))
        {
            // Scroll downwards
            ScrollOwner.ScrollToVerticalOffset(lineYPos - ViewportHeight + LineHeight);
        }
        else if (lineYPos < y_min)
        {
            // Scoll upwards
            ScrollOwner.ScrollToVerticalOffset(lineYPos);
        }
    }

    private void EntitySelection_Changed()
    {
        if (!EntitySelectionLocked)
        {
            LockedSelection.Clear();
            LockedSelection.AddRange(EntitySelection.SelectionSet);
        }
        Selection_Changed();
    }

    private void Selection_Changed()
    {
        RefreshLogsAndFilters();
    }

    public void ScrollingUpdated(ScrollViewer scrollViewer)
    {
        VerticalOffset = scrollViewer.VerticalOffset;
        ViewportHeight = scrollViewer.ViewportHeight;
    }

    internal void MouseDownHandler(object sender, RoutedEventArgs e)
    {
        if (e is MouseButtonEventArgs mb) 
        {
            var mousePos = mb.GetPosition(this);
            if (mousePos.X > 0
                && mousePos.X < ActualWidth
                && mousePos.Y >= VerticalOffset
                && mousePos.Y <= (VerticalOffset + ViewportHeight) )
            {
                OnMouseDown(sender, mb);
            }
        }
    }

    private void OnMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (Replay == null) { return; }

        var mousePos = e.GetPosition(this);
        double mousePosY = mousePos.Y;
        int lineNum = (int)Math.Floor(mousePosY / LineHeight);

        DebugLog($"LogsControl Mouse at line:{lineNum}");

        int startRange = lineNum;
        if ((Keyboard.GetKeyStates(Key.LeftShift) & KeyStates.Down) > 0)
        {
            //if (LastSelectionIndex >= 0) startRange = LastSelectionIndex;
            startRange = (LastSelectionIndex > 0) ? LastSelectionIndex : 0;
        }

        bool toggle = ((Keyboard.GetKeyStates(Key.LeftCtrl) & KeyStates.Down) > 0);
        if (toggle)
        {
            SelectionSpans.ToggleSelection(startRange, lineNum);
        }
        else
        {
            SelectionSpans.SetSelection(startRange, lineNum);
        }
        LastSelectionIndex = lineNum;

        e.Handled = true;
    }


    internal void MouseDoubleClickHandler(object sender, RoutedEventArgs e)
    {
        if (e is MouseButtonEventArgs mb) 
        {
            var mousePos = mb.GetPosition(this);
            if (mousePos.X > 0
                && mousePos.X < ActualWidth
                && mousePos.Y >= VerticalOffset
                && mousePos.Y <= (VerticalOffset + ViewportHeight))
            {
                OnMouseDoubleClick(sender, mb); 
            }
        }
    }

    bool IsJumpingToTime = false;
    private void OnMouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (Replay == null) { return; }

        double mousePos = e.GetPosition(this).Y;

        int lineNum = (int)Math.Floor(mousePos / LineHeight);
        var logLine = ActiveLogs.Skip(lineNum).FirstOrDefault();

        if (logLine.entity != null)
        {
            EntitySelection.Set(logLine.entity);
        }

        if (logLine.frame != 0)
        {
            var t = Replay.GetTimeForFrame(logLine.frame+1);
            IsJumpingToTime = true;
            TimelineWindow.Timeline.Cursor = t;
            IsJumpingToTime = false;
        }

        e.Handled = true;
    }

    public void JumpToNextSearchResult()
    {
        if (string.IsNullOrEmpty(SearchText))
        {
            LastSelectionIndex = -1;
            SelectionSpans.Clear();
            ScrollToLine(0);
        }
        else
        {
            var search = new SearchContext(SearchText.Value);
            var nextSelectedLine = ActiveLogs.Select((item, index) => (item, index)).FirstOrDefault(x => x.index > LastSelectionIndex && search.Match($"{x.item.logHeader} {x.item.formattedLog}"));
            if (!string.IsNullOrEmpty(nextSelectedLine.item?.formattedLog))
            {
                SelectionSpans.SetSelection(nextSelectedLine.index);
                LastSelectionIndex = nextSelectedLine.index;
                ScrollToLine(LastSelectionIndex);
            }
        }

    }

    public void JumpToPreviousSearchResult()
    {
        var search = new SearchContext(SearchText.Value);
        var previousSelectedLine = ActiveLogs.Select((item, index) => (item, index)).LastOrDefault(x => x.index < LastSelectionIndex && search.Match($"{x.item.logHeader} {x.item.formattedLog}"));
        if (!string.IsNullOrEmpty(previousSelectedLine.item?.formattedLog))
        {
            SelectionSpans.SetSelection(previousSelectedLine.index);
            LastSelectionIndex = previousSelectedLine.index;
            ScrollToLine(LastSelectionIndex);
        }
    }

    private int LineHeight => 16;
    private int TextMargin => 4;
    private readonly double PIXELS_DPI = 1.25;
    private static readonly System.Globalization.CultureInfo TextCultureInfo = System.Globalization.CultureInfo.GetCultureInfo("en-us");
    private static readonly Typeface TextTypeface = new(new FontFamily("monospace"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
    private static readonly Typeface TextTypefaceBold = new(new FontFamily("monospace"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        DebugLog($"LogsControl OnRender");
        if (Replay == null) return;

        var currentFrameHighlightColor = Colors.LightBlue;
        var currentFrameHighlightBrush = new LinearGradientBrush(currentFrameHighlightColor, currentFrameHighlightColor.WithAlpha(0), new System.Windows.Point(0, 0), new System.Windows.Point(0.25, 0));

        var normalHighlightBrush = Brushes.LightGray;
        var searchMatchHighlightBrush = Brushes.White;

        var search = new SearchContext(SearchText.Value);

        System.Windows.Point drawpos = new();
        drawpos.X = 4; // Don't start right at edge
        drawpos.Y = 0;
        int currentFrame = Replay.GetFrameForTime(TimelineWindow.Timeline.Cursor);
        int lineNum = 1;
        foreach (var line in ActiveLogs)
        {
            if (drawpos.Y >= (VerticalOffset - LineHeight))
            {
                var fullLineRect = new Rect(drawpos.WithX(0), new Size(ActualWidth, LineHeight));

                // TODO: test if reusing these make a difference
                var headerText = new FormattedText($"{lineNum} {line.logHeader}", TextCultureInfo, FlowDirection.LeftToRight, TextTypeface, LineHeight - TextMargin, Brushes.Black, PIXELS_DPI);
                
                // Darken lines that don't match the search pattern
                if (!search.Empty)
                {
                    // TODO: Take the search out (although it might be slower for large logs?)
                    dc.DrawRectangle(search.Match($"{line.logHeader} {line.formattedLog}") ? searchMatchHighlightBrush : normalHighlightBrush, null, fullLineRect);
                }

                // Highlight current frame
                if (line.frame == currentFrame)
                {
                    dc.DrawRectangle(currentFrameHighlightBrush, null, fullLineRect);
                }

                dc.DrawText(headerText, drawpos);

                var logDrawPos = drawpos;
                logDrawPos.X += headerText.Width + 4;

                // TODO: test if reusing these make a difference
                bool isStarred = StarredSelection.Contains(line.entity);
                var logText = new FormattedText(line.formattedLog, TextCultureInfo, FlowDirection.LeftToRight, isStarred ? TextTypefaceBold : TextTypeface, LineHeight - TextMargin, line.color.ToBrush(), PIXELS_DPI);
                dc.DrawText(logText, logDrawPos);
            }
            if (drawpos.Y > (VerticalOffset + ViewportHeight))
            {
                break;
            }

            drawpos.Y += LineHeight;
            ++lineNum;
        }

        // Draw selection outlines
        if (!SelectionSpans.Empty)
        {
            Pen selectionOutlinePen = new Pen(Brushes.Black, 1);
            selectionOutlinePen.DashStyle = DashStyles.Dash;
            foreach ((int linestart, int lineEnd) in SelectionSpans.Spans)
            {
                int spanLines = lineEnd - linestart + 1;
                var spanRect = new Rect(new System.Windows.Point(4, linestart * LineHeight - 1), new Size(ActualWidth - 12, LineHeight * spanLines));
                dc.DrawRoundedRectangle(null, selectionOutlinePen, spanRect, 4, 4);
            }
        }

        // Draw ranges next to scroll bar
        int scrollRefWidth = 5;
        var scrollRef = new Rect(new System.Windows.Point(ActualWidth - scrollRefWidth + 1, 0), new Size(scrollRefWidth, ActualHeight));
        dc.DrawRectangle(Brushes.LightGray, null, scrollRef);

        // Draw regions on scroll bar
        int numLines = ActiveLogs.Length;
        if (numLines > 0)
        {
            int startLine = CurrentTimeActiveLogLineStart;
            int endline = CurrentTimeActiveLogLineEnd;
            int lineCount = endline - startLine + 1;

            double startPos = VerticalOffset + ViewportHeight * startLine / numLines;
            double h = Math.Max(2, ViewportHeight * lineCount / numLines);

            if (!search.Empty)
            {
                // Darken bar
                dc.DrawRectangle(Brushes.Black.WithAlpha(0.5), null, scrollRef);

                // Lighten lines that match search
                double lineHeight = Math.Max(1, ViewportHeight / numLines);
                lineNum = 1;
                foreach (var line in ActiveLogs)
                {
                    if (search.Match($"{line.logHeader} {line.formattedLog}"))
                    {
                        double lineYPos = VerticalOffset + ViewportHeight * lineNum / numLines;
                        var currentLineScrollRef = new Rect(new System.Windows.Point(ActualWidth - scrollRefWidth + 1, lineYPos), new Size(scrollRefWidth, lineHeight));
                        dc.DrawRectangle(Brushes.LightGray, null, currentLineScrollRef);
                    }
                    ++lineNum;
                }
            }

            // Current page indicator
            var currentFrameScrollRef = new Rect(new System.Windows.Point(ActualWidth - scrollRefWidth + 1, startPos), new Size(scrollRefWidth, h));
            dc.DrawRectangle(Brushes.Red.WithAlpha(0.8), null, currentFrameScrollRef);

            if (!SelectionSpans.Empty)
            {
                foreach ((int lineStart, int lineEnd) in SelectionSpans.Spans)
                {
                    double lineYPos = VerticalOffset + ViewportHeight * lineStart / numLines;
                    int spanLines = lineEnd - lineStart + 1;
                    var spanRect = new Rect(new System.Windows.Point(ActualWidth - scrollRefWidth + 1, lineYPos), new Size(scrollRefWidth, Math.Max(2, ViewportHeight * spanLines / numLines)));
                    dc.DrawRectangle(Brushes.Blue, null, spanRect);
                }
            }
        }
    }

    [System.Diagnostics.Conditional("VERBOSE")]
    public static void DebugLog(string message)
    {
        System.Diagnostics.Debug.WriteLine(message);
    }
}
