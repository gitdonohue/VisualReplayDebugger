// (c) 2021 Charles Donohue
// This code is licensed under MIT license (see LICENSE file for details)

using System;

namespace Timeline
{
    public interface ITimeline
    {
        double Start { get; set; }
        double End { get; set; }
        double Cursor { get; set; }

        event Action Changed;

        double Range => End - Start;
        double CursorRatio => (Range > 0) ? ((Cursor - Start) / Range) : 0;
    }

    public interface ITimelineWindow
    {
        ITimeline Timeline { get; set; }
        double Start { get; set; }
        double End { get; set; }

        double Min => Timeline?.Start ?? double.NegativeInfinity;
        double Max => Timeline?.End ?? double.PositiveInfinity;
        double Range => End - Start;
        double CursorRatio => (Range > 0 && Timeline != null) ? ((Timeline.Cursor - Start) / Range) : 0;

        event Action Changed;

        void ScaleWindow(double scaleFactor, double center);
        void SlideWindow(double offset);
    }

    #region implementations
    public class Timeline : ITimeline
    {
        public double Start { get => start; set { start = SafeVal(value); if (start >= end) start = end; CallChanged(); } }
        double start = 0;

        public double End { get => end; set { end = SafeVal(value); if (end <= start) end = start; CallChanged(); } }
        double end = 1;

        public double Cursor { get => cursor; set { cursor = SafeVal(value); if (cursor < start) cursor = start; if (cursor > end) cursor = end; CallChanged(); } }
        double cursor;

        public event Action Changed;

        private void CallChanged() { Changed?.Invoke(); }

        public static double SafeVal(double x) => Double.IsNaN(x) ? 0 : x;

        public static string TimeString(double t) => TimeSpan.FromSeconds(t).ToString(@"mm\:ss\.fff");
    }

    public class TimelineWindow : ITimelineWindow
    {
        public ITimeline Timeline
        {
            get => timeline;
            set
            {
                if (timeline != null)
                {
                    timeline.Changed -= CallChanged;
                }
                timeline = value;
                if (timeline != null)
                {
                    timeline.Changed += CallChanged;
                }
                Clamp();
                CallChanged();
            }
        }
        ITimeline timeline;

        public double Start { get => start; set { start = value; Clamp(); CallChanged(); } }
        double start;

        public double End { get => end; set { end = value; Clamp(); CallChanged(); } }
        double end = 1;

        double Min => Timeline?.Start ?? double.NegativeInfinity;
        double Max => Timeline?.End ?? double.PositiveInfinity;

        public event Action Changed;

        public TimelineWindow(ITimeline timeline)
        {
            Timeline = timeline;
            start = timeline.Start;
            end = timeline.End;
        }

        public void Fill()
        {
            start = timeline.Start;
            end = timeline.End;
            CallChanged();
        }

        private void Clamp()
        {
            if (start < Min) start = Min;
            if (start > Max) start = Max;
            if (end < Min) end = Min;
            if (end > Max) end = Max;
            if (start > end) start = end;

        }
        private void CallChanged() { Changed?.Invoke(); }

        public void ScaleWindow(double scaleFactor, double center)
        {
            if (scaleFactor > 0 && (end-start) > 1E-6)
            {
                double d = end - center;
                double r = end - start;
                if (Math.Abs(d) > double.Epsilon)
                {
                    end = center + d * scaleFactor;
                    start = end - r * scaleFactor;
                }

            }
            Clamp();
            CallChanged();
        }

        public void SlideWindow(double offset)
        {
            if (offset == 0) return;
            if (offset < 0)
            {
                if ((start + offset) < Min) offset = Min - start;
            }
            else
            {
                if ((end + offset) > Max) offset = Max - end;
            }
            start += offset;
            end += offset;
            CallChanged();
        }
    }

    #endregion //implementations

}
