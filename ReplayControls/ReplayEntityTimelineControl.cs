// (c) 2021 Charles Donohue
// This code is licensed under MIT license (see LICENSE file for details)

using FontAwesome.WPF;
using ReplayCapture;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Timeline;
using WatchedVariable;

namespace VisualReplayDebugger
{
    class ReplayEntityTimelineControl : UserControl, IDisposable
    {
        public Entity Entity { get; private set; }
        public ReplayCaptureReader Replay { get; private set; }
        public ITimelineWindow TimelineWindow { get; private set; }

        double startRatio;
        double endRatio;
        List<double> markerPositions;

        FormattedText formattedText;
        ReplayEntitiesTimelinesView ReplayEntitiesTimelinesView;

        public ReplayEntityTimelineControl(Entity entity, ReplayCaptureReader replay, ITimelineWindow timelineWindow, ReplayEntitiesTimelinesView view)
        {
            Entity = entity;
            Replay = replay;
            TimelineWindow = timelineWindow;
            ReplayEntitiesTimelinesView = view;

            // Precalc ratios
            var lifetime = Replay.GetEntityLifeTime(Entity);
            double totalTime = Replay.TotalTime;
            startRatio = Replay.GetTimeForFrame(lifetime.Start) / totalTime;
            endRatio = Replay.GetTimeForFrame(lifetime.End) / totalTime;

            markerPositions = new();
            foreach (int frame in Replay.LogEntries.Where(x=>x.Item2.Item1 == entity).Select(x=>x.Item1).Distinct())
            {
                markerPositions.Add( Replay.GetTimeForFrame(frame) / totalTime );
            }

            formattedText = new FormattedText($"{entity.Path}",
                System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Verdana"), 12, Brushes.Black, 1.0);

            TimelineWindow.Changed += SetDirty;

            this.MouseDown += ReplayEntitiesTimelinesView.OnChildMouseDown;
        }
        public void Dispose()
        {
            TimelineWindow.Changed -= SetDirty;

            this.MouseDown -= ReplayEntitiesTimelinesView.OnChildMouseDown;
        }

        private void SetDirty() { InvalidateVisual(); }

        Brush BackgroundBrush;
        Brush WindowBrush;
        Brush InRangeBrush;
        Pen CursorPen;
        Pen LogTickPen;
        protected override void OnRender(DrawingContext dc)
        {
            if (BackgroundBrush == null) BackgroundBrush = new SolidColorBrush() { Color = Colors.AliceBlue };
            if (WindowBrush == null) WindowBrush = new SolidColorBrush() { Color = System.Windows.Media.Color.FromArgb(32, 0, 0, 0) };
            if (InRangeBrush == null) InRangeBrush = new SolidColorBrush() { Color = Colors.LightBlue };
            if (CursorPen == null) CursorPen = new Pen(Brushes.Red, 1);
            if (LogTickPen == null) LogTickPen = new Pen(Brushes.Blue, 1);

            var bounds = new System.Windows.Rect(0, 0, ActualWidth, ActualHeight);

            // Draw background
            dc.DrawRectangle(BackgroundBrush, null, bounds);

            // Draw active region
            var activebounds = new System.Windows.Rect(startRatio * ActualWidth, 0, (endRatio - startRatio) * ActualWidth, ActualHeight);
            dc.DrawRectangle(InRangeBrush, null, activebounds);

            // Draw Selection range
            //var selectionbounds = new System.Windows.Rect((TimelineWindow.Start / Replay.TotalTime) * ActualWidth, 0, ((TimelineWindow.End - TimelineWindow.Start) / Replay.TotalTime) * ActualWidth, ActualHeight);
            //dc.DrawRectangle(WindowBrush, null, selectionbounds);
            var selectionboundsPre = new System.Windows.Rect(0, 0, (TimelineWindow.Start / Replay.TotalTime) * ActualWidth, ActualHeight);
            var selectionboundsPost = new System.Windows.Rect((TimelineWindow.End / Replay.TotalTime) * ActualWidth, 0, (1 - (TimelineWindow.End / Replay.TotalTime)) * ActualWidth, ActualHeight);
            dc.DrawRectangle(WindowBrush, null, selectionboundsPre);
            dc.DrawRectangle(WindowBrush, null, selectionboundsPost);


            // Draw log ticks
            foreach (double tickRatio in markerPositions)
            {
                double tickXPos = tickRatio * ActualWidth;
                dc.DrawLine(LogTickPen, new System.Windows.Point(tickXPos, 0), new System.Windows.Point(tickXPos, ActualHeight));
            }

            // Draw cursor
            double r = Math.Clamp(TimelineWindow.Timeline.CursorRatio, 0, 1);
            double cursorXPos = r * ActualWidth;
            dc.DrawLine(CursorPen, new System.Windows.Point(cursorXPos, 0), new System.Windows.Point(cursorXPos, ActualHeight));

            // Draw path
            //dc.DrawText(formattedText, bounds.TopLeft);

            base.OnRender(dc);
        }
    }

