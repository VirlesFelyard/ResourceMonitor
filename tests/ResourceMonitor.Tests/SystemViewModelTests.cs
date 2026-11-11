using Avalonia;
using Avalonia.Controls;
using ResourceMonitor.Core;
using ResourceMonitor.Desktop.Controls;
using ResourceMonitor.Desktop.ViewModels;
using Xunit;

namespace ResourceMonitor.Tests;

public class SystemViewModelTests
{
    private static MonitoringUpdate Update(MemoryInfo? memory, MetricStatus status = MetricStatus.Available)
    {
        var transfer = new TransferSample(Metric<double>.Missing(), Metric<double>.Missing(), 0, 0);
        var sample = new SystemSample(DateTimeOffset.UtcNow, Metric<double>.Missing(), [], memory, transfer, transfer);
        return new(sample, [], [], [new("/", "ext4", 1000, 400)], Metric<TimeSpan>.Ok(TimeSpan.Zero),
            [new("1", "wlan0", NetworkKind.WiFi, true)], status);
    }
    [Fact]
    public void MissingMetricsClearPreviousValuesAndHideProgress()
    {
        var vm = new SystemViewModel();
        vm.Update(Update(new(1000, 400, 0, 0)));
        Assert.True(vm.HasMemory);
        Assert.Equal(60, vm.MemoryUsage);
        vm.Update(Update(null, MetricStatus.AccessDenied) with
        {
            Uptime = Metric<TimeSpan>.Missing(), VolumesStatus = MetricStatus.AccessDenied, NetworkStatus = MetricStatus.Unsupported
        });
        Assert.False(vm.HasMemory);
        Assert.Equal("Недостатньо прав", vm.MemoryUsed);
        Assert.Equal("—", vm.MemoryPercent);
        Assert.Empty(vm.Volumes);
        Assert.Empty(vm.Networks);
        Assert.Equal("Недостатньо прав", vm.VolumesMessage);
        Assert.Equal("Не підтримується", vm.NetworkMessage);
        Assert.Equal("Тимчасово недоступно", vm.Uptime);
    }
    [Fact]
    public void UnknownSwapUsageIsNotDisplayedAsZero()
    {
        var vm = new SystemViewModel();
        vm.Update(Update(new(1000, 400, 2000, null)));
        Assert.Contains("недоступно", vm.Swap);
        vm.Update(Update(new(1000, 400, 0, 0)));
        Assert.Equal("Swap: не налаштовано", vm.Swap);
        vm.Update(Update(new(1000, 400, null, null)));
        Assert.Equal("Swap: недоступно", vm.Swap);
    }
    [Theory]
    [InlineData("Windows 11", "X64")]
    [InlineData("Darwin 25.0", "Arm64")]
    [InlineData("Manjaro Linux", "X64")]
    public void MissingStaticFieldsDoNotHideAvailableOperatingSystem(string os, string architecture)
    {
        var vm = new SystemViewModel();
        vm.SetInfo(new(os, architecture, "host", "", "", null, 8, double.NaN));
        Assert.Equal(os, vm.OsName);
        Assert.Equal("Недоступно", vm.CpuModel);
        Assert.Equal("Недоступно", vm.PhysicalCores);
        Assert.Equal("Недоступно", vm.Frequency);
        Assert.Equal("8", vm.LogicalCores);
    }
    [Fact]
    public void EmptyListsDifferFromProviderFailure()
    {
        var vm = new SystemViewModel();
        vm.Update(Update(new(1000, 500, 0, 0)) with { Volumes = [], NetworkInfo = [] });
        Assert.Equal("Немає доступних локальних томів", vm.VolumesMessage);
        Assert.Equal("Немає доступних мережевих інтерфейсів", vm.NetworkMessage);
        Assert.Equal("Стан недоступний", new NetworkRow(new("id", "adapter", NetworkKind.Other, null)).State);
    }
    [Fact]
    public void ResponsiveLayoutUsesOneColumnInNarrowWindows()
    {
        var panel = new ResponsiveColumns();
        panel.Children.Add(new Border { Height = 100 });
        panel.Children.Add(new Border { Height = 160 });
        panel.Measure(new Size(1000, double.PositiveInfinity));
        panel.Arrange(new Rect(0, 0, 1000, panel.DesiredSize.Height));
        Assert.Equal(506, panel.Children[1].Bounds.X);
        Assert.Equal(0, panel.Children[1].Bounds.Y);
        panel.Measure(new Size(580, double.PositiveInfinity));
        panel.Arrange(new Rect(0, 0, 580, panel.DesiredSize.Height));
        Assert.Equal(0, panel.Children[1].Bounds.X);
        Assert.Equal(112, panel.Children[1].Bounds.Y);
        Assert.Equal(580, panel.Children[1].Bounds.Width);
    }
}
