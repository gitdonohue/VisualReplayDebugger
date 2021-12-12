// (c) 2021 Charles Donohue
// This code is licensed under MIT license (see LICENSE file for details)

using FontAwesome.WPF;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using WatchedVariable;

namespace VisualReplayDebugger.Panels
{
    class EntityTimelinesPanel : DockPanelWithToolbar, IDisposable
    {
        public EntityTimelinesPanel(MainWindow mainwindow)
            : base(scrolling: false)
        {
            var entityTimelineView = new ReplayEntitiesTimelinesView(mainwindow.TimelineWindow, mainwindow.EntitySelection, mainwindow.VisibleEntities, mainwindow.Replay);
            this.Content = entityTimelineView;
            entityTimelineView.DoubleClicked += () => mainwindow.SetTimeRangeToSelected();
            
            mainwindow.ReplayChanged += (replay) => entityTimelineView.Replay = replay;

            var filtertext = new TextBox() { Width = 150 };
            filtertext.BindTo(entityTimelineView.FilterText);
            ToolBar.Items.Add(new Label() { Content = GetIcon(FontAwesomeIcon.Filter) });
            ToolBar.Items.Add(filtertext);

            var cb = new CheckComboBoxControl("Entity Categories");
            mainwindow.ReplayChanged += (replay) => { cb.SetItems(replay.GetEntityCategories(), true); };
            cb.Changed += () => entityTimelineView.TimelineEntityCategoryFilter.Set(cb.UnselectedItems);
            ToolBar.Items.Add(new Label() { Content = GetIcon(FontAwesomeIcon.Filter) });
            ToolBar.Items.Add(cb);

            ToolBar.Items.Add(new Separator());

            var showAll = new ToggleButton() { Content = GetIcon(FontAwesomeIcon.Eye), ToolTip = "Show Hidden Entities" };
            showAll.BindTo(entityTimelineView.ShowAllEntities);
            ToolBar.Items.Add(showAll);

            var zoomRange = new Button() { Content = GetIcon(FontAwesomeIcon.ArrowsH), ToolTip = "Set time range to selected" };
            zoomRange.Click += (o, e) => mainwindow.SetTimeRangeToSelected();
            ToolBar.Items.Add(zoomRange);

            var clearSelection = new Button() { Content = GetIcon(FontAwesomeIcon.WindowClose), ToolTip = "Clear selection" };
            clearSelection.Click += (o, e) => { mainwindow.EntitySelection.Clear(); };
            ToolBar.Items.Add(clearSelection);
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
