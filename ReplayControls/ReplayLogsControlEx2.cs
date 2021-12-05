// (c) 2021 Charles Donohue
// This code is licensed under MIT license (see LICENSE file for details)

using ReplayCapture;
using SelectionSet;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Timeline;
using WatchedVariable;
using static ReplayCapture.ReplayCaptureReader;

namespace VisualReplayDebugger
{
    // Much faster than than AnavlonEdit or VirtualizingStackPanel, simpler color management and overlay.
    // Downside: no selection (for now)
    public class ReplayLogsControlEx2 : UserControl, IDisposable
    {
        public WatchedVariable<string> FilterText { get; } = new();
        public WatchedBool ShowSelectedLogsOnly { get; } = new(false);
        public WatchedBool EntitySelectionLocked { get; } = new(false);
        private IEnumerable<Entity> SelectedEntities => EntitySelectionLocked ? LockedSelection : EntitySelection.SelectionSet;
        private List<Entity> LockedSelection = new();
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
                ActiveLogs.Clear();
                AllLogs.Clear();
                if (replay != null)
                {
                    AllLogs = Replay.LogEntries.Select(x => (x.Item1, x.Item2.Item1, x.Item2.Item2, x.Item2.Item3, LogHeaderFormat(x.Item1, x.Item2.Item1, x.Item2.Item2, x.Item2.Item3), LogFormat(x.Item1, x.Item2.Item1, x.Item2.Item2, x.Item2.Item3), x.Item2.Item4)).ToList();
                }
                EntitySelectionLocked.Set(false);
                RefreshLogs();
            }
        }

        private List<(int frame, Entity entity, string category, string log, string logHeader, string formattedLog, ReplayCapture.Color color)> AllLogs = new();
        private List<(int frame, Entity entity, string category, string log, string logHeader, string formattedLog, ReplayCapture.Color color)> ActiveLogs = new();

        public record TextEntry
        {
            public string Text;
            public int Index;
        }

        private SelectionGroup<Entity> EntitySelection;
        private ITimelineWindow TimelineWindow;

        public ReplayLogsControlEx2(ReplayCaptureReader replay, ITimelineWindow timelineWindow, SelectionGroup<Entity> entitySelection)
        {
            Replay = replay;
            EntitySelection = entitySelection;
            TimelineWindow = timelineWindow;

            TimelineWindow.Changed += TimelineWindow_Changed;
            EntitySelection.Changed += EntitySelection_Changed;
            EntitySelectionLocked.Changed += RefreshLogs;
            FilterText.Changed += RefreshLogs;
            ShowSelectedLogsOnly.Changed += RefreshLogs;
            LogCategoryFilter.Changed += RefreshLogs;
            LogColorFilter.Changed += RefreshLogs;
            this.IsVisibleChanged += (o, e) => RefreshLogs();

            this.MouseDoubleClick += OnMouseDoubleClick;

            // Selected only and selection lock buttons can change their relative states.
            ShowSelectedLogsOnly.Changed += () => { if (!ShowSelectedLogsOnly) { EntitySelectionLocked.Set(false); } };
            EntitySelectionLocked.Changed += () => { if (EntitySelectionLocked) { ShowSelectedLogsOnly.Set(true); } };
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public string GetText()
        {
            return string.Join('\n',ActiveLogs.Select(x=> $"{x.logHeader} {x.formattedLog}"));
        }

        private string LogHeaderFormat(int frame, Entity entity, string category, string log) => entity == null ? log : $"{Timeline.Timeline.TimeString(Replay.GetTimeForFrame(frame))} ({frame}) [{entity.Name}] -";
        private string LogFormat(int frame, Entity entity, string category, string log) => log;

        IEnumerable<(int frame, Entity entity, string category, string log, string logHeader, string formattedLog, ReplayCapture.Color color)> CollectSelectedLogs()
        {
            var search = new SearchContext(FilterText.Value);
            return AllLogs.Where(x => (!ShowSelectedLogsOnly || SelectedEntities.Contains(x.entity))
                && (LogCategoryFilter.Empty || !LogCategoryFilter.Contains(x.category))
                && (LogColorFilter.Empty || !LogColorFilter.Contains(x.color))
                && (search.Match(x.logHeader)||search.Match(x.formattedLog))
            );
        }

        private IEnumerable<(int frame, Entity entity, string category, string log, string logHeader, string formattedLog, ReplayCapture.Color color)> CollectLogs()
        {
            var windowRange = Replay.GetFramesForTimes(TimelineWindow.Start, TimelineWindow.End);
            int currentFrame = Replay.GetFrameForTime(TimelineWindow.Timeline.Cursor);

            var currentFrameEntry = (currentFrame, default(Entity), string.Empty, string.Empty, $"{Timeline.Timeline.TimeString(Replay.GetTimeForFrame(currentFrame))} ({currentFrame})", "------------------------", ReplayCapture.Color.Black );

            bool currentTimeEntryShown = false;
            foreach (var logEntry in CollectSelectedLogs())
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

        public void RefreshLogs()
        {
            if (Replay == null) return;
            if (this.IsVisible == false) return;

            ActiveLogs.Clear();
            ActiveLogs.AddRange(CollectLogs());

            double h = ActiveLogs.Count * LineHeight;
            if (h < ViewportHeight) h = ViewportHeight;
            this.Height = h;
            InvalidateVisual();
        }

        double _viewportHeight = 0;
        double ViewportHeight
        {
            get => _viewportHeight;
            set
            {
                if (value != _viewportHeight)
                {
                    _viewportHeight = value;
                    InvalidateVisual();
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
                    InvalidateVisual();
                }
            }
        }


        int _lastFrame = -1;
        private void TimelineWindow_Changed()
        {
            // Auto scrolling

            int currentFrame = Replay?.GetFrameForTime(TimelineWindow.Timeline.Cursor) ?? 0;
            int lineNumAtCursorFrame = 0;
            if (_lastFrame != currentFrame)
            {
                foreach (var log in ActiveLogs)
                {
                    if (log.frame > currentFrame) break;
                    ++lineNumAtCursorFrame;
                }
                _lastFrame = currentFrame;
            }

            double verticalOffset = lineNumAtCursorFrame * LineHeight - (ViewportHeight-LineHeight);
            if (!IsJumpingToTime)
            {
                //System.Diagnostics.Debug.WriteLine($"Log jump to line {lineNumAtCursorFrame} at {verticalOffset}");
                if (verticalOffset>=0) ScrollOwner.ScrollToVerticalOffset(verticalOffset);
            }

            RefreshLogs();
        }

        private void EntitySelection_Changed()
        {
            if (!EntitySelectionLocked)
            {
                LockedSelection.Clear();
                LockedSelection.AddRange(EntitySelection.SelectionSet);
                RefreshLogs();
            }
        }

        public void ScrollingUpdated(ScrollViewer scrollViewer)
        {
            VerticalOffset = scrollViewer.VerticalOffset;
            ViewportHeight = scrollViewer.ViewportHeight;
        }

        bool IsJumpingToTime;
        private void OnMouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (Replay == null) { return; }

            double mousePos = e.GetPosition(this).Y;// + VerticalOffset;

            int lineNum = (int)Math.Floor(mousePos / LineHeight);
            var logLine = ActiveLogs.Skip(lineNum).FirstOrDefault();
            if (logLine.frame != 0)
            {
                var t = Replay.GetTimeForFrame(logLine.frame+1);
                IsJumpingToTime = true;
                TimelineWindow.Timeline.Cursor = t;
                IsJumpingToTime = false;
            }
        }

        //private Rect Bounds => new Rect(0, 0, ActualWidth, ActualHeight);
        private int LineHeight => 16;
        private int TextMargin => 4;
        private double PIXELS_DPI = 1.25;
        private static System.Globalization.CultureInfo TextCultureInfo = System.Globalization.CultureInfo.GetCultureInfo("en-us");
        private static Typeface TextTypeface = new Typeface("Lucida");

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            if (Replay == null) return;

            var currentFrameHighlightColor = Colors.LightBlue;
            var currentFrameHighlightBrush = new LinearGradientBrush(currentFrameHighlightColor, currentFrameHighlightColor.WithAlpha(0), new System.Windows.Point(0, 0), new System.Windows.Point(0.25, 0));

            System.Windows.Point drawpos = new();
            drawpos.X = 4; // Don't start right at edge
            drawpos.Y = 0;
            int currentFrame = Replay.GetFrameForTime(TimelineWindow.Timeline.Cursor);
            int lineNum = 1;
            foreach (var line in ActiveLogs)
            {
                if (drawpos.Y >= (VerticalOffset - LineHeight))
                {
                    // Highlight current frame
                    if (line.frame == currentFrame)
                    {
                        var r = new Rect(drawpos.WithX(0), new Size(ActualWidth, LineHeight));
                        dc.DrawRectangle(currentFrameHighlightBrush, null, r);
                    }

                    var headerText = new FormattedText($"{lineNum} {line.logHeader}", TextCultureInfo, FlowDirection.LeftToRight, TextTypeface, LineHeight - TextMargin, Brushes.Black, PIXELS_DPI);
                    dc.DrawText(headerText, drawpos);

                    var logDrawPos = drawpos;
                    logDrawPos.X += headerText.Width + 4;
                    var logText = new FormattedText(line.formattedLog, TextCultureInfo, FlowDirection.LeftToRight, TextTypeface, LineHeight - TextMargin, line.color.ToBrush(), PIXELS_DPI);
                    dc.DrawText(logText, logDrawPos);
                }
                if (drawpos.Y > (VerticalOffset + ViewportHeight))
                {
                    break;
                }

                drawpos.Y += LineHeight;
                ++lineNum;
            }

            // Draw ranges next to scroll bar
            int scrollRefWidth = 5;
            var scrollRef = new Rect(new System.Windows.Point(ActualWidth - scrollRefWidth + 1, 0), new Size(scrollRefWidth, ActualHeight));
            dc.DrawRectangle(Brushes.LightGray, null, scrollRef);

            int numLines = ActiveLogs.Count;
            if (numLines > 0)
            {
                int startLine = ActiveLogs.FindIndex(x => x.frame == currentFrame);
                int endline = ActiveLogs.FindLastIndex(x => x.frame == currentFrame);
                int lineCount = endline - startLine + 1;

                double startPos = VerticalOffset + ViewportHeight * startLine / numLines;
                double h = Math.Max(2, ViewportHeight * lineCount / numLines);
                var currentFrameScrollRef = new Rect(new System.Windows.Point(ActualWidth - scrollRefWidth + 1, startPos), new Size(scrollRefWidth, h));
                dc.DrawRectangle(Brushes.Red, null, currentFrameScrollRef);
            }
        }
    }
}
