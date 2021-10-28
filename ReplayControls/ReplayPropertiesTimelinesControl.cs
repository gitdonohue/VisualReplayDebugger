using ReplayCapture;
using SelectionSet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Timeline;
using WatchedVariable;

namespace VisualReplayDebugger
{
    class ReplayPropertiesTimelinesControl :  UserControl, IDisposable
    {
        public WatchedVariable<string> SearchText { get; } = new();
        public WatchedBool EntitySelectionLocked { get; } = new(false);
        private SelectionGroup<Entity> EntitySelection;
        private IEnumerable<Entity> SelectedEntities => EntitySelectionLocked ? LockedSelection : EntitySelection.SelectionSet;
        private List<Entity> LockedSelection = new();

        public WatchedBool StackedByParameterDepth { get; } = new(true);
        public SelectionGroup<string> ParameterFilter { get; } = new();

        private ITimelineWindow TimelineWindow;

        private int ChannelHeight => 20;
        private double ChannelTextSize => 8;
        private static System.Globalization.CultureInfo TextCultureInfo = System.Globalization.CultureInfo.GetCultureInfo("en-us");
        private static Typeface TextTypeface = new Typeface("Arial");

        private Pen CursorPen = new Pen(Brushes.Red, 1);
        private Pen InterEntityPen = new Pen(Brushes.Black, 1);

        public ReplayCaptureReader replay;
        public ReplayCaptureReader Replay
        {
            get => replay;
            set
            {
                replay = value;
                EntitySelectionLocked.Set(false);
            }
        }

        public ReplayPropertiesTimelinesControl(ITimelineWindow timeLineWindow, ReplayCaptureReader replay, SelectionGroup<Entity> selectionset)
        {
            TimelineWindow = timeLineWindow;
            Replay = replay;

            TimelineWindow.Changed += TimelineWindow_Changed;
            EntitySelection = selectionset;
            EntitySelection.Changed += EntitySelection_Changed;

            EntitySelectionLocked.Changed += SetDirty;
            ParameterFilter.Changed += SetDirty;
            StackedByParameterDepth.Changed += SetDirty;

            this.MouseDown += OnMouseDown;
            this.MouseUp += OnMouseUp;
            this.MouseMove += OnMouseMove;
        }

        public void Dispose()
        {
            TimelineWindow.Changed -= TimelineWindow_Changed;
            EntitySelection.Changed -= EntitySelection_Changed;
        }

        private void TimelineWindow_Changed()
        {
            SetDirty();
        }

        private void EntitySelection_Changed()
        {
            if (!EntitySelectionLocked)
            {
                LockedSelection.Clear();
                LockedSelection.AddRange(EntitySelection.SelectionSet);

                Height = CalcHeight();
                SetDirty();
            }
        }

        private void SetDirty()
        {
            InvalidateVisual();
        }

        IEnumerable<Entity> EnumerateSelectedEntitiesWithDynamicProperties()
        {
            foreach (var entity in SelectedEntities)
            {
                if (Replay.EntityDynamicParams.ContainsKey(entity))
                {
                    yield return entity;
                }
            }
        }

        IEnumerable<(Entity, string, List<ReplayCaptureReader.DynamicParamTimeEntry>)> EnumerateSelectedEntitiesPropertiesChannels()
        {
            foreach (var entity in EnumerateSelectedEntitiesWithDynamicProperties())
            {
                if (Replay.EntityDynamicParams.TryGetValue(entity, out var dict))
                {
                    foreach((string param, var lst) in dict)
                    {
                        if (!ParameterFilter.Contains(param))
                        {
                            yield return (entity, param, lst);
                        }
                    }
                }
            }
        }

        IEnumerable<(int,double,double,string)> EnumerateRanges(List<ReplayCaptureReader.DynamicParamTimeEntry> entries, double start, double end)
        {
            string currentVal = string.Empty;
            double currentStartTime = 0;
            int index = 0;
            foreach (var entry in entries)
            {
                if (entry.time < start)
                {

                }
                else if (entry.time > end)
                {
                    yield return (index++, currentStartTime, end, currentVal);
                    yield break;
                }
                else
                {
                    if (index == 0 && entry.time > start)
                    {
                        yield return (index++, start, entry.time, currentVal);
                    }
                    else
                    {
                        yield return (index++, currentStartTime, entry.time, currentVal);
                    }
                }
                currentVal = entry.val;
                currentStartTime = entry.time;
            }
            yield return (index++, currentStartTime, end, currentVal);
        }

