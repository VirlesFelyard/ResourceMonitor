using Avalonia;
using Avalonia.Controls;

namespace ResourceMonitor.Desktop.Controls;

public sealed class ResponsiveColumns : Panel
{
    public static readonly StyledProperty<double> MinimumColumnWidthProperty =
        AvaloniaProperty.Register<ResponsiveColumns, double>(nameof(MinimumColumnWidth), 350);
    public double MinimumColumnWidth { get => GetValue(MinimumColumnWidthProperty); set => SetValue(MinimumColumnWidthProperty, value); }
    static ResponsiveColumns() => AffectsMeasure<ResponsiveColumns>(MinimumColumnWidthProperty);
    private const double Gap = 12;
    private int ColumnCount(double width) => double.IsFinite(width) && width >= MinimumColumnWidth * 2 + Gap ? 2 : 1;
    protected override Size MeasureOverride(Size availableSize)
    {
        var columns = ColumnCount(availableSize.Width);
        var width = double.IsFinite(availableSize.Width) ? (availableSize.Width - Gap * (columns - 1)) / columns : MinimumColumnWidth;
        double height = 0;
        for (var i = 0; i < Children.Count; i += columns)
        {
            double rowHeight = 0;
            for (var j = i; j < Math.Min(i + columns, Children.Count); j++)
            {
                Children[j].Measure(new Size(Math.Max(0, width), double.PositiveInfinity));
                rowHeight = Math.Max(rowHeight, Children[j].DesiredSize.Height);
            }
            if (i > 0) height += Gap;
            height += rowHeight;
        }
        return new Size(width * columns + Gap * (columns - 1), height);
    }
    protected override Size ArrangeOverride(Size finalSize)
    {
        var columns = ColumnCount(finalSize.Width);
        var width = Math.Max(0, (finalSize.Width - Gap * (columns - 1)) / columns);
        double y = 0;
        for (var i = 0; i < Children.Count; i += columns)
        {
            var rowHeight = Children.Skip(i).Take(columns).Max(child => child.DesiredSize.Height);
            for (var j = i; j < Math.Min(i + columns, Children.Count); j++)
                Children[j].Arrange(new Rect((j - i) * (width + Gap), y, width, rowHeight));
            y += rowHeight + Gap;
        }
        return finalSize;
    }
}
