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
    class PropertiesPanel : DockPanelWithToolbar, IDisposable
    {
        public PropertiesPanel(MainWindow mainwindow)
            : base(scrolling: true)
        {
            var entityPropertiesView = new EntityPropertiesControl(mainwindow.Replay, mainwindow.TimelineWindow, mainwindow.EntitySelection);
            this.Content = entityPropertiesView;
            
            mainwindow.ReplayChanged += (replay) => entityPropertiesView.Replay = replay;

            var searchLabel = new Label() { Content = GetIcon(FontAwesomeIcon.QuestionCircleOutline) };
            ToolBar.Items.Add(searchLabel);

            var searchtext = new TextBox() { Width = 150 };
            searchtext.BindTo(entityPropertiesView.SearchText);
            ToolBar.Items.Add(searchtext);

            var lockEntitySelection = new ToggleButton() { Content = GetIcon(FontAwesomeIcon.Lock), ToolTip = "Lock selection" };
            lockEntitySelection.BindTo(entityPropertiesView.EntitySelectionLocked);
            ToolBar.Items.Add(lockEntitySelection);

            ToolBar.Items.Add(new Separator());

            var duplicatePanel = new Button() { Content = GetIcon(FontAwesomeIcon.LevelUp), ToolTip = "Duplicate Panel" };
            duplicatePanel.Click += (o, e) => mainwindow.DuplicatePanel(mainwindow.PropertiesWindow);
            ToolBar.Items.Add(duplicatePanel);
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
