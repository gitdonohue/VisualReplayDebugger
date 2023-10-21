// (c) 2021 Charles Donohue
// This code is licensed under MIT license (see LICENSE file for details)

using FontAwesomeIcon = FontAwesome.Sharp.IconChar;
using System;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace VisualReplayDebugger.Panels;

class PropertiesPanel : DockPanelWithToolbar, IDisposable
{
    public PropertiesPanel(MainWindow mainwindow)
        : base(scrolling: true)
    {
        var entityPropertiesView = new EntityPropertiesControl(mainwindow.Replay, mainwindow.TimelineWindow, mainwindow.EntitySelection);
        this.Content = entityPropertiesView;
        
        mainwindow.ReplayChanged += (replay) => entityPropertiesView.Replay = replay;

        var filterLabel = new Label() { Content = IconProvider.GetIcon(FontAwesomeIcon.Filter) };
        ToolBar.Items.Add(filterLabel);

        var filtertext = new TextBox() { Width = 150 };
        filtertext.BindTo(entityPropertiesView.FilterText);
        ToolBar.Items.Add(filtertext);

        var lockEntitySelection = new ToggleButton() { Content = IconProvider.GetIcon(FontAwesomeIcon.Lock), ToolTip = "Lock selection" };
        lockEntitySelection.BindTo(entityPropertiesView.EntitySelectionLocked);
        ToolBar.Items.Add(lockEntitySelection);

        ToolBar.Items.Add(new Separator());

        var duplicatePanel = new Button() { Content = IconProvider.GetIcon(FontAwesomeIcon.AngleUp), ToolTip = "Duplicate Panel" };
        duplicatePanel.Click += (o, e) => mainwindow.DuplicatePanel(mainwindow.PropertiesWindow);
        ToolBar.Items.Add(duplicatePanel);
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }
}
