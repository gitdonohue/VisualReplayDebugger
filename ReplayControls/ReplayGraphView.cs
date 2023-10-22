// (c) 2021 Charles Donohue
// This code is licensed under MIT license (see LICENSE file for details)

//#define VERBOSE

using ReplayCapture;
using SelectionSet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Timeline;
using WatchedVariable;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Pen = System.Windows.Media.Pen;

namespace VisualReplayDebugger;

public class ReplayGraphView : UserControl, IDisposable
{
    ReplayCaptureReader replay;
    public ReplayCaptureReader Replay
    {
        get => replay;
        set
        {
            replay = value;
            EntitySelectionLocked.Set(false);
            BuildReplayTables();
        }
    }

    public ITimelineWindow TimelineWindow { get; private set; }

    public SelectionGroup<Entity> EntitySelection { get; private set; }
    public ColorProvider ColorProvider { get; private set; }
    public WatchedBool EntitySelectionLocked { get; } = new(false);
    public WatchedBool GraphsFilled { get; } = new(true);
    public WatchedBool GraphsStackedByEntity { get; } = new(true);
    public WatchedBool GraphsStackedByParameter { get; } = new(false);
    public WatchedBool GraphsStackedByParameterDepth { get; } = new(true);
    public WatchedBool Autoscale { get; } = new(false);

    private static readonly System.Globalization.CultureInfo TextCultureInfo =  System.Globalization.CultureInfo.GetCultureInfo("en-us");
    private static readonly Typeface TextTypeface = new("Arial");

    private IEnumerable<Entity> SelectedEntities => EntitySelectionLocked ? LockedSelection : EntitySelection.SelectionSet;
    private readonly List<Entity> LockedSelection = new();

    readonly VisualReplayDebugger.ReplayControls.TimelineMouseControlHandler MouseHandler;

    public ReplayGraphView(ITimelineWindow timeLineWindow, ReplayCaptureReader replay, SelectionGroup<Entity> selectionset, ColorProvider colorprovider)
    {
        TimelineWindow = timeLineWindow;
        Replay = replay;
        ColorProvider = colorprovider;

        TimelineWindow.Changed += TimelineWindow_Changed;
        EntitySelection = selectionset;
        EntitySelection.Changed += EntitySelection_Changed;

        EntitySelectionLocked.Changed += RequestFullRedraw;
        GraphsFilled.Changed += RequestFullRedraw;
        GraphsStackedByEntity.Changed += RequestFullRedraw;
        GraphsStackedByParameter.Changed += RequestFullRedraw;
        GraphsStackedByParameterDepth.Changed += RequestFullRedraw;
        Autoscale.Changed += RequestFullRedraw;

        MouseHandler = new(TimelineWindow, this, windowMode:false);
        this.MouseDown += MouseHandler.OnMouseDown;
        this.MouseUp += MouseHandler.OnMouseUp;
        this.MouseMove += MouseHandler.OnMouseMove;
        this.MouseWheel += MouseHandler.OnMouseWheel;

        this.SizeChanged += OnSizeChanged;

        BackgroundBrush = new SolidColorBrush() { Color = Colors.AliceBlue };
        BackgroundBrush.Freeze();
        GraphPen = new Pen(Brushes.Red, 2);
        GraphPen.Freeze();
        CursorPen = new Pen(Brushes.Red, 1);
        CursorPen.Freeze();
        SeparatorPen = new Pen(Brushes.Gray, 1);
        SeparatorPen.Freeze();

        FastRedrawTimer = new System.Timers.Timer(500.0); // Q: ideal duration?  Maybe also wait for mouse to be lifted?
        FastRedrawTimer.Elapsed += FastRedrawTimer_Elapsed;
    }

    public void Dispose()
    {
        TimelineWindow.Changed -= TimelineWindow_Changed;
        EntitySelection.Changed -= EntitySelection_Changed;
    }

