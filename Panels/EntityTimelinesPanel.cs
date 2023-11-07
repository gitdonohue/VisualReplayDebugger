// (c) 2021 Charles Donohue
// This code is licensed under MIT license (see LICENSE file for details)

using FontAwesomeIcon = FontAwesome.Sharp.IconChar;
using System;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;


namespace VisualReplayDebugger.Panels;

class EntityTimelinesPanel : DockPanelWithToolbar, IDisposable
{
    public EntityTimelinesPanel(MainWindow mainwindow)
        : base(scrolling: false)
    {
        var entityTimelineView = new ReplayEntitiesTimelinesView(mainwindow.TimelineWindow, mainwindow.EntitySelection, mainwindow.HiddenEntities, mainwindow.StarredEntities, mainwindow.Replay);
        this.Content = entityTimelineView;
        entityTimelineView.DoubleClicked += () => mainwindow.SetTimeRangeToSelected();
        
        mainwindow.ReplayChanged += (replay) => entityTimelineView.Replay = replay;

        var filtertext = new TextBox() { Width = 150 };
        filtertext.BindTo(entityTimelineView.FilterText);
        ToolBar.Items.Add(new Label() { Content = IconProvider.GetIcon(FontAwesomeIcon.Filter) });
        ToolBar.Items.Add(filtertext);

        var cb = new CheckComboBoxControl("Entity Categories");
        mainwindow.ReplayChanged += (replay) => { cb.SetItems(replay.EntityCategories, true); };
        cb.Changed += () => entityTimelineView.TimelineEntityCategoryFilter.Set(cb.UnselectedItems);
        ToolBar.Items.Add(new Label() { Content = IconProvider.GetIcon(FontAwesomeIcon.Filter) });
        ToolBar.Items.Add(cb);

        ToolBar.Items.Add(new Separator());

        //var showAll = new ToggleButton() { Content = IconProvider.GetIcon(FontAwesomeIcon.Eye), ToolTip = "Show Hidden Entities" };
        //showAll.BindTo(entityTimelineView.ShowAllEntities);
        //ToolBar.Items.Add(showAll);

        var showStarredOnly = new ToggleButton() { Content = IconProvider.GetIcon(FontAwesomeIcon.Star), ToolTip = "Show starred entities only" };
        showStarredOnly.BindTo(entityTimelineView.ShowStarredEntitiesOnly);
        ToolBar.Items.Add(showStarredOnly);

        var zoomRange = new Button() { Content = IconProvider.GetIcon(FontAwesomeIcon.ArrowsAltH), ToolTip = "Set time range to selected" };
        zoomRange.Click += (o, e) => mainwindow.SetTimeRangeToSelected();
        ToolBar.Items.Add(zoomRange);

        var clearSelection = new Button() { Content = IconProvider.GetIcon(FontAwesomeIcon.WindowClose), ToolTip = "Clear selection" };
        clearSelection.Click += (o, e) => { mainwindow.EntitySelection.Clear(); };
        ToolBar.Items.Add(clearSelection);
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }
}
