using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using ResourceMonitor.Core;
using ResourceMonitor.Desktop.ViewModels;
using Xunit;

namespace ResourceMonitor.Tests;

public class ChartWindowTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
    private static SystemSample Sample(double second)
    {
        var transfer = new TransferSample(Metric<double>.Ok(100), Metric<double>.Ok(50), 100, 50);
        return new(Start.AddSeconds(second), Metric<double>.Ok(25), [Metric<double>.Ok(30)], new(1000, 500, 0, 0), transfer, transfer);
    }
    private static ObservablePoint[] Points(ChartsViewModel charts) => ((LineSeries<ObservablePoint>)charts.CpuSeries[0]).Values!.ToArray();

    [Fact]
    public void IrregularSamplesMoveLeftByActualElapsedTime()
    {
        var charts = new ChartsViewModel();
        charts.Update(Sample(0));
        charts.Update(Sample(2.5));
        charts.Update(Sample(7));
        Assert.Equal(new double?[] { -7, -4.5, 0 }, Points(charts).Select(p => p.X));
        Assert.Equal(-60, charts.CpuX[0].MinLimit);
        Assert.Equal(0, charts.CpuX[0].MaxLimit);
        Assert.Equal("1 хв", charts.CpuX[0].Labeler(-60));
        Assert.Equal("10 с", charts.CpuX[0].Labeler(-10));
        Assert.Equal("0 с", charts.CpuX[0].Labeler(0));
    }

    [Fact]
    public void LongSessionKeepsOnlyMinuteAndBoundarySegment()
    {
        var charts = new ChartsViewModel();
        for (var i = 0; i <= 1000; i++) charts.Update(Sample(i * 2.3));
        var points = Points(charts);
        Assert.InRange(points.Length, 27, 28);
        Assert.True(points[0].X <= -60);
        Assert.True(points[1].X > -60);
        Assert.Equal(0, points[^1].X);
        Assert.All(new[] { charts.CpuX, charts.RamX, charts.DiskX, charts.NetworkX }, axes =>
        {
            Assert.Equal(-60, axes[0].MinLimit);
            Assert.Equal(0, axes[0].MaxLimit);
        });
    }
}
