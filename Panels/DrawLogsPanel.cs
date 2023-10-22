// (c) 2021 Charles Donohue
// This code is licensed under MIT license (see LICENSE file for details)

using FontAwesomeIcon = FontAwesome.Sharp.IconChar;
using System;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace VisualReplayDebugger.Panels;

class DrawLogsPanel : DockPanelWithToolbar, IDisposable
{
    public DrawLogsPanel(MainWindow mainwindow)
        : base(scrolling: false)
    {
        var drawLogsView = new ReplayDrawLogsControl(mainwindow.Replay, mainwindow.TimelineWindow, mainwindow.EntitySelection);
        this.Content = drawLogsView;
        mainwindow.ReplayChanged += (replay) => drawLogsView.Replay = replay;

        var filterLabel = new Label() { Content = IconProvider.GetIcon(FontAwesomeIcon.Filter) };
        ToolBar.Items.Add(filterLabel);

        var filtertext = new TextBox() { Width = 150 };
        filtertext.BindTo(drawLogsView.FilterText);
        ToolBar.Items.Add(filtertext);

        var showonlyselectedlogs = new ToggleButton() { Content = IconProvider.GetIcon(FontAwesomeIcon.MousePointer), ToolTip = "Show Logs for selected entities only" };
        showonlyselectedlogs.BindTo(drawLogsView.ShowSelectedLogsOnly);
        ToolBar.Items.Add(showonlyselectedlogs);

        var showDrawsInRange = new ToggleButton() { Content = IconProvider.GetIcon(FontAwesomeIcon.ArrowsAltH), ToolTip = "Show all draw calls in range" };
        showDrawsInRange.BindTo(drawLogsView.ShowAllDrawsInRange);
        ToolBar.Items.Add(showDrawsInRange);
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }
}
