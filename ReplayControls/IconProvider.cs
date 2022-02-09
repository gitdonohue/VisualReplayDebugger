using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;
using FontAwesome.Sharp;
//using FontAwesomeIcon = FontAwesome.Sharp.IconChar;

namespace VisualReplayDebugger
{
    public static class IconProvider
    {
        public static Image GetIcon(FontAwesome.Sharp.IconChar icon, int width = 14, int height = 14) => new Image() { Source = icon.ToImageSource(Brushes.Black, width) };
        //public static Image GetIcon(FontAwesomeIcon icon, int width = 14, int height = 14) => new ImageAwesome { Icon = icon, Width = width, Height = height };

    }
}
