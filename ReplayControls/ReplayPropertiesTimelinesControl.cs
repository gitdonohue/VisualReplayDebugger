﻿using ReplayCapture;
using SelectionSet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Timeline;
using WatchedVariable;

namespace VisualReplayDebugger;

class ReplayPropertiesTimelinesControl :  UserControl, IDisposable
{
    public WatchedVariable<string> FilterText { get; } = new();
    public WatchedBool EntitySelectionLocked { get; } = new(false);
    private readonly SelectionGroup<Entity> EntitySelection;
    private List<Entity> SelectedEntities { get; } = new();
    private const int MaxSelectedElements = 10;

    public WatchedBool StackedByParameterDepth { get; } = new(true);
    public WatchedBool StackedByParameterDepthOnlyChanges { get; } = new(false);
    public SelectionGroup<string> ParameterFilter { get; } = new();

    private readonly ITimelineWindow TimelineWindow;

    public ColorProvider ColorProvider { get; private set; }
    private int ChannelHeight => 20;
    private double ChannelTextSize => 8;
    private static readonly System.Globalization.CultureInfo TextCultureInfo = System.Globalization.CultureInfo.GetCultureInfo("en-us");
    private static readonly Typeface TextTypeface = new("Arial");

    private readonly Pen CursorPen = new(Brushes.Red, 1);
    private readonly Pen InterEntityPen = new(Brushes.Black, 1);

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

    readonly VisualReplayDebugger.ReplayControls.TimelineMouseControlHandler MouseHandler;

    public ReplayPropertiesTimelinesControl(ITimelineWindow timeLineWindow, ReplayCaptureReader replay, SelectionGroup<Entity> selectionset, ColorProvider colorProvider)
    {
        TimelineWindow = timeLineWindow;
        Replay = replay;
        ColorProvider = colorProvider;

        TimelineWindow.Changed += TimelineWindow_Changed;
        EntitySelection = selectionset;
        EntitySelection.Changed += EntitySelection_Changed;

        EntitySelectionLocked.Changed += SetDirty;
        ParameterFilter.Changed += SetDirty;
        StackedByParameterDepth.Changed += SetDirty;
        StackedByParameterDepthOnlyChanges.Changed += SetDirty;
        FilterText.Changed += SetDirty;


        MouseHandler = new(TimelineWindow, this, windowMode: false);
        this.MouseDown += MouseHandler.OnMouseDown;
        this.MouseUp += MouseHandler.OnMouseUp;
        this.MouseMove += MouseHandler.OnMouseMove;
        this.MouseWheel += MouseHandler.OnMouseWheel;

        this.MouseMove += OnMouseMove;
        this.MouseDoubleClick += OnMouseDoubleClick;
        this.MouseLeave += OnMouseLeave;

        this.ToolTip = new TextBlock() {};
    }

    public void Dispose()
    {
        TimelineWindow.Changed -= TimelineWindow_Changed;
        EntitySelection.Changed -= EntitySelection_Changed;
    }

    public void MoveToPrevEvent()
    {
        if (DrawChannels.Any())
        {
            // Find closest transition to left
            double t = TimelineWindow.Timeline.Cursor;
            var closest = EnumerateAllTransitions().Where(x => x.time < t).OrderBy(x => t - x.time).FirstOrDefault();
            if (closest.drawblock != null)
            {
                TimelineWindow.Timeline.Cursor = closest.time;
            }
        }
    }

    public void MoveToNextEvent()
    {
        if (DrawChannels.Any())
        {
            // Find closest transition to right
            double t = TimelineWindow.Timeline.Cursor;
            var closest = EnumerateAllTransitions().Where(x => x.time > t).OrderBy(x => x.time - t).FirstOrDefault();
            if (closest.drawblock != null)
            {
                TimelineWindow.Timeline.Cursor = closest.time;
            }
        }
    }

    public void ZoomSmallestOnCursor()
    {
        if (DrawChannels.Any())
        {
            double t = TimelineWindow.Timeline.Cursor + double.Epsilon;
            var smallest = EnumerateAllBlocks().Where(b => b.startTime <= t && b.endTime > t).OrderBy(b => b.endTime - b.startTime).FirstOrDefault();
            ZoomOnBlock(smallest);
        }
    }

    private IEnumerable<DrawBlock> EnumerateAllBlocks()
    {
        foreach (var channel in DrawChannels)
        {
            foreach (var block in channel.drawBlocks)
            {
                yield return block;
            }
        }
    }

    private IEnumerable<(double time,DrawBlock drawblock)> EnumerateAllTransitions()
    {
        foreach (var block in EnumerateAllBlocks())
        {
            yield return (block.startTime, block);
            yield return (block.endTime, block);
        }
    }