    private Dictionary<Entity, Dictionary<string, List<(double t, double val)>>> ValueTables = new();
    private Dictionary<Entity, Dictionary<string, double>> MaxValueTables = new();

    private Dictionary<Entity, Dictionary<string, double>> MeanValueTables = new();
    private Dictionary<Entity, Dictionary<string, double>> StdDevValueTables = new();

    void BuildReplayTables()
    {
        ValueTables?.Clear();
        if (Replay != null)
        {
            ValueTables = new();
            MaxValueTables = new();

            foreach (var kvp in Replay.EntityDynamicValues)
            {
                Entity entity = kvp.Key;
                var dict = new Dictionary<string, List<(double, double)>>();
                ValueTables.Add(entity, dict);

                var maxValuesDict = new Dictionary<string, double>();
                MaxValueTables.Add(entity, maxValuesDict);

                foreach ((int frame,(string label, double value)) in kvp.Value)
                {
                    double frameTime = Replay.GetTimeForFrame(frame);
                    if (!dict.TryGetValue(label, out var valuelist)) { valuelist = new List<(double, double)>(); dict.Add(label, valuelist); }
                    valuelist.Add((frameTime, value));

                    double v = (value > 1) ? value : 1;
                    if (!maxValuesDict.TryGetValue(label, out double max)) { max = 0; }
                    if (v > max) { maxValuesDict[label] = v; }
                }
            }

            MeanValueTables = new();
            StdDevValueTables = new();
            foreach (var entityValues in ValueTables)
            {
                var meanvalues = new Dictionary<string, double>();
                MeanValueTables[entityValues.Key] = meanvalues;
                var stddevvalues = new Dictionary<string, double>();
                StdDevValueTables[entityValues.Key] = stddevvalues;
                foreach (var valuestream in entityValues.Value)
                {
                    double sum = valuestream.Value.Sum(x => x.val);
                    double avg = sum / valuestream.Value.Count;
                    meanvalues[valuestream.Key] = avg;

                    double stddev = Math.Sqrt(valuestream.Value.Sum(x => (x.val - avg)*(x.val - avg)) / valuestream.Value.Count);
                    stddevvalues[valuestream.Key] = stddev;
                }
            }
        }
    }

    #region drawing

    enum RenderModeType 
    { 
        Direct,
        Bitmap, // TODO: BROKEN
        BitmapWithLoRezFastDraw // TODO: BROKEN
    }
    RenderModeType RenderMode { get; set; } = RenderModeType.Direct;

    public void StepRenderingMode()
    {
        RenderMode = (RenderModeType)(((int)RenderMode + 1) % Enum.GetNames(typeof(RenderModeType)).Length);
        RequestFullRedraw();
        SetDirty();
    }

    static Brush BackgroundBrush;
    static Pen GraphPen;
    static Pen CursorPen;
    static Pen SeparatorPen;

    static readonly char[] SplitSeparators = new char[]{ '.', '\\', '/' };

    double lastStart;
    double lastEnd;
    private void TimelineWindow_Changed()
    {
        if (TimelineWindow.Start != lastStart || TimelineWindow.End != lastEnd)
        {
            RequestFastRedraw();
        }
        lastStart = TimelineWindow.Start;
        lastEnd = TimelineWindow.End;
        SetDirty();
    }

    private void EntitySelection_Changed()
    {
        if (!EntitySelectionLocked)
        {
            RequestFullRedraw();
            LockedSelection.Clear();
            LockedSelection.AddRange(EntitySelection.SelectionSet);
        }
    }

    RenderTargetBitmap GraphsBitmap;
    readonly System.Timers.Timer FastRedrawTimer;

