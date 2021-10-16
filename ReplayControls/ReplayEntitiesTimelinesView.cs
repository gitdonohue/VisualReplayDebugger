// (c) 2021 Charles Donohue
// This code is licensed under MIT license (see LICENSE file for details)

using ReplayCapture;
using SelectionSet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Timeline;
using WatchedVariable;

namespace VisualReplayDebugger
{

    class ReplayEntitiesTimelinesView : ListView
    {
        public WatchedVariable<string> SearchText { get; } = new();
        public ITimelineWindow TimelineWindow { get; private set; }

        public SelectionGroup<string> TimelineEntityCategoryFilter { get; private set; } = new();

        public int LabelWidth => 250;

        private ReplayCaptureReader replay;
        public ReplayCaptureReader Replay 
        {
            get => replay;
            set
            {
                replay = value;
                Rebuild();
            }
        }

        public event Action DoubleClicked;

        public SelectionGroup<Entity> VisibleEntities { get; private set; }

        public ReplayEntitiesTimelinesView(ITimelineWindow timelineWindow, SelectionGroup<Entity> selectionset, SelectionGroup<Entity> visibleset, ReplayCaptureReader replay)
        {
            TimelineWindow = timelineWindow;
            VisibleEntities = visibleset;

            this.HorizontalContentAlignment = System.Windows.HorizontalAlignment.Stretch;

            this.SelectionChanged += (o, e) =>
            {
                selectionset.Set(this.SelectedItems.Cast<EntityTimelineViewWithLabel>().Select(x => x.Entity));
            };
            selectionset.Changed += () =>
            {
                if (selectionset.Empty) 
                { 
                    this.UnselectAll(); 
                }
                else
                {
                    this.SetSelectedItems(this.Items.Cast<EntityTimelineViewWithLabel>().Where(x => selectionset.Contains(x.Entity)));
                }
            };

            SearchText.Changed += Rebuild;

            TimelineEntityCategoryFilter.Changed += Rebuild;

            this.MouseDoubleClick += (o, e) => DoubleClicked?.Invoke();
            this.MouseDown += OnMouseDown;
            this.MouseUp += OnMouseUp;
            this.MouseMove += OnMouseMove;
            this.MouseEnter += OnMouseEnter;
            this.MouseLeave += OnMouseLeave;

            Replay = replay;
        }

        private void Rebuild()
        {
            foreach(var it in this.Items.Cast<EntityTimelineViewWithLabel>()) { it.Dispose(); }
            this.Items.Clear();
            if (replay != null)
            {
                string[] searchstrings = null;
                if (!string.IsNullOrEmpty(SearchText))
                {
                    searchstrings = SearchText.Value.ToLower().Split();
                }

                foreach (var entity in replay.Entities)
                {
                    if (searchstrings != null && !searchstrings.All(s => entity.Path.ToLower().Contains(s))) continue;
                    if (TimelineEntityCategoryFilter.Contains(entity.CategoryName)) continue;
                    this.Items.Add(new EntityTimelineViewWithLabel(entity, replay, TimelineWindow, LabelWidth, this));
                }
            }
        }


        #region mouse handling

        private void OnMouseEnter(object sender, MouseEventArgs e)
        {
            //((UIElement)e.Source).CaptureMouse();
        }

        private void OnMouseLeave(object sender, MouseEventArgs e)
        {
            //IsScrollActive = false;
            //IsScrubActive = false;
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

        double CursorRatio => TimelineWindow != null ? ((TimelineWindow.Timeline.Range > 0) ? ((CursorUnitPos - TimelineWindow.Timeline.Start) / TimelineWindow.Timeline.Range) : 0) : 0;

        bool IsScrollActive;
        bool IsScrubActive;
        bool MouseDidMove;
        double MouseLastPosX;

        double TimelineBarOffset => (this.Items.Count > 0) ? (this.Items[0] as EntityTimelineViewWithLabel).TimelineOffset : 0;
        double TimelineBarWidth => (this.Items.Count > 0) ? (this.Items[0] as EntityTimelineViewWithLabel).TimelineWidth : ActualWidth;
        double MouseTimelinePos(MouseEventArgs e) => e.GetPosition(this).X - TimelineBarOffset - 8;

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            ((UIElement)e.Source).CaptureMouse();
            MouseLastPosX = MouseTimelinePos(e);
            MouseDidMove = false;

            if (TimelineWindow != null)
            {
                double cursorPixelXPos = CursorRatio * TimelineBarWidth;
                if (Math.Abs(cursorPixelXPos - MouseLastPosX) < 10)
                {
                    IsScrubActive = true;
                }
                else
                {
                    IsScrollActive = true;
                }
            }
        }

        public void OnChildMouseDown(object sender, MouseButtonEventArgs e)
        {
            OnMouseDown(sender, e);
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            ((UIElement)e.Source).ReleaseMouseCapture();

            if (!IsScrubActive && !MouseDidMove)
            {
                // Move cursor to position
                double mouseUnitPos = TimelineWindow.Timeline.Start + TimelineWindow.Timeline.Range * MouseLastPosX / TimelineBarWidth;
                CursorUnitPos = mouseUnitPos;
            }

            IsScrollActive = false;
            IsScrubActive = false;
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (this.Items.Count == 0) return;
            
            double scrollX = MouseTimelinePos(e);

            // Clamp to control
            if (scrollX < 0) { scrollX = 0; }
            if (scrollX > TimelineBarWidth) { scrollX = TimelineBarWidth; }

            var deltaX = scrollX - MouseLastPosX;
            MouseLastPosX = scrollX;

            if (Math.Abs(deltaX) > 0)
            {
                MouseDidMove = true;
            }

            if (TimelineWindow != null)
            {
                if (IsScrollActive)
                {
                    double offset = deltaX * TimelineWindow.Timeline.Range / TimelineBarWidth;
                    TimelineWindow.SlideWindow(offset);
                }
                else if (IsScrubActive)
                {
                    double mouseUnitPos = TimelineWindow.Timeline.Start + MouseLastPosX * TimelineWindow.Timeline.Range / TimelineBarWidth;
                    CursorUnitPos = mouseUnitPos;
                }
            }
        }
        #endregion //mouse handling

    }
}
