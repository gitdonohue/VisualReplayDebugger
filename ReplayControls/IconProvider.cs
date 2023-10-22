// (c) 2021 Charles Donohue
// This code is licensed under MIT license (see LICENSE file for details)

using System.Windows.Controls;
using System.Windows.Media;
using FontAwesome.Sharp;

namespace VisualReplayDebugger;

public static class IconProvider
{
    public static Image GetIcon(FontAwesome.Sharp.IconChar icon, int width = 24, int height = 24) => new() { Source = icon.ToImageSource(Brushes.Black, width) };

}
