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

        void SetDirty() { InvalidateVisual(); }

        public TimelineViewControl(ITimelineWindow timelineSelection, int preferredHeight = 30)
        {
            TimelineWindow = timelineSelection;
            Timeline = timelineSelection.Timeline;

            this.MinHeight = preferredHeight;
            this.Height = preferredHeight;
            this.MaxHeight = preferredHeight;

            this.MouseDown += OnMouseDown;
            this.MouseUp += OnMouseUp;
            this.MouseMove += OnMouseMove;
            this.MouseWheel += OnMouseWheel;
        }

        #region mouse handling
        bool IsScrubActive;
        bool IsScrollActive;
        bool MouseDidMove;
        Point MouseInitialPos;
        Point MouseLastPos;
        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            ((UIElement)e.Source).CaptureMouse();
            MouseLastPos = MouseInitialPos = e.GetPosition(this);
            MouseDidMove = false;

            if (Timeline != null)
            {
                double cursorPixelXPos = Timeline.CursorRatio * ActualWidth;
                if (Math.Abs(cursorPixelXPos - MouseLastPos.X) < 10)
                {
                    IsScrubActive = true;
                }
                else if (TimelineWindow != null)
                {
                    IsScrollActive = true;
                }
            }
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            ((UIElement)e.Source).ReleaseMouseCapture();

            if (!IsScrubActive && !MouseDidMove && Timeline != null)
            {
                // Move cursor to position
                double mouseUnitPos = Timeline.Start + MouseLastPos.X * Timeline.Range / ActualWidth;
                Timeline.Cursor = mouseUnitPos;
            }
            IsScrubActive = false;
            IsScrollActive = false;
        }
        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            Point scroll = e.GetPosition(this);

            // Clamp to control
            if (scroll.X < 0) { scroll.X = 0; }
            if (scroll.X > ActualWidth) { scroll.X = ActualWidth; }

            var delta = scroll - MouseLastPos;
            MouseLastPos = scroll;

            if (Math.Abs(delta.X) > 0)
            {
                MouseDidMove = true;
            }

            if (Timeline != null)
            {
                if (IsScrubActive)
                {
                    double mouseUnitPos = Timeline.Start + MouseLastPos.X * Timeline.Range / ActualWidth;
                    Timeline.Cursor = mouseUnitPos;

                    double offset = delta.X * Timeline.Range / ActualWidth;
                    TimelineWindow.SlideWindow(offset);
                }
                else if (IsScrollActive && TimelineWindow != null)
                {
                    double offset = delta.X * Timeline.Range / ActualWidth;
                    TimelineWindow.SlideWindow(offset);
                }
            }
        }

        private void OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (TimelineWindow != null)
            {
                double zoomFactor = 1.0 + e.Delta * 0.0005f;
                double zoomAboutPos = TimelineWindow.Start + (TimelineWindow.Range * e.GetPosition(this).X / ActualWidth);
                TimelineWindow?.ScaleWindow(zoomFactor, zoomAboutPos);
            }
        }

        #endregion //mouse handling

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

        void SetDirty() { InvalidateVisual(); }

        public TimelineWindowControl(ITimelineWindow timeLineWindow, int preferredHeight = 30)
        {
            TimelineWindow = timeLineWindow;

            this.MinHeight = preferredHeight;
            this.Height = preferredHeight;
            this.MaxHeight = preferredHeight;

            this.MouseDown += OnMouseDown;
            this.MouseUp += OnMouseUp;
            this.MouseMove += OnMouseMove;
            this.MouseWheel += OnMouseWheel;
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

        #region mouse handling
        bool IsScrollActive;
        bool IsScrubActive;
        bool MouseDidMove;
        Point MouseInitialPos;
        Point MouseLastPos;
        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            ((UIElement)e.Source).CaptureMouse();
            MouseLastPos = MouseInitialPos = e.GetPosition(this);
            MouseDidMove = false;

            if (TimelineWindow != null)
            {
                double cursorPixelXPos = CursorRatio * ActualWidth;
                if (Math.Abs(cursorPixelXPos - MouseLastPos.X) < 10)
                {
                    IsScrubActive = true;
                }
                else
                {
                    IsScrollActive = true;
                }
            }
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            ((UIElement)e.Source).ReleaseMouseCapture();

            if (!IsScrubActive && !MouseDidMove)
            {
                // Move cursor to position
                double mouseUnitPos = TimelineWindow.Start + MouseLastPos.X * TimelineWindow.Range / ActualWidth;
                CursorUnitPos = mouseUnitPos;
                SetDirty();
            }

            IsScrollActive = false;
            IsScrubActive = false;
        }
        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            Point scroll = e.GetPosition(this);

            // Clamp to control
            if (scroll.X < 0) { scroll.X = 0; }
            if (scroll.X > ActualWidth) { scroll.X = ActualWidth; }

            var delta = scroll - MouseLastPos;
            MouseLastPos = scroll;

            if (Math.Abs(delta.X) > 0)
            {
                MouseDidMove = true;
            }

            if (TimelineWindow != null)
            {
                if (IsScrollActive)
                {
                    double offset = -delta.X * TimelineWindow.Range / ActualWidth;
                    TimelineWindow.SlideWindow(offset);
                }
                else if (IsScrubActive)
                {
                    double mouseUnitPos = TimelineWindow.Start + MouseLastPos.X * timelineWindow.Range / ActualWidth;
                    CursorUnitPos = mouseUnitPos;
                    SetDirty();
                }
            }
        }

        private void OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (TimelineWindow != null)
            {
                double zoomFactor = 1.0 + e.Delta * 0.0005f;
                double zoomAboutPos = TimelineWindow.Start + (TimelineWindow.Range * e.GetPosition(this).X / ActualWidth);
                TimelineWindow?.ScaleWindow(zoomFactor, zoomAboutPos);
            }
        }
        #endregion //mouse handling

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
