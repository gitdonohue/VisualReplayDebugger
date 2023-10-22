// (c) 2021 Charles Donohue
// This code is licensed under MIT license (see LICENSE file for details)

using FontAwesomeIcon = FontAwesome.Sharp.IconChar;
using System;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace VisualReplayDebugger.Panels;

class ViewportPanel : DockPanelWithToolbar, IDisposable
{
    public ViewportPanel(MainWindow mainwindow)
    {
        var replayViewport3d = new ReplayViewportWithOverlay(mainwindow.TimelineWindow, mainwindow.EntitySelection, mainwindow.VisibleEntities);
        this.Content = replayViewport3d;
        replayViewport3d.CameraManuallyMoved += () => mainwindow.Play.Set(false);
        
        mainwindow.ReplayChanged += (replay) => replayViewport3d.Replay = replay;
        mainwindow.FocusOnSelected += replayViewport3d.FocusOnSelection;

        replayViewport3d.FollowCameraEnabled.Changed += () => mainwindow.Play.Set(false);

        var cam_follow = new ToggleButton() { Content = IconProvider.GetIcon(FontAwesomeIcon.Camera), ToolTip = "Follow main camera" };
        cam_follow.BindTo(replayViewport3d.FollowCameraEnabled);
        ToolBar.Items.Add(cam_follow);

        var focusOnSelected = new Button() { Content = IconProvider.GetIcon(FontAwesomeIcon.Binoculars), ToolTip = "Focus on selection (F2)" };
        focusOnSelected.Click += (o, e) => mainwindow.TriggerFocusOnSelected();
        ToolBar.Items.Add(focusOnSelected);

        var selection_follow = new ToggleButton() { Content = IconProvider.GetIcon(FontAwesomeIcon.Eye), ToolTip = "Follow selection" };
        selection_follow.BindTo(replayViewport3d.FollowSelectionEnabled);
        ToolBar.Items.Add(selection_follow);

        ToolBar.Items.Add(new Separator());

        var showAllNames = new ToggleButton() { Content = IconProvider.GetIcon(FontAwesomeIcon.Font), ToolTip = "Show all entity names" };
        showAllNames.BindTo(replayViewport3d.ShowAllNames);
        ToolBar.Items.Add(showAllNames);

        var showAllPaths = new ToggleButton() { Content = IconProvider.GetIcon(FontAwesomeIcon.Car), ToolTip = "Show all entity paths" };
        showAllPaths.BindTo(replayViewport3d.ShowAllPaths);
        ToolBar.Items.Add(showAllPaths);

        var showPrimitives = new ToggleButton() { Content = IconProvider.GetIcon(FontAwesomeIcon.PaintBrush), ToolTip = "Show draw primitives" };
        showPrimitives.BindTo(replayViewport3d.ShowDrawPrimitives);
        ToolBar.Items.Add(showPrimitives);

        var showPrimitivesInRange = new ToggleButton() { Content = IconProvider.GetIcon(FontAwesomeIcon.ExchangeAlt), ToolTip = "Show all draw primitives in range" };
        showPrimitivesInRange.BindTo(replayViewport3d.ShowAllDrawPrimitivesInRange);
        ToolBar.Items.Add(showPrimitivesInRange);

        ToolBar.Items.Add(new Separator());

        var cb = new CheckComboBoxControl("Draw Categories");
        if (mainwindow.Replay != null) cb.SetItems(mainwindow.Replay.DrawCategories, true);
        mainwindow.ReplayChanged += (replay) => cb.SetItems(replay.DrawCategories, true);
        cb.Changed += () => replayViewport3d.DrawCategoryFilter.Set(cb.UnselectedItems);
        ToolBar.Items.Add(cb);

        var cb2 = new CheckComboBoxControl("Draw Colors");
        if (mainwindow.Replay != null) cb2.SetItems(mainwindow.Replay.DrawColors.Select(x => x.ToString()), true);
        mainwindow.ReplayChanged += (replay) => cb2.SetItems(replay.DrawColors.Select(x => x.ToString()), true);
        cb2.Changed += () => replayViewport3d.DrawColorFilter.Set(cb2.UnselectedItems.Select(x => x.ToColor()));
        ToolBar.Items.Add(cb2);

        ToolBar.Items.Add(new Separator());
        
        var duplicatePanel = new Button() { Content = IconProvider.GetIcon(FontAwesomeIcon.AngleUp), ToolTip = "Duplicate Panel" };
        duplicatePanel.Click += (o, e) => mainwindow.DuplicatePanel(mainwindow.ViewportWindow);
        ToolBar.Items.Add(duplicatePanel);
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }
}