    private void TimelineWindow_Changed()
    {
        SetDirty();
    }

    private void EntitySelection_Changed()
    {
        if (!EntitySelectionLocked)
        {
            SelectedEntities.Clear();
            SelectedEntities.AddRange(EntitySelection.SelectionSet.Take(MaxSelectedElements));
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
        var filter = new SearchContext(FilterText.Value);
        foreach (var entity in EnumerateSelectedEntitiesWithDynamicProperties())
        {
            if (Replay.EntityDynamicParams.TryGetValue(entity, out var dict))
            {
                foreach((string param, var lst) in dict)
                {
                    if (!ParameterFilter.Contains(param)
                        && filter.Match(param) )
                    {
                        yield return (entity, param, lst);
                    }
                }
            }
        }
    }

    record DynamicParamsEntryRange(int index, double t1, double t2, double rt1, double rt2, string val);
    IEnumerable<DynamicParamsEntryRange> EnumerateRanges(List<ReplayCaptureReader.DynamicParamTimeEntry> entries, double start, double end)
    {
        string currentVal = string.Empty;
        double currentStartTime = 0;
        double currentRealStartTime = 0;
        int index = 0;
        foreach (var entry in entries)
        {
            if (entry.time < start)
            {

            }
            else if (entry.time > end)
            {
                yield return new(index++, currentStartTime, end, currentRealStartTime, entry.time, currentVal);
                yield break;
            }
            else
            {
                if (index == 0 && entry.time > start)
                {
                    yield return new(index++, start, entry.time, currentRealStartTime, entry.time, currentVal);
                }
                else
                {
                    yield return new(index++, currentStartTime, entry.time, currentRealStartTime, entry.time, currentVal);
                }
            }
            if (entry.val != currentVal) { currentRealStartTime = entry.time; }
            currentVal = entry.val;
            currentStartTime = entry.time;
        }
        yield return new(index++, currentStartTime, end, currentRealStartTime, end, currentVal);
    }

    record DynamicParamTimeEntryRange(int index, double t1, double t2, double rt1, double rt2, string val, string fullVal);
    IEnumerable<DynamicParamTimeEntryRange> EnumerateRangesAtDepth(List<ReplayCaptureReader.DynamicParamTimeEntry> entries, double start, double end, int depth)
    {
        string currentValueAtDepth = string.Empty;
        string currentFullValueAtDepth = string.Empty;
        double currentStartTime = 0;
        double currentRealStartTime = 0;
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
                yield return new(index++, currentStartTime, end, currentRealStartTime, entry.time, currentValueAtDepth, currentFullValueAtDepth);
                yield break;
            }
            else if (entryValueAtDepth != currentValueAtDepth)
            {
                if (index == 0 && entry.time > start)
                {
                    yield return new(index++, start, entry.time, currentRealStartTime, entry.time, currentValueAtDepth, currentFullValueAtDepth);
                }
                else
                {
                    yield return new(index++, currentStartTime, entry.time, currentRealStartTime, entry.time, currentValueAtDepth, currentFullValueAtDepth);
                }
            }

            if (entryValueAtDepth != currentValueAtDepth)
            {
                currentValueAtDepth = entryValueAtDepth;
                currentFullValueAtDepth = entryFullValueAtDepth;
                currentStartTime = entry.time;
                currentRealStartTime = entry.time;
            }
        }
        yield return new(index++, currentStartTime, end, currentStartTime, end, currentValueAtDepth, currentFullValueAtDepth);
    }

    private Rect Bounds => new(0, 0, ActualWidth, ActualHeight);

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
                int minDepth = StackedByParameterDepthOnlyChanges ? entryList.Where(x => x.SplitValues.Any()).Select(x => x.depth).DefaultIfEmpty().Min() : 0;
                int maxDepth = entryList.Select(x => x.depth).DefaultIfEmpty().Max();
                rowCount += maxDepth - minDepth + 1;
            }
            
        }
        return rowCount * ChannelHeight;
    }

    class DrawChannel
    {
        public Entity entity;
        public string parameter;
        public List<DrawBlock> drawBlocks;
        public Rect bounds;
        public Rect startBounds;
        public Rect endBounds;
    }

    class DrawBlock
    {
        public DrawChannel channel;
        public Rect bounds;
        public string val;
        public string fullval;
        public double startTime;
        public double endTime;

        public string RawValue => fullval ?? val;
        public string FullValue => Stripped(RawValue);
        public string ShortValue => Stripped(val);

        public string Stripped(string txt) => txt.Split('|').FirstOrDefault()?.Trim() ?? string.Empty;
    }

    private readonly List<DrawChannel> DrawChannels = new();
    private readonly List<(int, double, double)> FramesTimeLineWindowRatios = new();

    private void PrepRender()
    {
        if (Replay == null) return;

        DrawChannels.Clear();

        int channelPos = 0;
        double channelWidth = ActualWidth;
        foreach ((Entity entity, string param, var entryList) in EnumerateSelectedEntitiesPropertiesChannels())
        {
            DrawChannel channel = new() { entity = entity, parameter = param, drawBlocks = new() };

            Rect channelBounds = new Rect(0, channelPos * ChannelHeight, ActualWidth, ChannelHeight);
            
            var life = Replay.GetEntityLifeTime(entity);
            var entityStartTime = Replay.GetTimeForFrame(life.Start);
            var entityEndTime = Replay.GetTimeForFrame(life.End);

            int channelRows = 0;

            if (DrawLeafMode)
            {
                foreach ((int index, double t1, double t2, double rt1, double rt2, string val) in EnumerateRanges(entryList, TimelineWindow.Start, TimelineWindow.End))
                {
                    if (!string.IsNullOrEmpty(val))
                    {
                        double xStart = ((t1 - TimelineWindow.Start) / TimelineWindow.Range) * channelWidth;
                        double xWidth = ((t2 - t1) / TimelineWindow.Range) * channelWidth;
                        Rect entryBounds = new Rect(xStart, channelPos * ChannelHeight, xWidth, ChannelHeight);
                        channel.drawBlocks.Add(new DrawBlock() { channel = channel, bounds = entryBounds, val = val, startTime = rt1, endTime = rt2 });
                    }
                }
                ++channelPos;
                ++channelRows;
            }
            else
            {
                int minDepth = StackedByParameterDepthOnlyChanges ? entryList.Where(x => x.SplitValues.Any()).Select(x => x.depth).DefaultIfEmpty().Min() : 0;
                int maxDepth = entryList.Select(x => x.depth).DefaultIfEmpty().Max();
                for (int depth = minDepth; depth <= maxDepth; ++depth)
                {
                    foreach ((int index, double t1, double t2, double rt1, double rt2, string val, string fullVal) in EnumerateRangesAtDepth(entryList, TimelineWindow.Start, TimelineWindow.End, depth))
                    {
                        if (!string.IsNullOrEmpty(val))
                        {
                            double xStart = ((t1 - TimelineWindow.Start) / TimelineWindow.Range) * channelWidth;
                            double xWidth = ((t2 - t1) / TimelineWindow.Range) * channelWidth;
                            Rect entryBounds = new Rect(xStart, channelPos * ChannelHeight, xWidth, ChannelHeight);
                            channel.drawBlocks.Add(new DrawBlock() { channel = channel, bounds = entryBounds, val = val, fullval = fullVal, startTime = rt1, endTime = rt2 });
                        }
                    }
                    ++channelRows;
                    ++channelPos;
                }

            }

            channelBounds.Height = channelRows * ChannelHeight;

            if (entityStartTime > TimelineWindow.Start)
            {
                double xEntityStart = ((entityStartTime - TimelineWindow.Start) / TimelineWindow.Range) * channelWidth;
                Rect startBounds = new Rect(0, channelBounds.Top, xEntityStart, ChannelHeight * channelRows);
                channel.startBounds = startBounds;
            }

            if (entityEndTime < TimelineWindow.End)
            {
                double xEntityEnd = (1 - ((TimelineWindow.End - entityEndTime) / TimelineWindow.Range)) * channelWidth;
                Rect endBounds = new Rect(xEntityEnd, channelBounds.Top, channelWidth - xEntityEnd, ChannelHeight * channelRows);
                channel.endBounds = endBounds;
            }

            channel.bounds = channelBounds;
            DrawChannels.Add(channel);
        }

        FramesTimeLineWindowRatios.Clear();
        FramesTimeLineWindowRatios.AddRange(Replay.GetWindowFrameRatios(TimelineWindow));

        _blockUnderMouse = FindBlockAtPos(MouseHandler.MouseLastPos);
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (Replay == null) return;
        
        PrepRender();

        // Alternating background colors for frame delemitation
        var bgLight = new SolidColorBrush(Colors.White);
        var bgDark = new SolidColorBrush(Colors.LightGray);
        dc.DrawRectangle(bgLight, null, new Rect(0, 0, Bounds.Width, Bounds.Height));
        foreach ((int frameNum, double ratioLeft, double ratioRight) in FramesTimeLineWindowRatios)
        {
            double w = Bounds.Width * (ratioRight - ratioLeft);
            if ((frameNum % 2) == 0 && (w > 2))
            {
                dc.DrawRectangle(bgDark, null, new Rect(Bounds.Width * ratioLeft, 0, w, Bounds.Height));
            }
        }

        var outOfBoundsGradient = new LinearGradientBrush(Colors.GhostWhite, Colors.LightGray, 90);

        var channelGroups = DrawChannels.GroupBy(x => x.entity);
        int groupsCount = channelGroups.Count();
        int groupNum = 0;
        foreach (var channelGroup in channelGroups)
        {
            int channelForEntityCount = 0;
            foreach(var channel in channelGroup)
            {
                foreach (DrawBlock block in channel.drawBlocks)
                {
                    var baseColor = ColorProvider.GetLabelColor(block.FullValue);
                    var brush = new LinearGradientBrush(ColorProvider.Lighten(baseColor), ColorProvider.LightenLighten(baseColor), 90);
                    var pen = new Pen(new SolidColorBrush(ColorProvider.Darken(baseColor)), 0.5);
                    if (block == BlockUnderMouse) 
                    { 
                        pen = new Pen(new SolidColorBrush(Colors.White), 0.5); 
                    }
                    dc.DrawRectangle(brush, pen, block.bounds);

                    double txtSize = ChannelTextSize;
                    var txtPos = block.bounds.TopLeft;
                    txtPos.X += 2;
                    txtPos.Y += (ChannelHeight / 2) - txtSize / 2;
                    dc.DrawText(new FormattedText($"{block.ShortValue}",
                            TextCultureInfo, FlowDirection.LeftToRight, TextTypeface,
                            txtSize, pen.Brush, 1.25),
                            txtPos);
                }

                if (channel.startBounds.Width > 0)
                {
                    dc.DrawRectangle(outOfBoundsGradient, null, channel.startBounds);
                }

                if (channel.endBounds.Width > 0)
                {
                    dc.DrawRectangle(outOfBoundsGradient, null, channel.endBounds);
                }

                // Entity labels
                var labelPos = channel.bounds.TopLeft;
                labelPos.X += 20;
                labelPos.Y += 7;
                dc.DrawText(new FormattedText($"{channel.parameter}", TextCultureInfo, FlowDirection.LeftToRight, TextTypeface, 9, Brushes.Black, 1.25), labelPos);

                if (channelForEntityCount == 0)
                {
                    dc.DrawText(new FormattedText($"{channel.entity.Name}", TextCultureInfo, FlowDirection.LeftToRight, TextTypeface, 9, Brushes.Black, 1.25), channel.bounds.TopLeft);
                }

                // Line between entities
                if (channelForEntityCount == 0 && groupNum > 0)
                {
                    dc.DrawLine(InterEntityPen, channel.bounds.TopLeft, channel.bounds.TopRight);
                }

                ++channelForEntityCount;
            }
            ++groupNum;
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

    private DrawBlock _blockUnderMouse;
    private DrawBlock BlockUnderMouse
    {
        get => _blockUnderMouse;
        set
        {
            if (value != _blockUnderMouse )
            {
                _blockUnderMouse = value;
                SetDirty();
            }
        }
    }

    private DrawBlock FindBlockAtPos(System.Windows.Point pos)
    {
        if (DrawChannels.Any())
        {
            foreach (var channel in DrawChannels)
            {
                if ( channel.bounds.Contains(pos) )
                {
                    foreach (var block in channel.drawBlocks)
                    {
                        if (block.bounds.Contains(pos))
                        {
                            return block;
                        }
                    }
                    break;
                }
            }
        }
        return null;
    }

    private void RefreshTooltipText()
    {
        if (BlockUnderMouse != null)
        {
            string txt = $"{BlockUnderMouse.channel.entity.Name}\n{BlockUnderMouse.channel.parameter} : {(BlockUnderMouse.RawValue)}";
            SetTooltipText(txt);
        }
    }

    private void SetTooltipText(string text)
    {
        if (this.ToolTip is TextBlock tt && text != null)
        {
            tt.Text = text;
        }
    }

    #region Mouse Handling

    private void OnMouseLeave(object sender, MouseEventArgs e)
    {
        BlockUnderMouse = null;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        BlockUnderMouse = FindBlockAtPos(e.GetPosition(this));
        RefreshTooltipText();
    }

    private void OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        ZoomOnBlock(BlockUnderMouse);
    }

    private void ZoomOnBlock(DrawBlock block)
    {
        if (block != null)
        {
            TimelineWindow.Start = block.startTime;
            TimelineWindow.End = block.endTime;
            double midpoint = TimelineWindow.Start + (TimelineWindow.End - TimelineWindow.Start) / 2;
            TimelineWindow.ScaleWindow(1.2, midpoint);
        }
    }

    #endregion //Mouse Handling
}