    private void ResizeGraphsBitmap(double reductionFactor = 1)
    {
        var dpi = VisualTreeHelper.GetDpi(this);

        int current_w = 0;
        int current_h = 0;
        if (GraphsBitmap != null)
        {
            current_w = GraphsBitmap.PixelWidth;
            current_h = GraphsBitmap.PixelHeight;
        }

        int w = (int)(ActualWidth / reductionFactor);
        int h = (int)(ActualHeight / reductionFactor);
        
        if (GraphsBitmap == null || current_w != w || current_h != h)
        {
            DebugLog($"GraphsBitmap resized: ({current_w},{current_h}) -> ({w},{h})");
            if (w*h != 0 )
            {
                GraphsBitmap = new RenderTargetBitmap(w,h, dpi.PixelsPerInchX / reductionFactor, dpi.PixelsPerInchY / reductionFactor, PixelFormats.Pbgra32);
            }
        }
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        RequestFastRedraw();
    }

    private void SetDirty()
    {
        DebugLog($"Graphs SetDirty {TimelineWindow?.Timeline.Cursor}");
        InvalidateVisual();
    }

    private void RequestFastRedraw()
    {
        if (RenderMode == RenderModeType.BitmapWithLoRezFastDraw)
        {
            FastRedrawTimer.Stop();
            FastRedrawTimer.Start();
            ResizeGraphsBitmap(4);
        }
        RequestFullRedraw();
    }

    private void FastRedrawTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
    {
        FastRedrawTimer.Stop();
        Application.Current.Dispatcher.Invoke(() =>
        {
            ResizeGraphsBitmap(1);
            RequestFullRedraw();
        });
    }

    private void RequestFullRedraw()
    {
        if (RenderMode == RenderModeType.Direct)
        {
            InvalidateVisual();
        }
        else if (RenderMode == RenderModeType.Bitmap || RenderMode == RenderModeType.BitmapWithLoRezFastDraw)
        {
            double start = TimelineWindow.Start;
            double end = TimelineWindow.End;

            DrawingVisual drawingVisual = new DrawingVisual();
            DrawingContext dc = drawingVisual.RenderOpen();
            RenderGraphsForRange(dc, this, Bounds, Replay, start, end, SelectedEntities);
            dc.Close();
            GraphsBitmap?.Render(drawingVisual);

            InvalidateVisual();
        }
    }

    private Rect Bounds => new(0, 0, Math.Ceiling(ActualWidth), Math.Ceiling(ActualHeight));

