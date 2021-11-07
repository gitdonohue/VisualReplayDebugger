using FontAwesome.WPF;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Timeline;

namespace VisualReplayDebugger.Panels
{
    class VideoPanel : DockPanelWithToolbar, IDisposable
    {
        VideoControl VideoControl;

        public VideoPanel(MainWindow mainWindow)
            : base(scrolling: false)
        {
            VideoControl = new VideoControl(mainWindow.TimelineWindow);
            this.Content = VideoControl;

            var load = new Button() { Content = GetIcon(FontAwesomeIcon.FileMovieOutline), ToolTip = "Load video" };
            load.Click += (o, e) => VideoControl.LoadMovie();
            ToolBar.Items.Add(load);

            var sync = new ToggleButton() { Content = GetIcon(FontAwesomeIcon.Lock), ToolTip = "Sync video to timeline" };
            sync.BindTo(VideoControl.SyncLock);
            ToolBar.Items.Add(sync);
        }

        public void Dispose()
        {
            VideoControl?.Dispose();
            VideoControl = null;
        }
    }
}
