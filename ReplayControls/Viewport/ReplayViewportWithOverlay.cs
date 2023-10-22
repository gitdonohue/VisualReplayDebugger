// (c) 2021 Charles Donohue
// This code is licensed under MIT license (see LICENSE file for details)

using HelixToolkit.Wpf;
using ReplayCapture;
using SelectionSet;
using System;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Timeline;
using WatchedVariable;

namespace VisualReplayDebugger;

public class ReplayViewportWithOverlay : Grid
{
    public WatchedBool FollowCameraEnabled { get; } = new(true);
    public WatchedBool FollowSelectionEnabled { get; } = new(false);
    public WatchedBool ShowAllNames { get; } = new(false);
    public WatchedBool ShowAllPaths { get; } = new(false);
    public WatchedBool ShowDrawPrimitives { get; } = new(true);
    public WatchedBool ShowAllDrawPrimitivesInRange { get; } = new(false);
    public SelectionGroup<string> DrawCategoryFilter { get; } = new();
    public SelectionGroup<ReplayCapture.Color> DrawColorFilter { get; } = new();


    public event Action CameraManuallyMoved;

    public ReplayCaptureReader Replay
    {
        get => ReplayViewportContent?.Replay;
        set
        {
            if (ReplayViewportContent!=null)
            {
                ReplayViewportContent.Replay = value;
            }
        }
    }

    private ReplayViewportContent ReplayViewportContent { get; set; }

    public ReplayViewportWithOverlay(TimelineWindow timelineWindow, SelectionGroup<Entity> selectionset, SelectionGroup<Entity> visibleEntities)
    {
        var viewport3D = new HelixViewport3D();
        viewport3D.Background = Brushes.LightGray;
        viewport3D.ModelUpDirection = new Vector3D(0, 0, 1);
        viewport3D.Children.Add(new HelixToolkit.Wpf.SunLight() { Altitude = 70, Azimuth = 180 });
        viewport3D.Children.Add(new HelixToolkit.Wpf.SunLight() { Altitude = -45, Azimuth = 0 });
        viewport3D.Children.Add(new HelixToolkit.Wpf.GridLinesVisual3D() { Normal = viewport3D.ModelUpDirection, Thickness = 2, MajorDistance = 1000, MinorDistance = 100, Width = 10000, Length = 10000 });

        ReplayViewportContent = new ReplayViewportContent(viewport3D.Viewport, null, timelineWindow, selectionset, visibleEntities, DrawCategoryFilter, DrawColorFilter);
        ReplayViewportContent.FollowCameraEnabled.BindWith(FollowCameraEnabled);
        ReplayViewportContent.FollowSelectionEnabled.BindWith(FollowSelectionEnabled);
        ReplayViewportContent.ShowAllNames.BindWith(ShowAllNames);
        ReplayViewportContent.ShowAllPaths.BindWith(ShowAllPaths);
        ReplayViewportContent.ShowDrawPrimitives.BindWith(ShowDrawPrimitives);
        ReplayViewportContent.ShowAllDrawPrimitivesInRange.BindWith(ShowAllDrawPrimitivesInRange);
        ReplayViewportContent.FocusAtRequested += (p) => { viewport3D.LookAt(p, 200); };
        viewport3D.Children.Add(ReplayViewportContent.ModelVisual3D);
        ReplayViewportContent.CameraManuallyMoved += () => CameraManuallyMoved.Invoke();

        var overlay2D = new Viewport3DOverlayHelper(viewport3D.Viewport);
        timelineWindow.Changed += overlay2D.SetDirty;
        selectionset.Changed += overlay2D.SetDirty;
        ShowAllNames.Changed += overlay2D.SetDirty;
        ReplayViewportContent.DrawLabelRequest += (string txt, Point3D pos, int sz) => overlay2D.CreateLabel(txt, pos, sz, Colors.Black);
        ReplayViewportContent.DrawLabelResetRequest += overlay2D.ClearLabels;
        ReplayViewportContent.DrawCircleRequest += (Point3D pos, int sz, System.Windows.Media.Color color) => overlay2D.CreateCircle(pos,sz,color);
        ReplayViewportContent.DrawCircleResetRequest += overlay2D.ClearCircles;

        FollowSelectionEnabled.Changed += () => { if (FollowCameraEnabled) FollowCameraEnabled.Set(false); };
        FollowCameraEnabled.Changed += () => { if (FollowSelectionEnabled) FollowSelectionEnabled.Set(false); };

        this.MouseDown += OnMouseDown;

        this.Children.Add(viewport3D);
        this.Children.Add(overlay2D);
    }

    private void OnMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        var mousePos = e.GetPosition(ReplayViewportContent.Viewport3D);
        ReplayViewportContent.OnMouseDown(mousePos);
    }

    public void FocusOnSelection() => ReplayViewportContent.FocusOnSelection();
}
