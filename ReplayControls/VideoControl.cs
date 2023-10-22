using System;
using Timeline;
using WatchedVariable;

namespace VisualReplayDebugger;

class VideoControl : System.Windows.Controls.MediaElement, IDisposable
{

    public WatchedBool SyncLock { get; } = new(true);
    
    private ITimeline Timeline;

    private double TimeOffset;

    private System.Windows.Controls.MediaElement MediaElement;

    public VideoControl(ITimelineWindow timeLineWindow)
    {
        Timeline = timeLineWindow.Timeline;

        Timeline.Changed += Timeline_Changed;
        SyncLock.Changed += SyncLock_Changed;

        MediaElement = this;

        MediaElement.LoadedBehavior = System.Windows.Controls.MediaState.Manual;
        MediaElement.ScrubbingEnabled = true;

        MediaElement.MediaOpened += MediaElement_MediaOpened;

        TimeOffset = 0;
    }


    public void Dispose()
    {
        Timeline.Changed -= Timeline_Changed;
    }

    public void LoadMovie()
    {
        MediaElement.Stop();

        var openFileDialog = new Microsoft.Win32.OpenFileDialog();
        if (openFileDialog.ShowDialog() == true)
        {
            MediaElement.Source = new Uri(openFileDialog.FileName);
        }
    }

    private void MediaElement_MediaOpened(object sender, System.Windows.RoutedEventArgs e)
    {
        MediaElement.Pause();
        MediaElement.IsMuted = true;
    }

    private void SyncLock_Changed()
    {
        if (SyncLock)
        {
            TimeOffset = GetVideoPos() - Timeline.Cursor;
        }
    }

    private void Timeline_Changed()
    {
        if (SyncLock)
        {
            SetVideoPos(Timeline.Cursor + TimeOffset);
        }
    }

    private double _videoPos;
    private void SetVideoPos(double t)
    {
        _videoPos = t;

        MediaElement.Position = new TimeSpan(0, 0, 0, 0, (int)(t * 1000.0));
    }

    private double GetVideoPos()
    {
        return _videoPos;
    }
}
