using FontAwesomeIcon = FontAwesome.Sharp.IconChar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace VisualReplayDebugger.Panels
{
    class PropertiesTimelinesPanel : DockPanelWithToolbar, IDisposable
    {
        public PropertiesTimelinesPanel(MainWindow mainwindow)
            : base(scrolling: true)
        {
            var propertiesTimelinesView = new ReplayPropertiesTimelinesControl(mainwindow.TimelineWindow, mainwindow.Replay, mainwindow.EntitySelection, mainwindow.ColorProvider);
            this.Content = propertiesTimelinesView;

            mainwindow.ReplayChanged += (replay) => propertiesTimelinesView.Replay = replay;

            var filterLabel = new Label() { Content = IconProvider.GetIcon(FontAwesomeIcon.Filter) };
            ToolBar.Items.Add(filterLabel);

            var filtertext = new TextBox() { Width = 150 };
            filtertext.BindTo(propertiesTimelinesView.FilterText);
            ToolBar.Items.Add(filtertext);

            var showStackedByParameterDepth = new ToggleButton() { Content = IconProvider.GetIcon(FontAwesomeIcon.SortAlphaUpAlt), ToolTip = "Show stacked by parameter depth" };
            showStackedByParameterDepth.BindTo(propertiesTimelinesView.StackedByParameterDepth);
            ToolBar.Items.Add(showStackedByParameterDepth);

            var cb = new CheckComboBoxControl("Parameters");
            if (mainwindow.Replay != null) cb.SetItems(mainwindow.Replay.ParameterCategories, true);
            mainwindow.ReplayChanged += (replay) => cb.SetItems(replay.ParameterCategories, true);
            cb.Changed += () => propertiesTimelinesView.ParameterFilter.Set(cb.UnselectedItems);
            ToolBar.Items.Add(cb);

            ToolBar.Items.Add(new Separator());

            var prev = new Button() { Content = IconProvider.GetIcon(FontAwesomeIcon.ArrowLeft), ToolTip = "Move to previous event" };
            prev.Click += (o, e) => propertiesTimelinesView.MoveToPrevEvent();
            ToolBar.Items.Add(prev);

            var next = new Button() { Content = IconProvider.GetIcon(FontAwesomeIcon.ArrowRight), ToolTip = "Move to next event" };
            next.Click += (o, e) => propertiesTimelinesView.MoveToNextEvent();
            ToolBar.Items.Add(next);

            var zoomSmallest = new Button() { Content = IconProvider.GetIcon(FontAwesomeIcon.Compress), ToolTip = "Zoom on smallest block" };
            zoomSmallest.Click += (o, e) => propertiesTimelinesView.ZoomSmallestOnCursor();
            ToolBar.Items.Add(zoomSmallest);

            ToolBar.Items.Add(new Separator());

            var lockEntitySelection = new ToggleButton() { Content = IconProvider.GetIcon(FontAwesomeIcon.Lock), ToolTip = "Lock selection" };
            lockEntitySelection.BindTo(propertiesTimelinesView.EntitySelectionLocked);
            ToolBar.Items.Add(lockEntitySelection);

            var duplicatePanel = new Button() { Content = IconProvider.GetIcon(FontAwesomeIcon.AngleUp), ToolTip = "Duplicate Panel" };
            duplicatePanel.Click += (o, e) => mainwindow.DuplicatePanel(mainwindow.PropertiesTimelinesWindow);
            ToolBar.Items.Add(duplicatePanel);
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}