// (c) 2021 Charles Donohue
// This code is licensed under MIT license (see LICENSE file for details)

using FontAwesome.WPF;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using WatchedVariable;

namespace VisualReplayDebugger.Panels
{
    /// <summary>
    /// DEPRECATED
    /// </summary>
    class LogsPanelLegacy : DockPanelWithToolbar, IDisposable
    {
        public LogsPanelLegacy(MainWindow mainwindow)
            : base(scrolling:false)
        {
            var replayLogsView = new ReplayLogsControlAvalon(null, mainwindow.TimelineWindow, mainwindow.EntitySelection);  // Not ideal, but best solution for now
            //var replayLogsView = new ReplayLogsControlEx(Replay, timelineWindow, EntitySelection); // Has potential, but not as fast as avalon
            
            this.Content = replayLogsView;            
            mainwindow.ReplayChanged += (replay) => replayLogsView.Replay = replay;

            replayLogsView.ScrollOwner = this.ScrollViewer;
            //this.LayoutUpdated += (o,e) => replayLogsView.ScrollingUpdated(this.ScrollViewer);

            var searchLabel = new Label() { Content = GetIcon(FontAwesomeIcon.QuestionCircleOutline) };
            ToolBar.Items.Add(searchLabel);

            var searchtext = new TextBox() { Width = 150 };
            searchtext.BindTo(replayLogsView.SearchText);
            ToolBar.Items.Add(searchtext);

            var showonlyselectedlogs = new ToggleButton() { Content = GetIcon(FontAwesomeIcon.MousePointer), ToolTip = "Show Logs for selected entities only" };
            showonlyselectedlogs.BindTo(replayLogsView.ShowSelectedLogsOnly);
            ToolBar.Items.Add(showonlyselectedlogs);

            ToolBar.Items.Add(new Separator());

            var cb = new CheckComboBoxControl("Log Categories");
            mainwindow.ReplayChanged += (replay) => cb.SetItems(replay.GetLogCategories(), true);
            cb.Changed += () => replayLogsView.LogCategoryFilter.Set(cb.UnselectedItems);
            ToolBar.Items.Add(cb);

            ToolBar.Items.Add(new Separator());

            var duplicatePanel = new Button() { Content = GetIcon(FontAwesomeIcon.LevelUp), ToolTip = "Duplicate Panel" };
            duplicatePanel.Click += (o, e) => mainwindow.DuplicatePanel(mainwindow.LogsWindow);
            ToolBar.Items.Add(duplicatePanel);
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
