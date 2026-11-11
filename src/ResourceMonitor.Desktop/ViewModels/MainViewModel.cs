using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using ResourceMonitor.Application;
using ResourceMonitor.Core;
namespace ResourceMonitor.Desktop.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IPlatformProvider platform;
    private readonly MonitorService monitor;
    public ChartsViewModel Charts { get; } = new();
    public ProcessesViewModel Processes { get; } = new();
    public SettingsViewModel Settings { get; }
    public ObservableCollection<EventRow> Events { get; } = [];
    public SystemViewModel System { get; } = new();
    [ObservableProperty] private string cpu = "Очікування вимірювання";
    [ObservableProperty] private string ram = "Очікування вимірювання";
    [ObservableProperty] private string memoryDetails = "";
    [ObservableProperty] private string disk = "Очікування вимірювання";
    [ObservableProperty] private string network = "Очікування вимірювання";
    [ObservableProperty] private string diskTotals = "";
    [ObservableProperty] private string networkTotals = "";
    [ObservableProperty] private string eventSummary = "Подій поки немає. Аналіз CPU та RAM активний.";
    public MainViewModel(IPlatformProvider platform, MonitorService monitor, SettingsViewModel settings)
    {
        this.platform = platform; this.monitor = monitor; Settings = settings;
        monitor.Updated += UpdateAsync; monitor.Failed += message => Dispatcher.UIThread.Post(() => EventSummary = message);
    }
    public async Task InitializeAsync()
    {
        try
        {
            var info = await Task.Run(platform.GetSystemInfo);
            System.SetInfo(info);
        }
        catch (Exception e) { System.SetInfoUnavailable(e.Message); }
        monitor.Start();
    }
    private async Task UpdateAsync(MonitoringUpdate update)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var s = update.Sample; Cpu = Display.Percent(s.Cpu);
            Ram = s.Memory is { } m ? $"{Display.Bytes(m.Used)} · {m.Percent:F1}%" : "Тимчасово недоступно";
            MemoryDetails = s.Memory is { } mem ? $"Всього: {Display.Bytes(mem.Total)}\nВикористано: {Display.Bytes(mem.Used)} · доступно: {Display.Bytes(mem.Available)}\nSwap: {(mem.SwapTotal is { } swap ? $"{Display.Bytes(mem.SwapUsed ?? 0)} з {Display.Bytes(swap)}" : "недоступно")}" : "Пам'ять тимчасово недоступна";
            Disk = $"↓ {Display.Rate(s.Disk.ReadRate)}    ↑ {Display.Rate(s.Disk.WriteRate)}";
            Network = $"↓ {Display.Rate(s.Network.ReadRate)}    ↑ {Display.Rate(s.Network.WriteRate)}";
            DiskTotals = $"За сеанс: прочитано {Display.Bytes(s.Disk.ReadTotal)} · записано {Display.Bytes(s.Disk.WriteTotal)}";
            NetworkTotals = $"За сеанс: отримано {Display.Bytes(s.Network.ReadTotal)} · передано {Display.Bytes(s.Network.WriteTotal)}";
            Charts.Update(s); Processes.Update(update.Processes); System.Update(update);
            foreach (var e in update.Events) Events.Insert(0, new(e));
            if (Events.Count > 0) EventSummary = $"Зафіксовано подій: {Events.Count}";

        });
    }
}