    class EntityTimelineViewWithLabel : DockPanel, IDisposable
    {
        public ReplayEntityTimelineControl EntityTimelineView { get; private set; }
        public Entity Entity { get; private set; }

        ReplayEntitiesTimelinesView ReplayEntitiesTimelinesView;

        public WatchedBool Visible { get; } = new(true);

        public double TimelineWidth => EntityTimelineView?.ActualWidth ?? ActualWidth;
        public double TimelineOffset => ActualWidth - TimelineWidth;

        public EntityTimelineViewWithLabel(Entity entity, ReplayCaptureReader replay, ITimelineWindow timelineWindow, int labelWidth, ReplayEntitiesTimelinesView view)
        {
            Entity = entity;

            var visibilityBtn = new ToggleButton() { Content = GetIcon(FontAwesomeIcon.Eye), Width = 16, Height = 16 };
            Visible.Changed += () => { visibilityBtn.Content = GetIcon(Visible ? FontAwesomeIcon.Eye : FontAwesomeIcon.EyeSlash); };
            visibilityBtn.BindTo(Visible);
            this.Children.Add(visibilityBtn);

            var label = new TextBlock();
            label.Padding = new Thickness() { Left = 4 };
            label.Height = 16;
            label.Width = labelWidth;
            label.Text = entity.Name;
            DockPanel.SetDock(label, Dock.Left);
            this.Children.Add(label);

            ReplayEntitiesTimelinesView = view;
            EntityTimelineView = new ReplayEntityTimelineControl(entity, replay, timelineWindow, ReplayEntitiesTimelinesView);
            DockPanel.SetDock(EntityTimelineView, Dock.Right);
            this.Children.Add(EntityTimelineView);

            this.ToolTip = $"{entity.Path}";

            ReplayEntitiesTimelinesView.VisibleEntities.Changed += VisibleEntities_Changed;
            Visible.Changed += Visible_Changed;
        }

        public void Dispose()
        {
            ReplayEntitiesTimelinesView.VisibleEntities.Changed -= VisibleEntities_Changed;
            Visible.Changed -= Visible_Changed;

            EntityTimelineView?.Dispose();
            EntityTimelineView = null;
        }

        private void VisibleEntities_Changed()
        {
            Visible.Set(ReplayEntitiesTimelinesView.VisibleEntities.Contains(Entity));
        }

        private void Visible_Changed()
        {
            if (Visible)
            {
                ReplayEntitiesTimelinesView.VisibleEntities.Add(Entity);
            }
            else
            {
                ReplayEntitiesTimelinesView.VisibleEntities.Remove(Entity);
            }
        }

        public static Image GetIcon(FontAwesomeIcon icon, int width = 12, int height = 12) => new ImageAwesome { Icon = icon, Width = width, Height = height };
    }
}
