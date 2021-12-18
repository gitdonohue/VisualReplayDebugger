using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using Timeline;

namespace VisualReplayDebugger.ReplayControls
{
    public class TimelineMouseControlHandler
    {
        public enum MouseScrollModes
        {
            None,
            CursorScrub,
            WindowScrub,
            StartScroll,
            EndScroll
        }
        MouseScrollModes MouseScrollMode;

        public System.Windows.Point MouseLastPos { get; private set; }

        bool MouseDidMove;
        //Point MouseInitialPos;
        //double MouseLastPosX;

        private System.Windows.Controls.UserControl control;
        private ITimelineWindow timelineWindow;

        public ITimeline Timeline => timelineWindow?.Timeline;
        public ITimelineWindow TimelineWindow => timelineWindow;

        public virtual double ControlWidth => control.ActualWidth;
        public virtual System.Windows.Point MousePos(System.Windows.Input.MouseEventArgs e) => e.GetPosition(control);

        public int SelectionMargin => 10;

        private bool windowMode;
        private bool slideWindowWhileScurbbing;

        public TimelineMouseControlHandler(ITimelineWindow timelineWindow, System.Windows.Controls.UserControl control, bool windowMode = true, bool slideWindowWhileScurbbing = true)
        {
            this.timelineWindow = timelineWindow;
            this.control = control;
            this.windowMode = windowMode;
            this.slideWindowWhileScurbbing = slideWindowWhileScurbbing;
        }

        private double MouseUnitPos(double controlPos)
        {
            if (windowMode)
            {
                return Timeline.Start + controlPos * Timeline.Range / ControlWidth;
            }
            else
            {
                return TimelineWindow.Start + controlPos * TimelineWindow.Range / ControlWidth;
            }
        }

        private double TimeScale(double pixels)
        {
            if (windowMode)
            {
                return pixels * Timeline.Range / ControlWidth;
            }
            else
            {
                return -pixels * TimelineWindow.Range / ControlWidth;
            }
        }

        public void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            ((UIElement)e.Source).CaptureMouse();
            MouseLastPos = MousePos(e);
            MouseDidMove = false;

            MouseScrollMode = MouseScrollModes.None;
            if (Timeline != null)
            {
                double cursorPixelXPos = (windowMode ? Timeline.CursorRatio : TimelineWindow.CursorRatio) * ControlWidth;
                if (Math.Abs(cursorPixelXPos - MouseLastPos.X) < SelectionMargin)
                {
                    MouseScrollMode = MouseScrollModes.CursorScrub;
                }
                else
                {
                    MouseScrollMode = MouseScrollModes.WindowScrub;

                    if (windowMode)
                    {
                        double windowStartXpos = ControlWidth * TimelineWindow.Start / Timeline.Range;
                        double windowEndXpos = ControlWidth * TimelineWindow.End / Timeline.Range;
                        if (Math.Abs(windowStartXpos - MouseLastPos.X) < SelectionMargin)
                        {
                            MouseScrollMode = MouseScrollModes.StartScroll;
                        }
                        else if (Math.Abs(windowEndXpos - MouseLastPos.X) < SelectionMargin)
                        {
                            MouseScrollMode = MouseScrollModes.EndScroll;
                        }
                    }
                }
            }
        }

        public void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            ((UIElement)e.Source).ReleaseMouseCapture();

            if (!MouseDidMove && Timeline != null)
            {
                // Move cursor to position
                Timeline.Cursor = MouseUnitPos(MouseLastPos.X);
            }
            MouseScrollMode = MouseScrollModes.None;
        }

        public void OnMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            var scroll = MousePos(e);

            // Clamp to control
            if (scroll.X < 0) { scroll.X = 0; }
            if (scroll.X > ControlWidth) { scroll.X = ControlWidth; }

            double delta = scroll.X - MouseLastPos.X;
            MouseLastPos = scroll;

            if (Math.Abs(delta) > 0)
            {
                MouseDidMove = true;
            }

            if (Timeline != null)
            {
                if (MouseScrollMode == MouseScrollModes.CursorScrub)
                {
                    if (windowMode && slideWindowWhileScurbbing)
                    {
                        double t = MouseUnitPos(MouseLastPos.X);
                        TimelineWindow.SlideWindowAndSetTime(TimeScale(delta),t);
                    }
                    else
                    {
                        Timeline.Cursor = MouseUnitPos(MouseLastPos.X) + TimeScale(delta);
                    }
                }
                else if (MouseScrollMode == MouseScrollModes.WindowScrub && TimelineWindow != null)
                {
                    TimelineWindow.SlideWindow(TimeScale(delta));
                }
                else if (MouseScrollMode == MouseScrollModes.StartScroll)
                {
                    TimelineWindow.Start += TimeScale(delta);
                }
                else if (MouseScrollMode == MouseScrollModes.EndScroll)
                {
                    TimelineWindow.End += TimeScale(delta);
                }
            }
        }

        public void OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (TimelineWindow != null)
            {
                double zoomFactor = 1.0 + e.Delta * 0.0005f;
                double zoomAboutPos = TimelineWindow.Start + (TimelineWindow.Range * e.GetPosition(control).X / ControlWidth);
                TimelineWindow?.ScaleWindow(zoomFactor, zoomAboutPos);
            }
        }
    }

}
