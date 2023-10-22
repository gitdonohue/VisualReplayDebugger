using FontAwesomeIcon = FontAwesome.Sharp.IconChar;
using System;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace VisualReplayDebugger.Panels;

class VideoPanel : DockPanelWithToolbar, IDisposable
{
    VideoControl VideoControl;

    public VideoPanel(MainWindow mainWindow)
        : base(scrolling: false)
    {
        VideoControl = new VideoControl(mainWindow.TimelineWindow);
        this.Content = VideoControl;

        var load = new Button() { Content = IconProvider.GetIcon(FontAwesomeIcon.CameraRetro), ToolTip = "Load video" };
        load.Click += (o, e) => VideoControl.LoadMovie();
        ToolBar.Items.Add(load);

        var sync = new ToggleButton() { Content = IconProvider.GetIcon(FontAwesomeIcon.Lock), ToolTip = "Sync video to timeline" };
        sync.BindTo(VideoControl.SyncLock);
        ToolBar.Items.Add(sync);
    }

    public void Dispose()
    {
        VideoControl?.Dispose();
        VideoControl = null;
    }
}
