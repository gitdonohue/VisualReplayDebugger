// (c) 2021 Charles Donohue
// This code is licensed under MIT license (see LICENSE file for details)

using ReplayCapture;
using SelectionSet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;
using Timeline;
using WatchedVariable;

namespace VisualReplayDebugger
{
    class ReplayLogsControlAvalon : ICSharpCode.AvalonEdit.TextEditor
    {
        public WatchedVariable<string> SearchText { get; } = new();
        public WatchedBool ShowSelectedLogsOnly { get; } = new(false);

        public SelectionGroup<string> LogCategoryFilter { get; } = new();

        ReplayCaptureReader replay;
        public ReplayCaptureReader Replay
        {
            get => replay;
            set
            {
                replay = value;
            }
        }

        public ScrollViewer ScrollOwner { set { } } // Dummy

        private SelectionGroup<Entity> EntitySelection;
        private ITimelineWindow TimelineWindow;

        private int LineHeightTotal => (int)this.FontSize + 4;

        public ReplayLogsControlAvalon(ReplayCaptureReader replay, ITimelineWindow timelineWindow, SelectionGroup<Entity> entitySelection)
        {
            this.IsReadOnly = true;
            this.WordWrap = false;
            this.ShowLineNumbers = false;
            this.VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto;
            this.HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Hidden;

            Replay = replay;
            EntitySelection = entitySelection;
            TimelineWindow = timelineWindow;

            TimelineWindow.Changed += Refresh;
            EntitySelection.Changed += Refresh;
            SearchText.Changed += Refresh;
            ShowSelectedLogsOnly.Changed += Refresh;
            LogCategoryFilter.Changed += Refresh;

            this.IsVisibleChanged += (o, e) => Refresh();
        }

        private string LogFormat(int frame, Entity entity, string category, string log) => entity == null ? log : $"{Timeline.Timeline.TimeString(Replay.GetTimeForFrame(frame))} ({frame}) {category} {entity.Name} {log}";

        public void Refresh()
        {
            if (Replay == null) return;
            if (this.IsVisible == false) return;

            var windowRange = Replay.GetFramesForTimes(TimelineWindow.Start, TimelineWindow.End);
            var loglist = Replay.LogEntries.SubRange(windowRange);
            
            if (ShowSelectedLogsOnly && EntitySelection.SelectionSet.Count > 0)
            {
                loglist = loglist.Where(x => EntitySelection.SelectionSet.Contains(x.Item2.Item1));
            }

            if (LogCategoryFilter.SelectionSet.Any())
            {
                loglist = loglist.Where(x => !LogCategoryFilter.Contains(x.Item2.Item2));
            }

            if (!string.IsNullOrEmpty(SearchText))
            {
                var searchstrings = SearchText.Value.ToLower().Split();
                loglist = loglist.Where(x => searchstrings.All(s => x.Item2.Item2.ToLower().Contains(s) || x.Item2.Item3.ToLower().Contains(s) || x.Item2.Item1.Name.ToLower().Contains(s)));
            }

            // Show where the current time would be in the log
            int currentFrame = Replay.GetFrameForTime(TimelineWindow.Timeline.Cursor);
            loglist = loglist.InsertOnce(x => x.Item1 > currentFrame, (currentFrame, (null, string.Empty, $"{Timeline.Timeline.TimeString(TimelineWindow.Timeline.Cursor)} ({currentFrame}) -------------", ReplayCapture.Color.Black)));

            // Faster
            this.Foreground = Brushes.Black;
            this.Text = string.Join('\n', loglist.Select(x => LogFormat(x.Item1, x.Item2.Item1, x.Item2.Item2, x.Item2.Item3)));

            int cursorLineNum = loglist.TakeWhile(x => x.Item1 <= currentFrame).Count();
            this.ScrollToLine(cursorLineNum);
            //this.BringIntoView(new System.Windows.Rect(new System.Windows.Point(0, cursorLineNum * LineHeightTotal), new System.Windows.Size(1, LineHeightTotal)));
        }
    }
}