        IEnumerable<(int, double, double, string, string)> EnumerateRangesAtDepth(List<ReplayCaptureReader.DynamicParamTimeEntry> entries, double start, double end, int depth)
        {
            string currentValueAtDepth = string.Empty;
            string currentFullValueAtDepth = string.Empty;
            double currentStartTime = 0;
            int index = 0;
            foreach (var entry in entries)
            {
                string entryValueAtDepth = entry.SplitValues.ElementAtOrDefault(depth);
                string entryFullValueAtDepth = string.Join('.', entry.SplitValues.Take(depth+1));

                if (entry.time < start)
                {

                }
                else if (entry.time > end)
                {
                    yield return (index++, currentStartTime, end, currentValueAtDepth, currentFullValueAtDepth);
                    yield break;
                }
                else if (entryValueAtDepth != currentValueAtDepth)
                {
                    if (index == 0 && entry.time > start)
                    {
                        yield return (index++, start, entry.time, currentValueAtDepth, currentFullValueAtDepth);
                    }
                    else
                    {
                        yield return (index++, currentStartTime, entry.time, currentValueAtDepth, currentFullValueAtDepth);
                    }
                }

                if (entryValueAtDepth != currentValueAtDepth)
                {
                    currentValueAtDepth = entryValueAtDepth;
                    currentFullValueAtDepth = entryFullValueAtDepth;
                    currentStartTime = entry.time;
                }
            }
            yield return (index++, currentStartTime, end, currentValueAtDepth, currentFullValueAtDepth);
        }

        private Rect Bounds => new Rect(0, 0, ActualWidth, ActualHeight);

        bool DrawLeafMode => !StackedByParameterDepth;

        private float CalcHeight()
        {
            int rowCount = 0;
            if (DrawLeafMode)
            {
                rowCount = EnumerateSelectedEntitiesPropertiesChannels().Count();
            }
            else
            {
                foreach ((Entity entity, string param, var entryList) in EnumerateSelectedEntitiesPropertiesChannels())
                {
                    int minDepth = entryList.Where(x => x.SplitValues.Any()).Select(x => x.depth).Min();
                    int maxDepth = entryList.Select(x => x.depth).Max();
                    rowCount += maxDepth - minDepth + 1;
                }
                
            }
            return rowCount * ChannelHeight;
        }

        protected override void OnRender(DrawingContext dc)
        {
            if (Replay == null) return;

            int channelPos = 0;
            int entityIndex = 0;
            double channelWidth = ActualWidth;
            Entity lastEntity = null;
            foreach ((Entity entity, string param, var entryList) in EnumerateSelectedEntitiesPropertiesChannels())
            {
                bool firstRowForEntity = false;
                if (lastEntity != entity)
                {
                    if (lastEntity != null) { ++entityIndex; }
                    firstRowForEntity = true;
                }
                lastEntity = entity;

                Rect channelBounds = new Rect(0, channelPos * ChannelHeight, ActualWidth, ChannelHeight);

                var WindowBrush = new SolidColorBrush() { Color = System.Windows.Media.Color.FromArgb(32, 0, 0, 0) };
                //dc.DrawRectangle(WindowBrush, null, channelBounds);

                var life = Replay.GetEntityLifeTime(entity);
                var entityStartTime = Replay.GetTimeForFrame(life.Start);
                var entityEndTime = Replay.GetTimeForFrame(life.End);

                int channelRows = 0;

                if (DrawLeafMode)
                {
                    foreach ((int index, double t1, double t2, string val) in EnumerateRanges(entryList, TimelineWindow.Start, TimelineWindow.End))
                    {
                        if (!string.IsNullOrEmpty(val))
                        {
                            double xStart = ((t1 - TimelineWindow.Start) / TimelineWindow.Range) * channelWidth;
                            double xWidth = ((t2 - t1) / TimelineWindow.Range) * channelWidth;
                            Rect entryBounds = new Rect(xStart, channelPos * ChannelHeight, xWidth, ChannelHeight);
                            GetValueBrushAndPen(param,val,out Brush brush,out Pen pen);
                            dc.DrawRectangle(brush, pen, entryBounds);

                            double txtSize = ChannelTextSize;
                            var txtPos = entryBounds.TopLeft;
                            txtPos.X += 2;
                            txtPos.Y += (ChannelHeight / 2) - txtSize / 2;
                            dc.DrawText(new FormattedText($"{val}",
                                    TextCultureInfo, FlowDirection.LeftToRight, TextTypeface,
                                    txtSize, pen.Brush, 1.25),
                                    txtPos);
                        }
                    }
                    ++channelPos;
                    ++channelRows;
                }
                else
                {
                    int minDepth = entryList.Where(x=>x.SplitValues.Any()).Select(x => x.depth).Min();
                    int maxDepth = entryList.Select(x => x.depth).Max();
                    for (int depth = minDepth; depth <= maxDepth; ++depth)
                    {
                        foreach ((int index, double t1, double t2, string val, string fullVal) in EnumerateRangesAtDepth(entryList, TimelineWindow.Start, TimelineWindow.End, depth))
                        {
                            if (!string.IsNullOrEmpty(val))
                            {
                                double xStart = ((t1 - TimelineWindow.Start) / TimelineWindow.Range) * channelWidth;
                                double xWidth = ((t2 - t1) / TimelineWindow.Range) * channelWidth;
                                Rect entryBounds = new Rect(xStart, channelPos * ChannelHeight, xWidth, ChannelHeight);
                                GetValueBrushAndPen(param, fullVal, out Brush brush, out Pen pen);
                                dc.DrawRectangle(brush, pen, entryBounds);

                                double txtSize = ChannelTextSize;
                                var txtPos = entryBounds.TopLeft;
                                txtPos.X += 2;
                                txtPos.Y += (ChannelHeight / 2) - txtSize / 2;
                                dc.DrawText( new FormattedText($"{val}",
                                        TextCultureInfo, FlowDirection.LeftToRight, TextTypeface,
                                        txtSize, pen.Brush, 1.25),
                                        txtPos);
                            }
                        }
                        ++channelRows;
                        ++channelPos;
                    }

                }

                if (entityStartTime > TimelineWindow.Start)
                {
                    double xEntityStart = ((entityStartTime - TimelineWindow.Start) / TimelineWindow.Range) * channelWidth;
                    Rect startBounds = new Rect(0, channelBounds.Top, xEntityStart, ChannelHeight * channelRows);
                    var grad = new LinearGradientBrush(Colors.GhostWhite, Colors.LightGray, 90);
                    dc.DrawRectangle(grad, null, startBounds);
                }

                if (entityEndTime < TimelineWindow.End)
                {
                    double xEntityEnd = (1 - ((TimelineWindow.End - entityEndTime) / TimelineWindow.Range)) * channelWidth;
                    Rect endBounds = new Rect(xEntityEnd, channelBounds.Top, channelWidth - xEntityEnd, ChannelHeight * channelRows);
                    var grad = new LinearGradientBrush(Colors.GhostWhite, Colors.LightGray, 90);
                    dc.DrawRectangle(grad, null, endBounds);
                }

                if (firstRowForEntity)
                {
                    dc.DrawText(new FormattedText($"{entity.Name}", TextCultureInfo, FlowDirection.LeftToRight, TextTypeface, 9, Brushes.Black, 1.25), channelBounds.TopLeft);
                }

                // Entity labels
                var labelPos = channelBounds.TopLeft;
                labelPos.X += 20;
                labelPos.Y += 7;
                dc.DrawText(new FormattedText($"{param}", TextCultureInfo, FlowDirection.LeftToRight, TextTypeface, 9, Brushes.Black, 1.25), labelPos);

                if (firstRowForEntity && entityIndex > 0)
                {
                    dc.DrawLine(InterEntityPen, channelBounds.TopLeft, channelBounds.TopRight);
                }

            }

            ////
            //// Cursor-dependent overlays
            ////

            // Draw cursor
            double cursorTime = TimelineWindow.Timeline.Cursor;
            int cursorFrame = Replay.GetFrameForTime(cursorTime);
            double r = Math.Clamp(TimelineWindow.CursorRatio, 0, 1);
            double cursorXPos = r * Bounds.Width;
            dc.DrawLine(CursorPen, new System.Windows.Point(cursorXPos, 0), new System.Windows.Point(cursorXPos, ActualHeight));

            base.OnRender(dc);
        }

