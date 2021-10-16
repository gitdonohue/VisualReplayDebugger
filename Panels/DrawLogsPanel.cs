// (c) 2021 Charles Donohue
// This code is licensed under MIT license (see LICENSE file for details)

using FontAwesome.WPF;
using System;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using WatchedVariable;

namespace VisualReplayDebugger.Panels
{
    class DrawLogsPanel : DockPanelWithToolbar, IDisposable
    {
        public DrawLogsPanel(MainWindow mainwindow)
            : base(scrolling: false)
        {
            var drawLogsView = new ReplayDrawLogsControl(mainwindow.Replay, mainwindow.TimelineWindow, mainwindow.EntitySelection);
            this.Content = drawLogsView;
            mainwindow.ReplayChanged += (replay) => drawLogsView.Replay = replay;

            var searchLabel = new Label() { Content = GetIcon(FontAwesomeIcon.QuestionCircleOutline) };
            ToolBar.Items.Add(searchLabel);

            var searchtext = new TextBox() { Width = 150 };
            searchtext.BindTo(drawLogsView.SearchText);
            ToolBar.Items.Add(searchtext);

            var showonlyselectedlogs = new ToggleButton() { Content = GetIcon(FontAwesomeIcon.MousePointer), ToolTip = "Show Logs for selected entities only" };
            showonlyselectedlogs.BindTo(drawLogsView.ShowSelectedLogsOnly);
            ToolBar.Items.Add(showonlyselectedlogs);

            var showDrawsInRange = new ToggleButton() { Content = GetIcon(FontAwesomeIcon.ArrowsH), ToolTip = "Show all draw calls in range" };
            showDrawsInRange.BindTo(drawLogsView.ShowAllDrawsInRange);
            ToolBar.Items.Add(showDrawsInRange);
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
