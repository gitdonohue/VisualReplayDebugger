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

    private readonly Typeface Typeface;

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

    private readonly List<(FormattedText txt, Point3D worldpos, Brush backgroundBrush)> Labels = new();
    public void CreateLabel(string label, Point3D worldpos, int size, Color color) 
    {
        var txt = GetFormattedText(label, size, color);
        var backgroundBrush = new SolidColorBrush(Color.FromArgb(100,150,150,150));
        Labels.Add((txt, worldpos, backgroundBrush)); 
    }
    public void ClearLabels() { Labels.Clear(); }


    private readonly List<(Point3D worldpos, double radius, Pen pen, Brush brush)> ScreenSpaceCircles = new();
    public void CreateScreenSpaceCircle(Point3D worldpos, double size, Color color)
    {
        ScreenSpaceCircles.Add((worldpos, size, new Pen(new SolidColorBrush(color), 1), null));
    }
    public void ClearScreenSpaceCircles() { ScreenSpaceCircles.Clear(); }

    private readonly List<(Point3D worldpos, Point3D upVect, double radius, Pen pen, Brush brush)> WorldSpaceCircles = new();
    public void CreateWorldSpaceCircle(Point3D worldpos, Point3D upVect, double size, Color color)
    {
        WorldSpaceCircles.Add((worldpos, upVect, size, new Pen(new SolidColorBrush(color), 1), null));
    }
    public void ClearWorldSpaceCircles()
    {
        WorldSpaceCircles.Clear();
    }

    private readonly List<(Point3D worldpos1, Point3D worldpos2, Pen pen)> Lines = new();
    public void CreateLine(Point3D worldpos1, Point3D worldpos2, Color color)
    {
        Lines.Add((worldpos1, worldpos2, new Pen(new SolidColorBrush(color), 1)));
    }
    public void ClearLines()
    {
        Lines.Clear();
    }

    public Point WorldToScreen(Point3D p) => HelixToolkit.Wpf.Viewport3DHelper.Point3DtoPoint2D(Viewport, p); // TODO: Remove dependency on Helix3D
    public IEnumerable<Point> WorldToScreen(IEnumerable<Point3D> p) => HelixToolkit.Wpf.Viewport3DHelper.Point3DtoPoint2D(Viewport, p); // TODO: Remove dependency on Helix3D

    // TODO: Caching/Reuse
    private FormattedText GetFormattedText(string text, int size, Color color) => new(text,
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
        
        foreach ((Point3D worldpos, double size, Pen pen, Brush brush) in ScreenSpaceCircles)
        {
            Point pos = WorldToScreen(worldpos);
            dc.DrawEllipse(brush, pen, pos, size/2, size /2);
        }

        foreach ((Point3D worldpos, Point3D _, double size, Pen pen, Brush brush) in WorldSpaceCircles)
        {
            Point pos = WorldToScreen(worldpos);

            //// TODO: Find Transform that approximates circle => oval projection
            
            ////dc.PushTransform(new ScaleTransform(1.0,0.50, pos.X, pos.Y));
            //dc.PushTransform(new SkewTransform(0.0, 0.0, pos.X, pos.Y));
            //dc.DrawEllipse(brush, pen, pos, size, size);
            //dc.Pop();

            // draw line segments

        }

        foreach ((Point3D worldpos1, Point3D worldpos2, Pen pen) in Lines)
        {
            Point p1 = WorldToScreen(worldpos1);
            Point p2 = WorldToScreen(worldpos2);
            dc.DrawLine(pen, p1, p2);
        }
        dc.Pop();
    }
}
