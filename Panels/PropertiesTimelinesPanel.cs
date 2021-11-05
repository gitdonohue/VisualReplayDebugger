using FontAwesome.WPF;
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

            var searchLabel = new Label() { Content = GetIcon(FontAwesomeIcon.QuestionCircleOutline) };
            ToolBar.Items.Add(searchLabel);

            var searchtext = new TextBox() { Width = 150 };
            searchtext.BindTo(propertiesTimelinesView.SearchText);
            ToolBar.Items.Add(searchtext);

            var showStackedByParameterDepth = new ToggleButton() { Content = GetIcon(FontAwesomeIcon.SortNumericAsc), ToolTip = "Show stacked by parameter depth" };
            showStackedByParameterDepth.BindTo(propertiesTimelinesView.StackedByParameterDepth);
            ToolBar.Items.Add(showStackedByParameterDepth);

            var cb = new CheckComboBoxControl("Parameters");
            if (mainwindow.Replay != null) cb.SetItems(mainwindow.Replay.GetParameterCategories(), true);
            mainwindow.ReplayChanged += (replay) => cb.SetItems(replay.GetParameterCategories(), true);
            cb.Changed += () => propertiesTimelinesView.ParameterFilter.Set(cb.UnselectedItems);
            ToolBar.Items.Add(cb);

            ToolBar.Items.Add(new Separator());

            var prev = new Button() { Content = GetIcon(FontAwesomeIcon.ArrowLeft), ToolTip = "Move to previous event" };
            prev.Click += (o, e) => propertiesTimelinesView.MoveToPrevEvent();
            ToolBar.Items.Add(prev);

            var next = new Button() { Content = GetIcon(FontAwesomeIcon.ArrowRight), ToolTip = "Move to next event" };
            next.Click += (o, e) => propertiesTimelinesView.MoveToNextEvent();
            ToolBar.Items.Add(next);

            var zoomSmallest = new Button() { Content = GetIcon(FontAwesomeIcon.Compress), ToolTip = "Zoom on smallest block" };
            zoomSmallest.Click += (o, e) => propertiesTimelinesView.ZoomSmallestOnCursor();
            ToolBar.Items.Add(zoomSmallest);

            ToolBar.Items.Add(new Separator());

            var lockEntitySelection = new ToggleButton() { Content = GetIcon(FontAwesomeIcon.Lock), ToolTip = "Lock selection" };
            lockEntitySelection.BindTo(propertiesTimelinesView.EntitySelectionLocked);
            ToolBar.Items.Add(lockEntitySelection);

            var duplicatePanel = new Button() { Content = GetIcon(FontAwesomeIcon.LevelUp), ToolTip = "Duplicate Panel" };
            duplicatePanel.Click += (o, e) => mainwindow.DuplicatePanel(mainwindow.PropertiesTimelinesWindow);
            ToolBar.Items.Add(duplicatePanel);
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}