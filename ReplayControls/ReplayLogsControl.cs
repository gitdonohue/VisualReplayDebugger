// (c) 2021 Charles Donohue
// This code is licensed under MIT license (see LICENSE file for details)

//#define USE_TEXTBLOCK

using ReplayCapture;
using SelectionSet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Timeline;
using WatchedVariable;

namespace VisualReplayDebugger
{
    public class ReplayLogsControl :
#if USE_TEXTBLOCK
        TextBlock // Allows for coloring, but no selection and slower
#else
        TextBox // Faster, allows selecting, but only one color
#endif
    {
        public WatchedVariable<string> SearchText { get; } = new();
        public WatchedBool ShowSelectedLogsOnly { get; } = new(false);

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

        public ReplayLogsControl(ReplayCaptureReader replay, ITimelineWindow timelineWindow, SelectionGroup<Entity> entitySelection)
        {
            Replay = replay;
            EntitySelection = entitySelection;
            TimelineWindow = timelineWindow;

            TimelineWindow.Changed += Refresh;
            EntitySelection.Changed += Refresh;
            SearchText.Changed += Refresh;
            ShowSelectedLogsOnly.Changed += Refresh;

            this.IsVisibleChanged += (o, e) => Refresh();
        }

        private string LogFormat(int frame, Entity entity, string category, string log) => entity == null ? log : $"{Timeline.Timeline.TimeString(Replay.GetTimeForFrame(frame))} ({frame}) {category} {entity.Name} {log}";

        public void Refresh()
        {
            if (Replay == null) return;
            if (this.IsVisible == false) return;

            var windowRange = Replay.GetFramesForTimes(TimelineWindow.Start, TimelineWindow.End);
            var loglist = Replay.LogEntries.SubRange(windowRange);

            if (!string.IsNullOrEmpty(SearchText))
            {
                var searchstrings = SearchText.Value.ToLower().Split();
                loglist = loglist.Where(x => searchstrings.All( s => x.Item2.Item2.ToLower().Contains(s) || x.Item2.Item3.ToLower().Contains(s) || x.Item2.Item1.Name.ToLower().Contains(s)) );
            }

            // Show where the current time would be in the log
            int currentFrame = Replay.GetFrameForTime(TimelineWindow.Timeline.Cursor);
            loglist = loglist.InsertOnce( x => x.Item1 > currentFrame, (currentFrame,(null, string.Empty, $"{Timeline.Timeline.TimeString(TimelineWindow.Timeline.Cursor)} ({currentFrame}) -------------", ReplayCapture.Color.Black)) );

#if USE_TEXTBLOCK
            if (EntitySelection.Empty)
            {
                // Faster
                this.Foreground = Brushes.Black;
                this.Text = string.Join('\n', loglist.Select(x => LogFormat(x.Item1, x.Item2.Item1, x.Item2.Item2, x.Item2.Item3)));
            }
            else if (this is TextBlock)
            {
                var TextBlock = this;

                // Note: this can be slow
                TextBlock.Foreground = Brushes.Gray;

                TextBlock.Inlines.Clear();
                int index = 0;
                int currentTimeLineNum = 0;
                foreach ((int frame, (Entity entity, string category, string log)) in loglist)
                {
                    var txt = LogFormat(frame, entity, category, log) + '\n';
                    if (entity == null && log.Contains("-------------")) // Hackerydoo to avoid running through enumerable just to get the current line
                    {
                        TextBlock.Inlines.Add(txt);
                        currentTimeLineNum = index;
                    }
                    else if (EntitySelection.SelectionSet.Contains(entity))
                    {
                        TextBlock.Inlines.Add(new Run(txt) { Foreground = Brushes.Black });
                    }
                    else if (!ShowSelectedLogsOnly)
                    {
                        TextBlock.Inlines.Add(txt);
                    }
                    ++index;
                }

                var rect = new System.Windows.Rect(new System.Windows.Point(0, currentTimeLineNum * LineHeightTotal), new System.Windows.Size(1, LineHeightTotal));
                this.BringIntoView(rect);
            }
#else
            this.Foreground = Brushes.Black;
            this.Text = string.Join('\n', loglist.Select(x => LogFormat(x.Item1, x.Item2.Item1, x.Item2.Item2, x.Item2.Item3)));
#endif
        }
    }
}
