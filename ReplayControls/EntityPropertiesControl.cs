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
using Timeline;
using WatchedVariable;

namespace VisualReplayDebugger
{
    class EntityPropertiesControl : TextBox
    {
        public WatchedVariable<string> FilterText { get; } = new();
        public WatchedBool EntitySelectionLocked { get; } = new(false);
        private SelectionGroup<Entity> EntitySelection;
        private IEnumerable<Entity> SelectedEntities => EntitySelectionLocked ? LockedSelection : EntitySelection.SelectionSet;
        private List<Entity> LockedSelection = new();

        private ITimelineWindow TimelineWindow;

        public ReplayCaptureReader replay;
        public ReplayCaptureReader Replay
        {
            get => replay;
            set
            {
                replay = value;
                EntitySelectionLocked.Set(false);
            }
        }

        public EntityPropertiesControl(ReplayCaptureReader replay, ITimelineWindow timelineWindow, SelectionGroup<Entity> entitySelection)
        {
            this.IsReadOnly = true;
            this.VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto;
            this.HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Hidden;

            Replay = replay;
            EntitySelection = entitySelection;
            TimelineWindow = timelineWindow;

            TimelineWindow.Changed += Refresh;
            FilterText.Changed += Refresh;
            EntitySelectionLocked.Changed += Refresh;
            EntitySelection.Changed += EntitySelection_Changed;
        }

        public void Refresh()
        {
            if (Replay == null) return;

            int frame = Replay.GetFrameForTime(TimelineWindow.Timeline.Cursor);
            var filter = new SearchContext(FilterText.Value);
            var txt = string.Join("\n----------\n",
                SelectedEntities.Select(x => string.Join('\n', Replay.AllParametersAt(x, frame).Where(s=>filter.Match(s.name)).Select(s => $"{s.name}\t{s.val}"))));
            this.Text = txt;
        }

        private void EntitySelection_Changed()
        {
            if (!EntitySelectionLocked)
            {
                Refresh();
                LockedSelection.Clear();
                LockedSelection.AddRange(EntitySelection.SelectionSet);
            }
        }
    }
}