        static Dictionary<string, (Brush,Pen)> ValueBrushes = new();
        static void GetValueBrushAndPen(string param, string val, out Brush brush, out Pen pen)
        {
            string k = $"{param}:{val}";
            if (!ValueBrushes.TryGetValue(k, out var brushAndPen))
            {
                var col = RandomColors[ValueBrushes.Count() % RandomColors.Count];
                brushAndPen = (new LinearGradientBrush(col, Lighten(col), 90),new Pen(new SolidColorBrush(Darken(col)), 0.5));
                ValueBrushes.Add(k, brushAndPen);
            }
            brush = brushAndPen.Item1;
            pen  = brushAndPen.Item2;
        }

        static List<System.Windows.Media.Color> RandomColors = new List<System.Windows.Media.Color>
        {
            Colors.LightGreen,
            Colors.LightCoral,
            Colors.LightGreen,
            Colors.LightBlue,
            Colors.YellowGreen,
            Colors.LightSalmon,
            Colors.LightYellow
            // TODO: add more colors
        };

        static List<SolidColorBrush> RandomBrushes = RandomColors.Select(c => new SolidColorBrush(c)).ToList();
        static List<Pen> RandomPens = RandomBrushes.Select(b => new Pen(b, 1.5)).ToList();
        static SolidColorBrush GetRandomBrush(int index) => RandomBrushes.ElementAt(index % RandomBrushes.Count());

        static System.Windows.Media.Color Lighten(System.Windows.Media.Color color)
        {
            var newcol = System.Windows.Forms.ControlPaint.LightLight(System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B));
            return System.Windows.Media.Color.FromArgb(newcol.A, newcol.R, newcol.G, newcol.B);
        }

        static System.Windows.Media.Color Darken(System.Windows.Media.Color color)
        {
            var newcol = System.Windows.Forms.ControlPaint.Dark(System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B));
            return System.Windows.Media.Color.FromArgb(newcol.A, newcol.R, newcol.G, newcol.B);
        }


        #region Mouse Handling

        //
        // TODO: This code is almost exactly the same as for the graphview, it should be shared
        //

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

        #endregion //Mouse Handling
    }
}