    private double GetHighVal(Entity entity, string streamlabel)
    {
        double maxVal = MaxValueTables[entity][streamlabel];

        double highVal = maxVal;

        if (Autoscale && maxVal > 1)
        {
            double avg = MeanValueTables[entity][streamlabel];
            double stddev = StdDevValueTables[entity][streamlabel];
            if (maxVal > (avg + 3 * stddev))
            {
                highVal = avg + 2 * stddev;
            }
        }

        return highVal;
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (Replay == null) return;

        if (RenderMode == RenderModeType.Direct)
        {
            RenderGraphsForRange(dc, this, Bounds, Replay, TimelineWindow.Start, TimelineWindow.End, SelectedEntities);
        }
        else if (RenderMode == RenderModeType.Bitmap || RenderMode == RenderModeType.BitmapWithLoRezFastDraw)
        {
            dc.DrawImage(GraphsBitmap, Bounds);
        }

        //
        // Cursor-dependent overlays
        //

        // Draw cursor
        double cursorTime = TimelineWindow.Timeline.Cursor;
        int cursorFrame = Replay.GetFrameForTime(cursorTime);
        double r = Math.Clamp(TimelineWindow.CursorRatio, 0, 1);
        double cursorXPos = r * Bounds.Width;
        dc.DrawLine(CursorPen, new System.Windows.Point(cursorXPos, 0), new System.Windows.Point(cursorXPos, ActualHeight));

        foreach ((Entity entity, Rect rectToFillForEntity, int entityIndex) in EnumerateEntityDrawRegions(SelectedEntities, Bounds))
        {
            // Entity labels
            dc.DrawText(
                new FormattedText($"{entity.Name}",
                    TextCultureInfo, FlowDirection.LeftToRight, TextTypeface,
                    12, Brushes.Black, 1.25),
                    rectToFillForEntity.TopLeft);

            // Param values at cursor
            bool inRange = replay.GetEntityLifeTime(entity).InRange(cursorFrame);
            foreach ((string streamlabel, var dataPoints, int entryNum, Rect rectToFillForParameter) in EnumerateParameterDrawRegions(entity, rectToFillForEntity))
            {
                // Draw label at cursor pos
                if (inRange && cursorTime >= TimelineWindow.Start && cursorTime <= TimelineWindow.End)
                {
                    double highVal = GetHighVal(entity,streamlabel);

                    double baselineHeight = rectToFillForParameter.Y + rectToFillForParameter.Height;
                    double valueAtCursor = dataPoints.TakeWhile(x => x.t < cursorTime).LastOrDefault().val;
                    double textHeight = 8;
                    double yPos = baselineHeight - textHeight - (valueAtCursor * (rectToFillForParameter.Height - textHeight) / highVal);

                    var baseColor = ColorProvider.GetLabelColor(streamlabel);
                    var brush = new SolidColorBrush(baseColor);
                    if (valueAtCursor > double.Epsilon || GraphsStackedByParameter)
                    {
                        dc.DrawText(
                           // Note: this could be cached
                           new FormattedText($" {streamlabel} {valueAtCursor:.###}",
                              TextCultureInfo, FlowDirection.LeftToRight, TextTypeface,
                              8, brush, 1.25),
                              new System.Windows.Point(cursorXPos, yPos));
                    }
                }
            }
        }

        base.OnRender(dc);
    }

    private IEnumerable<(Entity,Rect,int)> EnumerateEntityDrawRegions(IEnumerable<Entity> EntitiesToDraw, Rect bounds)
    {
        int entityIndex = -1;
        foreach (var selectedEntity in EntitiesToDraw)
        {
            if (selectedEntity == null) continue;
            ++entityIndex;

            Rect rectToFillForEntity = bounds;
            if (GraphsStackedByEntity)
            {
                double h = bounds.Height / EntitiesToDraw.Count();
                rectToFillForEntity = new Rect(bounds.X, bounds.Y + entityIndex * h, bounds.Width, h);
            }

            Rect rectToFillForParameter = rectToFillForEntity;

            yield return (selectedEntity, rectToFillForEntity, entityIndex);
        }
    }

    private IEnumerable<(string,List<(double t,double val)>,int,Rect)> EnumerateParameterDrawRegions(Entity entity, Rect rectToFillForEntity)
    {
        if (ValueTables.TryGetValue(entity, out var entityData))
        {
            int entryNum = -1;
            int entryCount = entityData.Keys.Count;

            int maxDepth = 0;
            if (GraphsStackedByParameterDepth)
            {
                var keyTokens = entityData.Keys.Select(k => k.Split(SplitSeparators).Count() - 1);
                if (keyTokens.Any()) maxDepth = keyTokens.Max();
            }

            foreach (var datastream in entityData)
            {
                ++entryNum;

                Rect rectToFillForParameter = rectToFillForEntity;
                if (GraphsStackedByParameter)
                {
                    double h = rectToFillForEntity.Height / entryCount;
                    rectToFillForParameter = new Rect(rectToFillForEntity.X, rectToFillForEntity.Y + entryNum * h, rectToFillForEntity.Width, h);
                }

                if (GraphsStackedByParameterDepth && maxDepth > 0)
                {
                    int paramDepth = datastream.Key.Split(SplitSeparators).Count() - 1;
                    double h = rectToFillForEntity.Height / (maxDepth + 1);
                    rectToFillForParameter = new Rect(rectToFillForEntity.X, rectToFillForEntity.Y + paramDepth * h, rectToFillForEntity.Width, h);
                }

                yield return (datastream.Key, datastream.Value, entryNum, rectToFillForParameter);
            }
        }
    }

