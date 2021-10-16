// (c) 2021 Charles Donohue
// This code is licensed under MIT license (see LICENSE file for details)

using FontAwesome.WPF;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
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

            var clearSelection = new Button() { Content = GetIcon(FontAwesomeIcon.WindowClose), ToolTip = "Clear selection" };
            clearSelection.Click += (o, e) => { mainwindow.EntitySelection.Clear(); };
            ToolBar.Items.Add(clearSelection);

            var searchLabel = new Label() { Content = GetIcon(FontAwesomeIcon.QuestionCircleOutline) };
            ToolBar.Items.Add(searchLabel);

            var searchtext = new TextBox() { Width = 150 };
            searchtext.BindTo(entityTimelineView.SearchText);
            ToolBar.Items.Add(searchtext);

            var zoomRange = new Button() { Content = GetIcon(FontAwesomeIcon.ArrowsH), ToolTip = "Set time range to selected" };
            zoomRange.Click += (o, e) => mainwindow.SetTimeRangeToSelected();
            ToolBar.Items.Add(zoomRange);

            ToolBar.Items.Add(new Separator());

            var cb = new CheckComboBoxControl("Entity Categories");
            mainwindow.ReplayChanged += (replay) => { cb.SetItems(replay.GetEntityCategories(), true); };
            cb.Changed += () => entityTimelineView.TimelineEntityCategoryFilter.Set(cb.UnselectedItems);
            ToolBar.Items.Add(cb);
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
