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
    class GraphsPanel : DockPanelWithToolbar, IDisposable
    {
        public GraphsPanel(MainWindow mainwindow)
            : base(scrolling: false)
        {
            var graphView = new ReplayGraphView(mainwindow.TimelineWindow, mainwindow.Replay, mainwindow.EntitySelection, mainwindow.ColorProvider);
            this.Content = graphView;

            mainwindow.ReplayChanged += (replay) => graphView.Replay = replay;

            var showFilled = new ToggleButton() { Content = GetIcon(FontAwesomeIcon.AreaChart), ToolTip = "Show filled" };
            showFilled.BindTo(graphView.GraphsFilled);
            ToolBar.Items.Add(showFilled);

            var showStackedByEntity = new ToggleButton() { Content = GetIcon(FontAwesomeIcon.Bars), ToolTip = "Show stacked by entity" };
            showStackedByEntity.BindTo(graphView.GraphsStackedByEntity);
            ToolBar.Items.Add(showStackedByEntity);

            var showStackedByParameter = new ToggleButton() { Content = GetIcon(FontAwesomeIcon.EllipsisV), ToolTip = "Show stacked by parameter" };
            showStackedByParameter.BindTo(graphView.GraphsStackedByParameter);
            ToolBar.Items.Add(showStackedByParameter);

            var showStackedByParameterDepth = new ToggleButton() { Content = GetIcon(FontAwesomeIcon.SortNumericAsc), ToolTip = "Show stacked by parameter depth" };
            showStackedByParameterDepth.BindTo(graphView.GraphsStackedByParameterDepth);
            ToolBar.Items.Add(showStackedByParameterDepth);

            var autoScale = new ToggleButton() { Content = GetIcon(FontAwesomeIcon.ArrowsV), ToolTip = "Autoscale" };
            autoScale.BindTo(graphView.Autoscale);
            ToolBar.Items.Add(autoScale);

            ToolBar.Items.Add(new Separator());

            var lockEntitySelection = new ToggleButton() { Content = GetIcon(FontAwesomeIcon.Lock), ToolTip = "Lock selection" };
            lockEntitySelection.BindTo(graphView.EntitySelectionLocked);
            ToolBar.Items.Add(lockEntitySelection);

            var duplicatePanel = new Button() { Content = GetIcon(FontAwesomeIcon.LevelUp), ToolTip = "Duplicate Panel" };
            duplicatePanel.Click += (o,e) => mainwindow.DuplicatePanel(mainwindow.GraphsWindow);
            ToolBar.Items.Add(duplicatePanel);
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