    private static IEnumerable<(double begin,double end)> GetRange(IEnumerable<(double t,double val)> values, double start, double finish)
    {
        double lastValue = 0;
        bool firstSent = false;
        foreach (var timePair in values)
        {
            double t = timePair.t;
            double v = timePair.val;
            if (t > finish)
            {
                yield return (finish, lastValue);
                yield break;
            }
            else if (t < start)
            {

            }
            else if(t > start)
            {
                if (!firstSent)
                {
                    yield return (start,lastValue);
                    firstSent = true;
                }
                yield return timePair;
            }
            lastValue = v;
        }
    }

    protected static void RenderGraphsForRange(DrawingContext dc, ReplayGraphView view, Rect bounds, ReplayCaptureReader replay, double timeStart, double timeEnd, IEnumerable<Entity> EntitiesToDraw)
    {
        if (replay == null) return;

        DebugLog($"RenderGraphsForRange times:({timeStart},{timeEnd}) sz:({bounds})");

        double t0 = timeStart;
        double t1 = timeEnd;

        double t0_ = replay.GetTimeForFrame(replay.GetFrameForTime(t0) - 1);
        double t1_ = replay.GetTimeForFrame(replay.GetFrameForTime(t1) + 1);

        // Draw background
        dc.DrawRectangle(BackgroundBrush, null, bounds);

        bool doFill = view.GraphsFilled;
        bool doStep = true;

        foreach ((Entity entity, Rect rectToFillForEntity, int entityIndex) in view.EnumerateEntityDrawRegions(EntitiesToDraw,bounds))
        {
            // Draw separator line
            dc.DrawLine(SeparatorPen, rectToFillForEntity.BottomLeft, rectToFillForEntity.BottomRight);

            if (view.ValueTables.TryGetValue(entity, out var entityData))
            {
                foreach ((string streamlabel, var dataPoints, int entryNum, Rect rectToFillForParameter) in view.EnumerateParameterDrawRegions(entity, rectToFillForEntity))
                {
                    double baselineHeight = rectToFillForParameter.Y + rectToFillForParameter.Height;
                    double highVal = view.GetHighVal(entity, streamlabel);

                    var _pts = GetRange(dataPoints,t0_,t1_).Select(x => IntoRect(rectToFillForParameter, (x.begin - t0) / (t1 - t0), x.end / highVal));

                    if (doStep) { _pts = _pts.StepPoints(); }
                    var pts = _pts.ToArray();
                    if (pts.Count() > 2)
                    {
                        var geometry = new StreamGeometry();
                        using (StreamGeometryContext ctx = geometry.Open())
                        {
                            ctx.BeginFigure(pts.First().WithY(baselineHeight), doFill, doFill);
                            ctx.PolyLineTo(pts, true, false);
                            ctx.LineTo(pts.Last().WithY(baselineHeight), true, false);
                        }
                        geometry.Freeze();
                        var baseColor = view.ColorProvider.GetLabelColor(streamlabel);
                        var pen = new Pen(new SolidColorBrush(baseColor), 0.75);
                        if (doFill)
                        {
                            dc.DrawGeometry(new SolidColorBrush(baseColor).WithAlpha(0.30), pen.WithAlpha(0.70), geometry);
                        }
                        else
                        {
                            dc.DrawGeometry(null, pen, geometry);
                        }
                    }
                }
            }
        }
    }

    private static System.Windows.Point IntoRect(Rect rect, double x01, double y01) => new System.Windows.Point(rect.X + x01 * rect.Width, rect.Y + ((1 - y01) * rect.Height));

    #endregion //drawing

    [System.Diagnostics.Conditional("VERBOSE")]
    public static void DebugLog(string message)
    {
        System.Diagnostics.Debug.WriteLine(message);
    }
}
