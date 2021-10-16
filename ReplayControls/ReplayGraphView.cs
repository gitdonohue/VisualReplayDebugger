// (c) 2021 Charles Donohue
// This code is licensed under MIT license (see LICENSE file for details)

using ReplayCapture;
using SelectionSet;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Timeline;
using WatchedVariable;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Pen = System.Windows.Media.Pen;

namespace VisualReplayDebugger
{
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

        public WatchedBool EntitySelectionLocked { get; } = new(false);
        public WatchedBool GraphsFilled { get; } = new(true);
        public WatchedBool GraphsStackedByEntity { get; } = new(true);
        public WatchedBool GraphsStackedByParameter { get; } = new(false);
        public WatchedBool GraphsStackedByParameterDepth { get; } = new(false);

        private static System.Globalization.CultureInfo TextCultureInfo =  System.Globalization.CultureInfo.GetCultureInfo("en-us");
        private static Typeface TextTypeface = new Typeface("Arial");

        private IEnumerable<Entity> SelectedEntities => EntitySelectionLocked ? LockedSelection : EntitySelection.SelectionSet;
        private List<Entity> LockedSelection = new();

        public ReplayGraphView(ITimelineWindow timeLineWindow, ReplayCaptureReader replay, SelectionGroup<Entity> selectionset)
        {
            TimelineWindow = timeLineWindow;
            Replay = replay;

            TimelineWindow.Changed += TimelineWindow_Changed;
            EntitySelection = selectionset;
            EntitySelection.Changed += EntitySelection_Changed;

            EntitySelectionLocked.Changed += RequestFullRedraw;
            GraphsFilled.Changed += RequestFullRedraw;
            GraphsStackedByEntity.Changed += RequestFullRedraw;
            GraphsStackedByParameter.Changed += RequestFullRedraw;
            GraphsStackedByParameterDepth.Changed += RequestFullRedraw;

            this.MouseDown += OnMouseDown;
            this.MouseUp += OnMouseUp;
            this.MouseMove += OnMouseMove;
            this.MouseWheel += OnMouseWheel;

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

        private Dictionary<Entity, Dictionary<string, List<(double, double)>>> ValueTables = new();
        private Dictionary<Entity, Dictionary<string, double>> MaxValueTables = new();

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
            }
        }

        #region mouse handling

        double CursorUnitPos
        {
            get => TimelineWindow?.Timeline?.Cursor ?? 0;
            set
            {
                if (TimelineWindow?.Timeline != null)
                {
                    TimelineWindow.Timeline.Cursor = value;
                }
            }
        }

        double CursorRatio => TimelineWindow != null ? ((TimelineWindow.Range > 0) ? ((CursorUnitPos - TimelineWindow.Start) / TimelineWindow.Range) : 0) : 0;

        bool IsScrollActive;
        bool IsScrubActive;
        bool MouseDidMove;
        System.Windows.Point MouseLastPos;
        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            ((UIElement)e.Source).CaptureMouse();
            MouseLastPos = e.GetPosition(this);
            MouseDidMove = false;

            if (TimelineWindow != null)
            {
                double cursorPixelXPos = CursorRatio * ActualWidth;
                if (Math.Abs(cursorPixelXPos - MouseLastPos.X) < 10)
                {
                    IsScrubActive = true;
                }
                else
                {
                    IsScrollActive = true;
                }
            }
        }

        private void OnMouseLeave(object sender, MouseEventArgs e)
        {
            //IsScrubActive = false;
            //IsScrollActive = false;
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            ((UIElement)e.Source).ReleaseMouseCapture();

            if (!IsScrubActive && !MouseDidMove)
            {
                // Move cursor to position
                double mouseUnitPos = TimelineWindow.Start + MouseLastPos.X * TimelineWindow.Range / ActualWidth;
                CursorUnitPos = mouseUnitPos;
                SetDirty();
            }

            IsScrollActive = false;
            IsScrubActive = false;
        }
        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            System.Windows.Point scroll = e.GetPosition(this);

            // Clamp to control
            if (scroll.X < 0) { scroll.X = 0; }
            if (scroll.X > ActualWidth) { scroll.X = ActualWidth; }

            var delta = scroll - MouseLastPos;
            MouseLastPos = scroll;

            if (Math.Abs(delta.X) > 0)
            {
                MouseDidMove = true;
            }

            if (TimelineWindow != null)
            {
                if (IsScrollActive)
                {
                    double offset = -delta.X * TimelineWindow.Range / ActualWidth;
                    TimelineWindow.SlideWindow(offset);
                }
                else if (IsScrubActive)
                {
                    double mouseUnitPos = TimelineWindow.Start + MouseLastPos.X * TimelineWindow.Range / ActualWidth;
                    CursorUnitPos = mouseUnitPos;
                    SetDirty();
                }
            }
        }

        private void OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (TimelineWindow != null)
            {
                double zoomFactor = 1.0 + e.Delta * 0.0005f;
                double zoomAboutPos = TimelineWindow.Start + (TimelineWindow.Range * e.GetPosition(this).X / ActualWidth);
                TimelineWindow?.ScaleWindow(zoomFactor, zoomAboutPos);
            }
        }
        #endregion //mouse handling

        #region drawing

        enum RenderModeType 
        { 
            Direct,
            Bitmap,
            BitmapWithLoRezFastDraw
        }
        RenderModeType RenderMode { get; set; } = RenderModeType.BitmapWithLoRezFastDraw;

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

        static char[] SplitSeparators = new char[]{ '.', '\\', '/' };

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
        System.Timers.Timer FastRedrawTimer;

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
                //System.Diagnostics.Trace.WriteLine($"GraphsBitmap resized: ({current_w},{current_h}) -> ({w},{h})");
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

        private Rect Bounds => new Rect(0, 0, ActualWidth, ActualHeight);

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
                        double maxVal = MaxValueTables[entity][streamlabel];
                        double baselineHeight = rectToFillForParameter.Y + rectToFillForParameter.Height;
                        double valueAtCursor = dataPoints.TakeWhile(x => x.Item1 < cursorTime).LastOrDefault().Item2;
                        double textHeight = 8;
                        double yPos = baselineHeight - textHeight - (valueAtCursor * (rectToFillForParameter.Height - textHeight) / maxVal);

                        dc.DrawText(
                           // Note: this could be cached
                           new FormattedText($" {streamlabel} {valueAtCursor:.###}",
                              TextCultureInfo, FlowDirection.LeftToRight, TextTypeface,
                              8, GetRandomBrush(entryNum), 1.25),
                              new System.Windows.Point(cursorXPos, yPos));
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

        private IEnumerable<(string,List<(double,double)>,int,Rect)> EnumerateParameterDrawRegions(Entity entity, Rect rectToFillForEntity)
        {
            if (ValueTables.TryGetValue(entity, out var entityData))
            {
                int entryNum = -1;
                int entryCount = entityData.Keys.Count;
                int maxDepth = GraphsStackedByParameterDepth ? entityData.Keys.Select(k => k.Split(SplitSeparators).Count() - 1).Max() : 0;
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

        private static IEnumerable<(double,double)> GetRange(IEnumerable<(double,double)> values, double start, double end)
        {
            double lastValue = 0;
            bool firstSent = false;
            foreach (var timePair in values)
            {
                double t = timePair.Item1;
                double v = timePair.Item2;
                if (t > end)
                {
                    yield return (end, lastValue);
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
                        double maxVal = view.MaxValueTables[entity][streamlabel];

                        var _pts = GetRange(dataPoints,t0_,t1_).Select(x => IntoRect(rectToFillForParameter, (x.Item1 - t0) / (t1 - t0), x.Item2 / maxVal));

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
                            if (doFill)
                            {
                                dc.DrawGeometry(GetRandomBrush(entryNum).WithAlpha(0.15), GetRandomPen(entryNum).WithAlpha(0.5), geometry);
                            }
                            else
                            {
                                dc.DrawGeometry(null, GetRandomPen(entryNum), geometry);
                            }
                        }
                    }
                }
            }
        }

        private static System.Windows.Point IntoRect(Rect rect, double x01, double y01) => new System.Windows.Point(rect.X + x01 * rect.Width, rect.Y + ((1 - y01) * rect.Height));
        
        static List<System.Windows.Media.Color> RandomColors = new List<System.Windows.Media.Color>
        {
            Colors.Red,
            Colors.Purple,
            Colors.Green,
            Colors.Blue,
            Colors.YellowGreen,
            Colors.Orange,
            Colors.OrangeRed
            // TODO: add more colors
        };

        static List<SolidColorBrush> RandomBrushes = RandomColors.Select(c => new SolidColorBrush(c)).ToList();
        static List<Pen> RandomPens = RandomBrushes.Select(b => new Pen(b, 1.5)).ToList();
        static SolidColorBrush GetRandomBrush(int index) => RandomBrushes.ElementAt(index % RandomBrushes.Count());
        static Pen GetRandomPen(int index) => RandomPens.ElementAt(index % RandomPens.Count());

        #endregion //drawing
    }
}
