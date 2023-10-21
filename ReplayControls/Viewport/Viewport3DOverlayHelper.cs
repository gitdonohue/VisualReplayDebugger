// (c) 2021 Charles Donohue
// This code is licensed under MIT license (see LICENSE file for details)

using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace VisualReplayDebugger;

public class Viewport3DOverlayHelper : Canvas
{
    public Viewport3D Viewport { get; private set; }

    private Typeface Typeface;

    public Viewport3DOverlayHelper(Viewport3D viewport)
    {
        Viewport = viewport;
        Viewport.Camera.Changed += (o, e) => SetDirty();
        Typeface = new Typeface("Verdana");

        // Resend mouse events to vieport underneath
        this.MouseWheel += (o, e) => viewport.RaiseEvent(e);
        this.MouseDown += (o, e) => viewport.RaiseEvent(e);
    }

    public void SetDirty() => InvalidateVisual();

    private List<(FormattedText txt, Point3D worldpos, Brush backgroundBrush)> Labels = new();
    public void CreateLabel(string label, Point3D worldpos, int size, Color color) 
    {
        var txt = GetFormattedText(label, size, color);
        var backgroundBrush = new SolidColorBrush(Color.FromArgb(100,150,150,150));
        Labels.Add((txt, worldpos, backgroundBrush)); 
    }
    public void ClearLabels() { Labels.Clear(); }


    private List<(Point3D worldpos, int radius, Pen pen, Brush brush)> Circles = new();
    public void CreateCircle(Point3D worldpos, int size, Color color)
    {
        Circles.Add((worldpos, size, new Pen(new SolidColorBrush(color), 1), null));
    }
    public void ClearCircles() { Circles.Clear(); }

    
    public Point WorldToScreen(Point3D p) => HelixToolkit.Wpf.Viewport3DHelper.Point3DtoPoint2D(Viewport, p); // TODO: Remove dependency on Helix3D

    // TODO: Caching/Reuse
    private FormattedText GetFormattedText(string text, int size, Color color) => new FormattedText(text,
            System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface, size, new SolidColorBrush(color), 1.0);

    protected override void OnRender(DrawingContext dc)
    {
        dc.PushClip(new System.Windows.Media.RectangleGeometry(new Rect(0, 0, ActualWidth, ActualHeight)));
        foreach ((FormattedText formattedText, Point3D worldpos, Brush backgroundBrush) in Labels)
        {
            Point pos = WorldToScreen(worldpos);
            dc.DrawRectangle(backgroundBrush, null, new Rect(pos, new Size(formattedText.Width, formattedText.Height)));
            dc.DrawText(formattedText, pos);
        }
        foreach ((Point3D worldpos, int size, Pen pen, Brush brush) in Circles)
        {
            Point pos = WorldToScreen(worldpos);
            dc.DrawEllipse(brush, pen, pos, (double)size/2, (double)size /2);
        }
        dc.Pop();
    }
}
