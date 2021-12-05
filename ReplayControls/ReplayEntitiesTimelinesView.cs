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
using TimelineControls;
using WatchedVariable;

namespace VisualReplayDebugger
{

    class ReplayEntitiesTimelinesView : ListView
    {
        public WatchedVariable<string> FilterText { get; } = new();
        public ITimelineWindow TimelineWindow { get; private set; }
        public SelectionGroup<string> TimelineEntityCategoryFilter { get; private set; } = new();
        public WatchedBool ShowAllEntities { get; } = new(false);

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

        // Mouse handler derived to handle offset
        public class TimelineMouseControlHandlerEx : ReplayControls.TimelineMouseControlHandler
        {
            private ReplayEntitiesTimelinesView ReplayEntitiesTimelinesView;
            public TimelineMouseControlHandlerEx(ITimelineWindow timelineWindow, ReplayEntitiesTimelinesView replayEntitiesTimelinesView, bool slideWindowWhileScurbbing) : base(timelineWindow,null, slideWindowWhileScurbbing: slideWindowWhileScurbbing) 
            {
                ReplayEntitiesTimelinesView = replayEntitiesTimelinesView;
            }
            public override double ControlWidth => ReplayEntitiesTimelinesView.TimelineBarWidth;
            public override System.Windows.Point MousePos(MouseEventArgs e) => e.GetPosition(ReplayEntitiesTimelinesView).WithXOffset(-ReplayEntitiesTimelinesView.TimelineBarOffset - 8);
        }
        TimelineMouseControlHandlerEx MouseHandler;

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

            FilterText.Changed += Rebuild;
            TimelineEntityCategoryFilter.Changed += Rebuild;
            ShowAllEntities.Changed += Rebuild;

            MouseHandler = new TimelineMouseControlHandlerEx(TimelineWindow, this, slideWindowWhileScurbbing:false);
            this.MouseDown += MouseHandler.OnMouseDown;
            this.MouseUp += MouseHandler.OnMouseUp;
            this.MouseMove += MouseHandler.OnMouseMove;
            this.MouseWheel += MouseHandler.OnMouseWheel;

            this.MouseDoubleClick += (o, e) => DoubleClicked?.Invoke();

            Replay = replay;
        }

        private void Rebuild()
        {
            foreach(var it in this.Items.Cast<EntityTimelineViewWithLabel>()) { it.Dispose(); }
            this.Items.Clear();
            if (replay != null)
            {
                var search = new SearchContext(FilterText.Value);
                foreach (var entity in replay.Entities)
                {
                    if (!ShowAllEntities && !entity.HasTransforms && !entity.HasParameters && !entity.HasNumericParameters && !entity.HasLogsPastFirstFrame & !entity.HasMesh) continue;
                    if (TimelineEntityCategoryFilter.Contains(entity.CategoryName)) continue;
                    if (!search.Empty && !(search.Match(entity.Name) || search.Match(entity.Path)) ) continue;
                    this.Items.Add(new EntityTimelineViewWithLabel(entity, replay, TimelineWindow, LabelWidth, this));
                }
            }
        }

        double TimelineBarOffset => (this.Items.Count > 0) ? (this.Items[0] as EntityTimelineViewWithLabel).TimelineOffset : 0;
        double TimelineBarWidth => (this.Items.Count > 0) ? (this.Items[0] as EntityTimelineViewWithLabel).TimelineWidth : ActualWidth;

        public void OnChildMouseDown(object sender, MouseButtonEventArgs e)
        {
            MouseHandler.OnMouseDown(sender, e);
        }
    }
}
