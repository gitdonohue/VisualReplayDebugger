// (c) 2021 Charles Donohue
// This code is licensed under MIT license (see LICENSE file for details)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using ReplayCapture;
using WatchedVariable;

namespace VisualReplayDebugger;

public static class Extensions
{
    public static void BindTo(this ToggleButton togglebutton, WatchedBool b)
    {
        togglebutton.IsChecked = b;
        togglebutton.Click += (o, e) => b.Set(togglebutton.IsChecked.Value);
        b.Changed += () => { togglebutton.IsChecked = b; };
    }

    public static void BindTo(this MenuItem menuItem, WatchedBool b)
    {
        menuItem.IsChecked = b;
        menuItem.Click += (o, e) => b.Set(menuItem.IsChecked);
        b.Changed += () => { menuItem.IsChecked = b; };
    }

    public static void BindVisibilityTo(this UIElement uielement, WatchedBool b)
    {
        b.Changed += () => { uielement.Visibility = b.Value ? Visibility.Visible : Visibility.Collapsed; };
    }

    public static void BindTo(this TextBox textbox, WatchedVariable<string> s)
    {
        textbox.Text = s;
        textbox.TextChanged += (o, e) => s.Set(textbox.Text);
        s.Changed += () => { textbox.Text = s; };
    }

    public static IEnumerable<System.Windows.Point> StepPoints(this IEnumerable<System.Windows.Point> src)
    {
        if (src.Any())
        {
            System.Windows.Point lastPoint = src.First();
            foreach (var p in src)
            {
                yield return new System.Windows.Point(p.X, lastPoint.Y);
                yield return p;
                lastPoint = p;
            }
        }
    }

    public static IEnumerable<T> InsertOnce<T>(this IEnumerable<T> src, Func<T,bool> predicate, T itemToInsert)
    {
        bool inserted = false;
        foreach(var elem in src)
        {
            if (!inserted && predicate.Invoke(elem))
            {
                yield return itemToInsert;
                inserted = true;
            }
            yield return elem;
        }
    }

    public static System.Windows.Point WithX(this System.Windows.Point p, double x) => new(x, p.Y);
    public static System.Windows.Point WithXOffset(this System.Windows.Point p, double x) => new(p.X + x, p.Y);
    public static System.Windows.Point WithY(this System.Windows.Point p, double y) => new(p.X, y);
    public static System.Windows.Point WithYOffset(this System.Windows.Point p, double y) => new(p.X, p.Y + y);
    public static System.Windows.Media.Color WithAlpha(this System.Windows.Media.Color c, byte alpha) => System.Windows.Media.Color.FromArgb(alpha, c.R, c.G, c.B);
    public static System.Windows.Media.Color WithAlpha(this System.Windows.Media.Color c, double alpha) => c.WithAlpha((byte)(alpha * 0xFF));
    public static SolidColorBrush WithAlpha(this SolidColorBrush b, byte alpha) => new(b.Color.WithAlpha(alpha));
    public static SolidColorBrush WithAlpha(this SolidColorBrush b, double alpha) => b.WithAlpha((byte)(alpha * 0xFF));

    public static Pen WithAlpha(this Pen pen, byte alpha) => new((pen.Brush as SolidColorBrush).WithAlpha(alpha), pen.Thickness);
    public static Pen WithAlpha(this Pen pen, double alpha) => pen.WithAlpha((byte)(alpha * 0xFF));


    private static Dictionary<string, ReplayCapture.Color> _colorTranslationTable;
    public static ReplayCapture.Color ToColor(this string colorName)
    {
        if (_colorTranslationTable == null)
        {
            _colorTranslationTable = new();
            foreach (ReplayCapture.Color replayColor in Enum.GetValues(typeof(ReplayCapture.Color)))
            {
                _colorTranslationTable[replayColor.ToString()] = replayColor;
            }
        }
        return _colorTranslationTable[colorName];
    }

    private static Dictionary<ReplayCapture.Color, System.Windows.Media.Color> _colorConversionTable;
    public static System.Windows.Media.Color ToColor(this ReplayCapture.Color color)
    {
        if (_colorConversionTable == null)
        {
            _colorConversionTable = new();
            foreach (ReplayCapture.Color replayColor in Enum.GetValues(typeof(ReplayCapture.Color)))
            {
                var c = (System.Windows.Media.Color)ColorConverter.ConvertFromString(replayColor.ToString());
                _colorConversionTable[replayColor] = c;
            }
        }
        return _colorConversionTable[color];
    }

    private static Dictionary<ReplayCapture.Color, SolidColorBrush> _brushConversionTable;
    public static SolidColorBrush ToBrush(this ReplayCapture.Color color)
    {
        if (_brushConversionTable == null)
        {
            _brushConversionTable = new();
            foreach (ReplayCapture.Color replayColor in Enum.GetValues(typeof(ReplayCapture.Color)))
            {
                var c = (System.Windows.Media.Color)ColorConverter.ConvertFromString(replayColor.ToString());
                _brushConversionTable[replayColor] = new SolidColorBrush(c);
            }
        }
        return _brushConversionTable[color];
    }
}
