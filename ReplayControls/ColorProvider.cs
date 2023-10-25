// (c) 2021 Charles Donohue
// This code is licensed under MIT license (see LICENSE file for details)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;

namespace VisualReplayDebugger;

public class ColorProvider
{
    static public List<System.Windows.Media.Color> RandomColors = new List<System.Windows.Media.Color>
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

    static public System.Windows.Media.Color Lighten(System.Windows.Media.Color color)
    {
        var newcol = System.Windows.Forms.ControlPaint.Light(System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B));
        return System.Windows.Media.Color.FromArgb(newcol.A, newcol.R, newcol.G, newcol.B);
    }

    static public System.Windows.Media.Color LightenLighten(System.Windows.Media.Color color)
    {
        var newcol = System.Windows.Forms.ControlPaint.LightLight(System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B));
        return System.Windows.Media.Color.FromArgb(newcol.A, newcol.R, newcol.G, newcol.B);
    }

    static public System.Windows.Media.Color Darken(System.Windows.Media.Color color)
    {
        var newcol = System.Windows.Forms.ControlPaint.Dark(System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B));
        return System.Windows.Media.Color.FromArgb(newcol.A, newcol.R, newcol.G, newcol.B);
    }

    static public Color Saturate(Color c, double saturation)
    {
        // Values from: https://www.w3.org/TR/filter-effects-1/#feColorMatrixElement , type="saturate"
        var s = saturation;
        Func<double, byte> clamp = i => (byte)Math.Min(255, Math.Max(0, Convert.ToInt32(i)));
        return Color.FromArgb(255,
           clamp((0.213 + 0.787 * s) * c.R + (0.715 - 0.715 * s) * c.G + (0.072 - 0.072 * s) * c.B),
           clamp((0.213 - 0.213 * s) * c.R + (0.715 + 0.285 * s) * c.G + (0.072 - 0.072 * s) * c.B),
           clamp((0.213 - 0.213 * s) * c.R + (0.715 - 0.715 * s) * c.G + (0.072 + 0.928 * s) * c.B));
    }

    Dictionary<string, Color> LabelColors = new();
    public Color GetLabelColor(string label)
    {
        label = label.Split('.').Last();
        if (!LabelColors.TryGetValue(label, out Color basecolor))
        {
            basecolor = RandomColors[LabelColors.Count() % RandomColors.Count];
            LabelColors.Add(label, basecolor);
        }
        return basecolor;
    }
}
