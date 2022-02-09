// (c) 2021 Charles Donohue
// This code is licensed under MIT license (see LICENSE file for details)

//using FontAwesome.WPF;
using FontAwesome.Sharp;
using FontAwesomeIcon = FontAwesome.Sharp.IconChar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Drawing;

namespace VisualReplayDebugger
{
    public class DockPanelWithToolbar : DockPanel
    {
        public ToolBarTray ToolBarTray { get; private set; }
        public ToolBar ToolBar { get; private set; }

        private FrameworkElement content;
        public FrameworkElement Content 
        {
            get => content;
            set
            {
                if (content != null)
                {
                    if (ScrollViewer != null)
                    {
                        ScrollViewer.Content = null;
                    }
                    else
                    {
                        Children.Remove(content);
                    }
                }
                content = value;
                if (content != null)
                {
                    DockPanel.SetDock(content, Dock.Top);
                    if (ScrollViewer != null)
                    {
                        ScrollViewer.Content = content;
                    }
                    else
                    {
                        Children.Add(content);
                    }
                }
            }
        }

        public ScrollViewer ScrollViewer { get; private set; }

        public DockPanelWithToolbar(double minHeight = 0, double initialHeight = 0, bool scrolling = false)
        {
            if (minHeight > 0) this.MinHeight = minHeight;
            if (initialHeight > 0) this.Height = initialHeight;

            DockPanel.SetDock(this, Dock.Top);

            ToolBarTray = new ToolBarTray();
            DockPanel.SetDock(ToolBarTray, Dock.Top);

            Children.Add(ToolBarTray);

            ToolBar = new ToolBar();
            ToolBarTray.ToolBars.Add(ToolBar);

            if (scrolling)
            {
                ScrollViewer = new ScrollViewer();
                DockPanel.SetDock(ScrollViewer, Dock.Top);
                Children.Add(ScrollViewer);
            }
        }
    }
}
