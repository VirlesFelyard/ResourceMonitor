using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.Drawing;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using ResourceMonitor.Core;
namespace ResourceMonitor.Desktop.ViewModels;

public partial class CoreLineViewModel : ObservableObject
{
    public string Name { get; }
    public string Color { get; }
    private readonly LineSeries<ObservablePoint> series;
    [ObservableProperty] private bool visible;
    public CoreLineViewModel(string name, string color, LineSeries<ObservablePoint> series) { Name = name; Color = color; this.series = series; }
    partial void OnVisibleChanged(bool value) { series.IsVisible = value; }
}
public sealed class ChartsViewModel
{
    public ObservableCollection<ISeries> CpuSeries { get; } = [];
    public ISeries[] RamSeries { get; }
    public ISeries[] DiskSeries { get; }
    public ISeries[] NetworkSeries { get; }
    public ObservableCollection<CoreLineViewModel> CoreLines { get; } = [];
    public Axis[] CpuY { get; } = [new() { MinLimit = 0, MaxLimit = 100, Labeler = v => $"{v:0}%", TextSize = 11 }];
    public Axis[] RamY { get; } = [new() { MinLimit = 0, Labeler = v => $"{v:F1} GiB", TextSize = 11 }];
    public Axis[] DiskY { get; } = [new() { MinLimit = 0, Labeler = v => Display.Bytes(Math.Max(0, v)) + "/с", TextSize = 11 }];
    public Axis[] NetworkY { get; } = [new() { MinLimit = 0, Labeler = v => Display.Bytes(Math.Max(0, v)) + "/с", TextSize = 11 }];
    public Axis[] CpuX { get; } = TimeAxis();
    public Axis[] RamX { get; } = TimeAxis();
    public Axis[] DiskX { get; } = TimeAxis();
    public Axis[] NetworkX { get; } = TimeAxis();
    private readonly List<SystemSample> samples = [];
    public Margin PlotMargin { get; } = new(64, 8, 12, 26);
    private static Axis[] TimeAxis() =>
    [
        new()
        {
            MinLimit = -60,
            MaxLimit = 0,
            CustomSeparators = [-60, -50, -40, -30, -20, -10, 0],
            Labeler = seconds => seconds == -60 ? "1 хв" : seconds == 0 ? "0 с" : $"{-seconds:0} с",
            TextSize = 11,
            Padding = new Padding(0, 8, 0, 0),
            MinStep = 10,
            ForceStepToMin = true,
            SeparatorsPaint = new SolidColorPaint(SKColors.Gray.WithAlpha(35), 1),
            UnitWidth = 0
        }
    ];
    private static LineSeries<ObservablePoint> Line(string name, string color, bool visible = true) => new() { DataPadding = new LvcPoint(0, 0), Name = name, Values = new ObservableCollection<ObservablePoint>(), Stroke = new SolidColorPaint(SKColor.Parse(color), 2), Fill = null, GeometrySize = 0, LineSmoothness = 0, IsVisible = visible };
    public ChartsViewModel()
    {
        CpuSeries.Add(Line("Загальне", "#4779EC"));
        RamSeries = [Line("Використано", "#8D68D9")]; DiskSeries = [Line("Читання", "#429C86"), Line("Запис", "#DB9A42")]; NetworkSeries = [Line("Отримано", "#4779EC"), Line("Передано", "#C671A1")];
    }
    public void Update(SystemSample latest)
    {
        samples.Add(latest);
        var cutoff = latest.Time.AddSeconds(-60);
        while (samples.Count > 1 && samples[1].Time <= cutoff) samples.RemoveAt(0);
        var coreCount = samples.LastOrDefault()?.Cores.Count ?? 0;
        while (CpuSeries.Count < coreCount + 1)
        {
            var i = CpuSeries.Count - 1; var color = SKColor.FromHsv(i * 137.508f % 360, 60, 80); var series = Line($"CPU {i}", color.ToString(), false);
            CpuSeries.Add(series); CoreLines.Add(new($"CPU {i}", color.ToString(), series));
        }
        void Set(ISeries series, Func<SystemSample, double?> value)
        {
            var points = (ObservableCollection<ObservablePoint>)((LineSeries<ObservablePoint>)series).Values!;
            var i = 0;
            foreach (var sample in samples)
            {
                var y = value(sample); var x = (sample.Time - latest.Time).TotalSeconds;
                if (i < points.Count) { points[i].X = x; points[i].Y = y; } else points.Add(new(x, y)); i++;
            }
            while (points.Count > i) points.RemoveAt(points.Count - 1);
        }
        Set(CpuSeries[0], s => s.Cpu.Value);
        for (var i = 1; i < CpuSeries.Count; i++) { var index = i - 1; Set(CpuSeries[i], s => index < s.Cores.Count ? s.Cores[index].Value : null); }
        Set(RamSeries[0], s => s.Memory?.Used / (1024d * 1024 * 1024));
        Set(DiskSeries[0], s => s.Disk.ReadRate.Value); Set(DiskSeries[1], s => s.Disk.WriteRate.Value);
        Set(NetworkSeries[0], s => s.Network.ReadRate.Value); Set(NetworkSeries[1], s => s.Network.WriteRate.Value);
    }
}
