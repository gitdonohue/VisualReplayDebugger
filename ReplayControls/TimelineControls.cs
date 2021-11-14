// (c) 2021 Charles Donohue
// This code is licensed under MIT license (see LICENSE file for details)

using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Timeline;

namespace TimelineControls
{
    public class TimelineViewControl : UserControl
    {
        private ITimeline timeline;
        public ITimeline Timeline
        {
            get => timeline;
            set
            {
                if (value != timeline)
                {
                    if (timeline != null)
                    {
                        timeline.Changed -= SetDirty;
                    }
                    timeline = value;
                    if (timeline != null)
                    {
                        timeline.Changed += SetDirty;
                    }
                }
            }
        }

        private ITimelineWindow timelineWindow;
        public ITimelineWindow TimelineWindow
        {
            get => timelineWindow;
            set
            {
                if (value != timelineWindow)
                {
                    if (timelineWindow != null)
                    {
                        timelineWindow.Changed -= SetDirty;
                    }
                    timelineWindow = value;
                    if (timelineWindow != null)
                    {
                        timelineWindow.Changed += SetDirty;
                    }
                }
            }
        }

        VisualReplayDebugger.ReplayControls.TimelineMouseControlHandler MouseHandler;
        
        void SetDirty() { InvalidateVisual(); }

        public TimelineViewControl(ITimelineWindow timelineSelection, int preferredHeight = 30)
        {
            TimelineWindow = timelineSelection;
            Timeline = timelineSelection.Timeline;

            this.MinHeight = preferredHeight;
            this.Height = preferredHeight;
            this.MaxHeight = preferredHeight;

            MouseHandler = new(TimelineWindow,this);
            this.MouseDown += MouseHandler.OnMouseDown;
            this.MouseUp += MouseHandler.OnMouseUp;
            this.MouseMove += MouseHandler.OnMouseMove;
            this.MouseWheel += MouseHandler.OnMouseWheel;
        }

        #region drawing
        Brush BackgroundBrush;
        Brush SelectionWindowBrush;
        Pen TimelinePen;
        Pen CursorPen;

        protected override void OnRender(DrawingContext dc)
        {
            if (BackgroundBrush == null) BackgroundBrush = new SolidColorBrush() { Color = Colors.LightGray };
            if (SelectionWindowBrush == null) SelectionWindowBrush = new SolidColorBrush() { Color = Colors.AliceBlue };
            if (TimelinePen == null) TimelinePen = new Pen(Brushes.Gray, 8);
            if (CursorPen == null) CursorPen = new Pen(Brushes.Red, 8);

            var bounds = new Rect(0, 0, ActualWidth, ActualHeight);
            Point midpointLeft = new Point(0, ActualHeight / 2);
            Point midpointRight = new Point(ActualWidth, ActualHeight / 2);

            // Draw background
            dc.DrawRectangle(BackgroundBrush, null, bounds);
            
            // Draw selection window
            if (TimelineWindow != null && Timeline.Range > 0 && TimelineWindow.Range > 0)
            {
                double startPosX = Math.Clamp((TimelineWindow.Start - Timeline.Start) / Timeline.Range, 0, 1) * ActualWidth;
                double width = Math.Clamp(TimelineWindow.Range / Timeline.Range,0,1) * ActualWidth;
                Rect selectionBounds = new Rect(startPosX, 0, width, ActualHeight);
                dc.DrawRectangle(SelectionWindowBrush, null, selectionBounds);
            }

            // Center line
            dc.DrawLine(TimelinePen, midpointLeft, midpointRight);

            // Draw cursor
            double r = Math.Clamp(Timeline.CursorRatio, 0, 1);
            double xpos = r * ActualWidth;
            dc.DrawLine(CursorPen, midpointLeft, new Point(xpos, ActualHeight / 2));

            base.OnRender(dc);
        }
        #endregion //drawing
    }

    public class TimelineWindowControl : UserControl
    {
        private ITimelineWindow timelineWindow;
        public ITimelineWindow TimelineWindow
        {
            get => timelineWindow;
            set
            {
                if (value != timelineWindow)
                {
                    if (timelineWindow != null)
                    {
                        timelineWindow.Changed -= SetDirty;
                    }
                    timelineWindow = value;
                    if (timelineWindow != null)
                    {
                        timelineWindow.Changed += SetDirty;
                    }
                }
            }
        }

        VisualReplayDebugger.ReplayControls.TimelineMouseControlHandler MouseHandler;

        void SetDirty() { InvalidateVisual(); }

        public TimelineWindowControl(ITimelineWindow timeLineWindow, int preferredHeight = 30)
        {
            TimelineWindow = timeLineWindow;

            this.MinHeight = preferredHeight;
            this.Height = preferredHeight;
            this.MaxHeight = preferredHeight;


            MouseHandler = new(TimelineWindow, this, windowMode:false);
            this.MouseDown += MouseHandler.OnMouseDown;
            this.MouseUp += MouseHandler.OnMouseUp;
            this.MouseMove += MouseHandler.OnMouseMove;
            this.MouseWheel += MouseHandler.OnMouseWheel;
        }

