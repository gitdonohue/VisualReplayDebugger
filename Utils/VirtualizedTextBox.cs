// (c) 2021 Charles Donohue
// This code is licensed under MIT license (see LICENSE file for details)

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace VisualReplayDebugger
{
    public class VirtualizedTextBox : VirtualizingStackPanel
    {
        public record TextEntry
        {
            public string Text;
            public int Index;
        }

        public ObservableCollection<TextEntry> TextEntries { get; private set; } = new();

        private ListBox Container;

        public VirtualizedTextBox()
        {
            this.Orientation = Orientation.Vertical;
            this.CanHorizontallyScroll = false;
            this.CanVerticallyScroll = true;

            Container = new();
            SetIsVirtualizing(Container, true);
            SetVirtualizationMode(Container, VirtualizationMode.Standard);
            Container.ItemsSource = TextEntries;
            this.Children.Add(Container);
        }

    }
}
