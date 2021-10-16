// (c) 2021 Charles Donohue
// This code is licensed under MIT license (see LICENSE file for details)

using ReplayCapture;
using SelectionSet;
using System.Collections.Generic;
using System.Linq;
using Timeline;
using WatchedVariable;
using static ReplayCapture.ReplayCaptureReader;

namespace VisualReplayDebugger
{
    public class ReplayLogsControlEx : VirtualizedTextBox
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
                this.TextEntries.Clear();
                AllLogs.Clear();
                if (replay != null)
                {
                    AllLogs = Replay.LogEntries.Select(x => ( x.Item1, x.Item2.Item1, x.Item2.Item2, x.Item2.Item3, LogFormat(x.Item1, x.Item2.Item1, x.Item2.Item2, x.Item2.Item3))).ToList();
                }
                PreviousRange = new FrameRange();
                PreviousFrame = -1;
                Refresh();
            }
        }

        private List<(int frame, Entity entity, string category, string log, string formattedLog)> AllLogs = new();

        private SelectionGroup<Entity> EntitySelection;
        private ITimelineWindow TimelineWindow;

        public ReplayLogsControlEx(ReplayCaptureReader replay, ITimelineWindow timelineWindow, SelectionGroup<Entity> entitySelection)
        {
            Replay = replay;
            EntitySelection = entitySelection;
            TimelineWindow = timelineWindow;
            this.CanVerticallyScroll = true;

            TimelineWindow.Changed += Refresh;
            EntitySelection.Changed += Refresh;
            SearchText.Changed += Refresh;
            ShowSelectedLogsOnly.Changed += Refresh;

            this.IsVisibleChanged += (o, e) => Refresh();
        }

        private string LogFormat(int frame, Entity entity, string category, string log) => entity == null ? log : $"{Timeline.Timeline.TimeString(Replay.GetTimeForFrame(frame))} ({frame}) {category} {entity.Name} {log}";

        FrameRange PreviousRange;
        int PreviousFrame;

        public void Refresh()
        {
            if (Replay == null) return;
            if (this.IsVisible == false) return;

            var windowRange = Replay.GetFramesForTimes(TimelineWindow.Start, TimelineWindow.End);
            int currentFrame = Replay.GetFrameForTime(TimelineWindow.Timeline.Cursor);

            int topIndex = 0;
            foreach ((int frame, Entity entity, string category, string log, string formattedLog) in AllLogs)
            {
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
                    // in range
                    if (frame < PreviousRange.Start)
                    {
                        // insert at top
                        TextEntries.Insert(topIndex++, new TextEntry() { Text = formattedLog, Index = frame });
                    }
                    else if (frame > PreviousRange.End)
                    {
                        // Append to end
                        TextEntries.Add( new TextEntry() { Text = formattedLog, Index = frame });
                    }
                    else
                    {
                        // Was already there
                    }
                }
            }

            // Trim
            int toRemoveFromTop = 0;
            int toRemoveFromBottom = 0;
            foreach (var item in TextEntries)
            {
                if (item.Index < windowRange.Start) { ++toRemoveFromTop; }
                if (item.Index > windowRange.End) { ++toRemoveFromBottom; }
            }

            while (toRemoveFromTop--> 0) { TextEntries.RemoveAt(0); }
            int bottomIndex = TextEntries.Count - toRemoveFromBottom;
            while (toRemoveFromBottom-- > 0) { TextEntries.RemoveAt(bottomIndex); }

            //this.BringIndexIntoView(TextEntries.Count - 1);

            PreviousRange = windowRange;
            PreviousFrame = currentFrame;
        }
    }
}