        double CursorUnitPos
        {
            get => TimelineWindow?.Timeline?.Cursor ?? 0;
            set
            {
                if (TimelineWindow?.Timeline != null)
                {
                    TimelineWindow.Timeline.Cursor = value;
                }
            }
        }

        double CursorRatio => TimelineWindow != null ? ((TimelineWindow.Range > 0) ? ((CursorUnitPos - TimelineWindow.Start) / TimelineWindow.Range) : 0) : 0;

        #region drawing
        Brush BackgroundBrush;
        Pen TimelinePen;
        Pen CursorPen;

        protected override void OnRender(DrawingContext dc)
        {
            if (BackgroundBrush == null) BackgroundBrush = new SolidColorBrush() { Color = Colors.AntiqueWhite };
            if (TimelinePen == null) TimelinePen = new Pen(Brushes.Gray, 1);
            if (CursorPen == null) CursorPen = new Pen(Brushes.Red, 2);

            var bounds = new Rect(0, 0, ActualWidth, ActualHeight);
            Point midpointLeft = new Point(0, ActualHeight / 2);
            Point midpointRight = new Point(ActualWidth, ActualHeight / 2);

            dc.DrawRectangle(BackgroundBrush, null, bounds);

            // Center line
            dc.DrawLine(TimelinePen, midpointLeft, midpointRight);

            if (TimelineWindow != null && TimelineWindow.Range > double.Epsilon)
            {
                // tick marks
                double minTickSpacingPixels = 10;
                double unitsPerPixed = TimelineWindow.Range / ActualWidth;
                double unitsPerTick = unitsPerPixed * minTickSpacingPixels;
                double log10 = Math.Log10(unitsPerTick);
                double rounded = Math.Ceiling(log10);
                double tickUnitDist = Math.Pow(10, rounded);

                int tickLongMarks = 5;
                int tickIndex = 0;
                double currentUnitTickPos = Math.Floor(TimelineWindow.Start / (tickUnitDist * tickLongMarks)) * (tickUnitDist * tickLongMarks);
                while ( (currentUnitTickPos - TimelineWindow.End ) < double.Epsilon )
                {
                    if ( (currentUnitTickPos - TimelineWindow.Start) > double.Epsilon )
                    {
                        double tickPixelPos = ActualWidth * (currentUnitTickPos - TimelineWindow.Start) / TimelineWindow.Range;
                        if ((tickIndex % tickLongMarks) == 0)
                        {
                            dc.DrawLine(TimelinePen, new Point(tickPixelPos, ActualHeight / 8), new Point(tickPixelPos, ActualHeight * 7 / 8));
                        }
                        else
                        {
                            dc.DrawLine(TimelinePen, new Point(tickPixelPos, ActualHeight / 4), new Point(tickPixelPos, ActualHeight * 3 / 4));
                        }

                    }
                    tickIndex++;
                    currentUnitTickPos += tickUnitDist;
                }

                double r = Math.Clamp(CursorRatio, 0, 1);
                double xpos = r * ActualWidth;
                dc.DrawLine(CursorPen, midpointLeft, new Point(xpos, ActualHeight / 2));
            }

            base.OnRender(dc);
        }
        #endregion // drawing
    }

    public class TimelineController
    {
        private ITimeline Timeline;
        private Stopwatch Stopwatch;
        private DispatcherTimer Timer;
        private double CursorStartTime;

        public event Action Stopped;

        public TimelineController(ITimeline timeline)
        {
            Timeline = timeline;
            if (timeline == null) throw new ArgumentException("Timeline must be provided.");
            Timeline.Changed += Timeline_Changed;

            Stopwatch = new Stopwatch();
            Timer = new DispatcherTimer();
            Timer.Interval = TimeSpan.FromMilliseconds(33);
            Timer.Tick += Timer_Tick;
        }

        public void Play()
        {
            CursorStartTime = Timeline.Cursor;
            Stopwatch.Restart();
            Timer.Start();
        }

        public void Stop()
        {
            Timer.Stop();
            Stopwatch.Stop();
            Stopped?.Invoke();
        }

        public void Play(bool b)
        {
            if (b) Play(); else Stop();
        }

        public void Rewind()
        {
            Stop();
            Timeline.Cursor = Timeline.Start;
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            double secondsElapsed = Stopwatch.ElapsedMilliseconds / 1000.0;

            IsAffectingCursor = true;
            Timeline.Cursor = CursorStartTime + secondsElapsed;
            IsAffectingCursor = false;
            if (Timeline.Cursor >= Timeline.End)
            {
                Stop();
            }
        }

        bool IsAffectingCursor;
        private void Timeline_Changed()
        {
            if (!IsAffectingCursor)
            {
                Stop();
            }
        }
    }
}
