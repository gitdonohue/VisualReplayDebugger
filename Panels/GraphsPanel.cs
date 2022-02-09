// (c) 2021 Charles Donohue
// This code is licensed under MIT license (see LICENSE file for details)

using FontAwesomeIcon = FontAwesome.Sharp.IconChar;
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

            var showFilled = new ToggleButton() { Content = IconProvider.GetIcon(FontAwesomeIcon.Fill), ToolTip = "Show filled" };
            showFilled.BindTo(graphView.GraphsFilled);
            ToolBar.Items.Add(showFilled);

            var showStackedByEntity = new ToggleButton() { Content = IconProvider.GetIcon(FontAwesomeIcon.Bars), ToolTip = "Show stacked by entity" };
            showStackedByEntity.BindTo(graphView.GraphsStackedByEntity);
            ToolBar.Items.Add(showStackedByEntity);

            var showStackedByParameter = new ToggleButton() { Content = IconProvider.GetIcon(FontAwesomeIcon.EllipsisV), ToolTip = "Show stacked by parameter" };
            showStackedByParameter.BindTo(graphView.GraphsStackedByParameter);
            ToolBar.Items.Add(showStackedByParameter);

            var showStackedByParameterDepth = new ToggleButton() { Content = IconProvider.GetIcon(FontAwesomeIcon.SortAlphaDown), ToolTip = "Show stacked by parameter depth" };
            showStackedByParameterDepth.BindTo(graphView.GraphsStackedByParameterDepth);
            ToolBar.Items.Add(showStackedByParameterDepth);

            var autoScale = new ToggleButton() { Content = IconProvider.GetIcon(FontAwesomeIcon.ArrowsAltH), ToolTip = "Autoscale" };
            autoScale.BindTo(graphView.Autoscale);
            ToolBar.Items.Add(autoScale);

            ToolBar.Items.Add(new Separator());

            var lockEntitySelection = new ToggleButton() { Content = IconProvider.GetIcon(FontAwesomeIcon.Lock), ToolTip = "Lock selection" };
            lockEntitySelection.BindTo(graphView.EntitySelectionLocked);
            ToolBar.Items.Add(lockEntitySelection);

            var duplicatePanel = new Button() { Content = IconProvider.GetIcon(FontAwesomeIcon.AngleUp), ToolTip = "Duplicate Panel" };
            duplicatePanel.Click += (o,e) => mainwindow.DuplicatePanel(mainwindow.GraphsWindow);
            ToolBar.Items.Add(duplicatePanel);
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
