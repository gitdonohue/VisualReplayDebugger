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
    class LogsPanel : DockPanelWithToolbar, IDisposable
    {
        public LogsPanel(MainWindow mainwindow)
            : base(scrolling: true)
        {
            var replayLogsView = new ReplayLogsControlEx2(mainwindow.Replay, mainwindow.TimelineWindow, mainwindow.EntitySelection);

            this.Content = replayLogsView;
            mainwindow.ReplayChanged += (replay) => replayLogsView.Replay = replay;

            replayLogsView.ScrollOwner = this.ScrollViewer;
            this.LayoutUpdated += (o,e) => replayLogsView.ScrollingUpdated(this.ScrollViewer);

            var searchLabel = new Label() { Content = GetIcon(FontAwesomeIcon.QuestionCircleOutline) };
            ToolBar.Items.Add(searchLabel);

            var searchtext = new TextBox() { Width = 150 };
            searchtext.BindTo(replayLogsView.SearchText);
            ToolBar.Items.Add(searchtext);

            var showonlyselectedlogs = new ToggleButton() { Content = GetIcon(FontAwesomeIcon.MousePointer), ToolTip = "Show Logs for selected entities only" };
            showonlyselectedlogs.BindTo(replayLogsView.ShowSelectedLogsOnly);
            ToolBar.Items.Add(showonlyselectedlogs);

            var lockEntitySelection = new ToggleButton() { Content = GetIcon(FontAwesomeIcon.Lock), ToolTip = "Lock selection" };
            lockEntitySelection.BindTo(replayLogsView.EntitySelectionLocked);
            ToolBar.Items.Add(lockEntitySelection);

            ToolBar.Items.Add(new Separator());

            var cb = new CheckComboBoxControl("Log Categories");
            if (mainwindow.Replay != null) cb.SetItems(mainwindow.Replay.GetLogCategories(), true);
            mainwindow.ReplayChanged += (replay) => cb.SetItems(replay.GetLogCategories(), true);
            cb.Changed += () => replayLogsView.LogCategoryFilter.Set(cb.UnselectedItems);
            ToolBar.Items.Add(cb);

            var cb2 = new CheckComboBoxControl("Log Colors");
            if (mainwindow.Replay != null) cb2.SetItems(mainwindow.Replay.GetLogColors().Select(x => x.ToString()), true);
            mainwindow.ReplayChanged += (replay) => cb2.SetItems(replay.GetLogColors().Select(x=>x.ToString()), true);
            cb2.Changed += () => replayLogsView.LogColorFilter.Set(cb2.UnselectedItems.Select(x=>x.ToColor()));
            ToolBar.Items.Add(cb2);

            ToolBar.Items.Add(new Separator());

            var duplicatePanel = new Button() { Content = GetIcon(FontAwesomeIcon.LevelUp), ToolTip = "Duplicate Panel" };
            duplicatePanel.Click += (o, e) => mainwindow.DuplicatePanel(mainwindow.LogsWindow);
            ToolBar.Items.Add(duplicatePanel);

            mainwindow.CopyCalled += () => { Clipboard.SetText(replayLogsView.GetText()); };
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
