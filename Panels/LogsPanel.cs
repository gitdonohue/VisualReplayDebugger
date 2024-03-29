﻿// (c) 2021 Charles Donohue
// This code is licensed under MIT license (see LICENSE file for details)

//using FontAwesome.WPF;
using FontAwesomeIcon = FontAwesome.Sharp.IconChar;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace VisualReplayDebugger.Panels;

class LogsPanel : DockPanelWithToolbar, IDisposable
{
    public LogsPanel(MainWindow mainwindow)
        : base(scrolling: true)
    {
        var replayLogsView = new ReplayLogsControlEx2(mainwindow.Replay, mainwindow.TimelineWindow, mainwindow.EntitySelection, mainwindow.HiddenEntities, mainwindow.StarredEntities);

        this.Content = replayLogsView;
        mainwindow.ReplayChanged += (replay) => replayLogsView.Replay = replay;

        replayLogsView.ScrollOwner = this.ScrollViewer;
        this.LayoutUpdated += (o,e) => replayLogsView.ScrollingUpdated(this.ScrollViewer);

        var showonlyselectedlogs = new ToggleButton() { Content = IconProvider.GetIcon(FontAwesomeIcon.MousePointer), ToolTip = "Show Logs for selected entities only" };
        showonlyselectedlogs.BindTo(replayLogsView.ShowSelectedLogsOnly);
        ToolBar.Items.Add(showonlyselectedlogs);

        var filtertext = new TextBox() { Width = 150 };
        filtertext.BindTo(replayLogsView.FilterText);
        ToolBar.Items.Add(new Label() { Content = IconProvider.GetIcon(FontAwesomeIcon.Filter) });
        ToolBar.Items.Add(filtertext);

        var cb = new CheckComboBoxControl("Log Categories");
        if (mainwindow.Replay != null) cb.SetItems(mainwindow.Replay.LogCategories, true);
        mainwindow.ReplayChanged += (replay) => cb.SetItems(replay.LogCategories, true);
        cb.Changed += () => replayLogsView.LogCategoryFilter.Set(cb.UnselectedItems);
        ToolBar.Items.Add(new Label() { Content = IconProvider.GetIcon(FontAwesomeIcon.Filter) });
        ToolBar.Items.Add(cb);

        var cb2 = new CheckComboBoxControl("Log Colors");
        if (mainwindow.Replay != null) cb2.SetItems(mainwindow.Replay.LogColors.Select(x => x.ToString()), true);
        mainwindow.ReplayChanged += (replay) => cb2.SetItems(replay.LogColors.Select(x=>x.ToString()), true);
        cb2.Changed += () => replayLogsView.LogColorFilter.Set(cb2.UnselectedItems.Select(x=>x.ToColor()));
        ToolBar.Items.Add(new Label() { Content = IconProvider.GetIcon(FontAwesomeIcon.Filter) });
        ToolBar.Items.Add(cb2);

        ToolBar.Items.Add(new Separator());

        var searchText = new TextBox() { Width = 150 };
        searchText.BindTo(replayLogsView.SearchText);
        ToolBar.Items.Add(new Label() { Content = IconProvider.GetIcon(FontAwesomeIcon.Search) });
        ToolBar.Items.Add(searchText);
        mainwindow.FindCalled += () => searchText.Focus();

        var prev = new Button() { Content = IconProvider.GetIcon(FontAwesomeIcon.ArrowLeft), ToolTip = "Move to previous event (Shift+F3)" };
        prev.Click += (o, e) => replayLogsView.JumpToPreviousSearchResult();
        mainwindow.JumpToPrevious += replayLogsView.JumpToPreviousSearchResult;
        ToolBar.Items.Add(prev);

        var next = new Button() { Content = IconProvider.GetIcon(FontAwesomeIcon.ArrowRight), ToolTip = "Move to next event (F3)" };
        next.Click += (o, e) => replayLogsView.JumpToNextSearchResult();
        mainwindow.JumpToNext += replayLogsView.JumpToNextSearchResult;
        ToolBar.Items.Add(next);

        ToolBar.Items.Add(new Separator());

        var lockEntitySelection = new ToggleButton() { Content = IconProvider.GetIcon(FontAwesomeIcon.Lock), ToolTip = "Lock selection" };
        lockEntitySelection.BindTo(replayLogsView.EntitySelectionLocked);
        ToolBar.Items.Add(lockEntitySelection);

        var duplicatePanel = new Button() { Content = IconProvider.GetIcon(FontAwesomeIcon.Upload), ToolTip = "Duplicate Panel" };
        duplicatePanel.Click += (o, e) => mainwindow.DuplicatePanel(mainwindow.LogsWindow);
        ToolBar.Items.Add(duplicatePanel);

        mainwindow.CopyCalled += () => { Clipboard.SetText(replayLogsView.GetText()); };
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }
}
