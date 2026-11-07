using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ResourceMonitor.Core;
using ResourceMonitor.Application;
namespace ResourceMonitor.Desktop.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsStore store;
    private readonly MonitorService monitor;
    public int[] Intervals { get; } = [1, 2, 5];
    [ObservableProperty] private int intervalSeconds = 1;
    [ObservableProperty] private decimal cpuIncrease = 25;
    [ObservableProperty] private decimal cpuMinimum = 50;
    [ObservableProperty] private decimal ramIncreaseMiB = 512;
    [ObservableProperty] private decimal cpuWindowSeconds = 5;
    [ObservableProperty] private decimal ramWindowSeconds = 10;
    [ObservableProperty] private decimal cooldownSeconds = 30;
    [ObservableProperty] private bool darkTheme;
    [ObservableProperty] private string message = "";
    public SettingsViewModel(ISettingsStore store, MonitorService monitor)
    {
        this.store = store; this.monitor = monitor; var value = store.Load(); Load(value); monitor.Configure(value);
    }
    partial void OnDarkThemeChanged(bool value) { if (Avalonia.Application.Current is { } app) app.RequestedThemeVariant = value ? ThemeVariant.Dark : ThemeVariant.Light; }
    private void Load(MonitorSettings value)
    {
        IntervalSeconds = value.IntervalSeconds; CpuIncrease = (decimal)value.CpuIncrease; CpuMinimum = (decimal)value.CpuMinimum; RamIncreaseMiB = (decimal)value.RamIncreaseMiB; CpuWindowSeconds = value.CpuWindowSeconds; RamWindowSeconds = value.RamWindowSeconds; CooldownSeconds = value.CooldownSeconds; DarkTheme = value.DarkTheme;
    }
    [RelayCommand]
    private void Apply()
    {
        try
        {
            var value = new MonitorSettings { IntervalSeconds = IntervalSeconds, CpuIncrease = (double)CpuIncrease, CpuMinimum = (double)CpuMinimum, RamIncreaseMiB = (double)RamIncreaseMiB, CpuWindowSeconds = (int)CpuWindowSeconds, RamWindowSeconds = (int)RamWindowSeconds, CooldownSeconds = (int)CooldownSeconds, DarkTheme = DarkTheme };
            value.Validate(); store.Save(value); monitor.Configure(value); Message = "Налаштування збережено й застосовано";
        }
        catch (Exception e) when (e is ArgumentException or IOException or UnauthorizedAccessException) { Message = e.Message; }
    }
    [RelayCommand] private void Reset() { Load(new()); Apply(); }
}
