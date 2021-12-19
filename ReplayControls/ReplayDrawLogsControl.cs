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
using System.Windows.Documents;
using System.Windows.Media;
using Timeline;
using WatchedVariable;

namespace VisualReplayDebugger
{
    class ReplayDrawLogsControl : ICSharpCode.AvalonEdit.TextEditor
    {
        public WatchedVariable<string> FilterText { get; } = new();
        public WatchedBool ShowSelectedLogsOnly { get; } = new(false);
        public WatchedBool ShowAllDrawsInRange { get; } = new(false);

        ReplayCaptureReader replay;
        public ReplayCaptureReader Replay
        {
            get => replay;
            set
            {
                replay = value;
            }
        }


        private SelectionGroup<Entity> EntitySelection;
        private ITimelineWindow TimelineWindow;

        public ReplayDrawLogsControl(ReplayCaptureReader replay, ITimelineWindow timelineWindow, SelectionGroup<Entity> entitySelection)
        {
            this.IsReadOnly = true;
            this.WordWrap = false;
            this.ShowLineNumbers = true;
            this.VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto;
            this.HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Hidden;

            Replay = replay;
            EntitySelection = entitySelection;
            TimelineWindow = timelineWindow;

            TimelineWindow.Changed += Refresh;
            EntitySelection.Changed += Refresh;
            FilterText.Changed += Refresh;
            ShowSelectedLogsOnly.Changed += Refresh;
            ShowAllDrawsInRange.Changed += Refresh;

            this.IsVisibleChanged += (o,e) => Refresh();
        }

        private string DrawCommandLogFormat(int frame, ReplayCaptureReader.EntityDrawCommand cmd) => cmd.type != ReplayCaptureReader.EntityDrawCommandType.None ?
            $"{Timeline.Timeline.TimeString(Replay.GetTimeForFrame(frame))} ({frame}) {cmd.entity?.Name ?? "None"} {cmd.entity?.Path ?? "None"} {cmd.category} {cmd.type} {cmd.color} {cmd.verts?.Length ?? 0} ({cmd.Pos.X},{cmd.Pos.Y},{cmd.Pos.Z})"
            : $"{Timeline.Timeline.TimeString(Replay.GetTimeForFrame(frame))} ({frame}) ------------------------------";

        public void Refresh()
        {
            if (Replay == null) return;
            if (this.IsVisible == false) return;

            var windowRange = Replay.GetFramesForTimes(TimelineWindow.Start, TimelineWindow.End);

            int currentFrame = Replay.GetFrameForTime(TimelineWindow.Timeline.Cursor);
            if (!ShowAllDrawsInRange)
            {
                windowRange = new ReplayCaptureReader.FrameRange() { Start = currentFrame, End = currentFrame };
            }

            var drawLogs = Replay.DrawCommands.SubRange(windowRange);

            if (ShowSelectedLogsOnly)
            {
                drawLogs = drawLogs.Where(x => EntitySelection.Contains(x.val.entity));
            }

            // Show where the current time would be in the log
            if (ShowAllDrawsInRange)
            {
                drawLogs = drawLogs.InsertOnce(x => x.Item1 > currentFrame, (currentFrame, new ReplayCaptureReader.EntityDrawCommand()));
            }
            
            this.Foreground = Brushes.Black;
            this.Text = string.Join('\n', drawLogs.Select(x => DrawCommandLogFormat(x.frame,x.val)));
        }
    }
}
