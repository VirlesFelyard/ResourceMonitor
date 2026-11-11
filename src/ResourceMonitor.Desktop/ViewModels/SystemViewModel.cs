using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ResourceMonitor.Core;

namespace ResourceMonitor.Desktop.ViewModels;

public partial class SystemViewModel : ObservableObject
{
    private const string Pending = "Завантаження…";
    private const string Missing = "Недоступно";
    [ObservableProperty] private string osName = Pending;
    [ObservableProperty] private string machine = Pending;
    [ObservableProperty] private string cpuModel = Pending;
    [ObservableProperty] private string cpuVendor = Pending;
    [ObservableProperty] private string physicalCores = "—";
    [ObservableProperty] private string logicalCores = "—";
    [ObservableProperty] private string frequency = "—";
    [ObservableProperty] private string uptime = Pending;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasStaticNotice))] private string staticNotice = "";
    [ObservableProperty] private string memoryUsed = Pending;
    [ObservableProperty] private string memoryTotal = "";
    [ObservableProperty] private string memoryAvailable = "";
    [ObservableProperty] private string memoryPercent = "—";
    [ObservableProperty] private double memoryUsage;
    [ObservableProperty] private bool hasMemory;
    [ObservableProperty] private string swap = Pending;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasVolumesMessage))] private string volumesMessage = Pending;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasNetworkMessage))] private string networkMessage = Pending;
    public bool HasStaticNotice => StaticNotice.Length > 0;
    public bool HasVolumesMessage => VolumesMessage.Length > 0;
    public bool HasNetworkMessage => NetworkMessage.Length > 0;
    public ObservableCollection<SystemVolumeRow> Volumes { get; } = [];
    public ObservableCollection<NetworkRow> Networks { get; } = [];

    private static string Text(string? value) => string.IsNullOrWhiteSpace(value) ? Missing : value.Trim();
    public void SetInfo(SystemInfo info)
    {
        OsName = Text(info.OperatingSystem);
        Machine = $"{Text(info.Architecture).ToLowerInvariant()} · {Text(info.Host)}";
        CpuModel = Text(info.CpuModel);
        CpuVendor = Text(info.CpuVendor);
        PhysicalCores = info.PhysicalCores is > 0 ? info.PhysicalCores.ToString()! : Missing;
        LogicalCores = info.LogicalCores > 0 ? info.LogicalCores.ToString() : Missing;
        Frequency = info.FrequencyMhz is { } mhz && double.IsFinite(mhz) && mhz > 0 ? $"{mhz / 1000:F2} GHz" : Missing;
        StaticNotice = "";
    }
    public void SetInfoUnavailable(string detail)
    {
        OsName = Machine = CpuModel = CpuVendor = PhysicalCores = LogicalCores = Frequency = Missing;
        StaticNotice = "Статичні відомості недоступні. Поточні показники оновлюються окремо.";
        System.Diagnostics.Trace.WriteLine(detail);
    }
    public void Update(MonitoringUpdate update)
    {
        Uptime = update.Uptime.Value is { } elapsed && elapsed >= TimeSpan.Zero
            ? elapsed.TotalDays >= 1 ? $"{(int)elapsed.TotalDays} д {elapsed.Hours} год {elapsed.Minutes} хв"
            : elapsed.TotalHours >= 1 ? $"{(int)elapsed.TotalHours} год {elapsed.Minutes} хв" : $"{elapsed.Minutes} хв {elapsed.Seconds} с"
            : Display.Status(update.Uptime.Status);
        HasMemory = update.MemoryStatus == MetricStatus.Available && update.Sample.Memory is { Total: > 0 };
        if (HasMemory && update.Sample.Memory is { } memory)
        {
            MemoryUsed = Display.Bytes(memory.Used);
            MemoryTotal = $"із {Display.Bytes(memory.Total)}";
            MemoryAvailable = $"Доступно: {Display.Bytes(memory.Available)}";
            MemoryUsage = memory.Percent;
            MemoryPercent = $"{memory.Percent:F0}%";
            Swap = memory.SwapTotal switch
            {
                null => "Swap: недоступно",
                0 => "Swap: не налаштовано",
                var total => $"Swap: {(memory.SwapUsed is { } used ? Display.Bytes(used) : "недоступно")} / {Display.Bytes(total.Value)}"
            };
        }
        else
        {
            MemoryUsed = Display.Status(update.MemoryStatus);
            MemoryTotal = MemoryAvailable = "";
            MemoryPercent = "—";
            MemoryUsage = 0;
            Swap = "Swap: недоступно";
        }
        var volumes = update.VolumesStatus == MetricStatus.Available ? update.Volumes.Select(v => new SystemVolumeRow(v)).ToArray() : [];
        Replace(Volumes, volumes);
        VolumesMessage = update.VolumesStatus != MetricStatus.Available ? Display.Status(update.VolumesStatus)
            : volumes.Length == 0 ? "Немає доступних локальних томів" : "";
        var networks = update.NetworkStatus == MetricStatus.Available ? update.NetworkInfo.Select(n => new NetworkRow(n)).ToArray() : [];
        Replace(Networks, networks);
        NetworkMessage = update.NetworkStatus != MetricStatus.Available ? Display.Status(update.NetworkStatus)
            : networks.Length == 0 ? "Немає доступних мережевих інтерфейсів" : "";
    }
    private static void Replace<T>(ObservableCollection<T> target, IReadOnlyList<T> values)
    {
        for (var i = 0; i < values.Count; i++)
        {
            if (i >= target.Count) target.Add(values[i]);
            else if (!Equals(target[i], values[i])) target[i] = values[i];
        }
        while (target.Count > values.Count) target.RemoveAt(target.Count - 1);
    }
}

public sealed record SystemVolumeRow(VolumeInfo Volume)
{
    public string Name => $"{Volume.Name} · {(string.IsNullOrWhiteSpace(Volume.FileSystem) ? "ФС недоступна" : Volume.FileSystem)}";
    public bool HasCapacity => Volume.Total > 0;
    public double Percent => Volume.Percent;
    public string PercentText => HasCapacity ? $"{Percent:F0}%" : "—";
    public string Capacity => HasCapacity ? $"{Display.Bytes(Volume.Used)} / {Display.Bytes(Volume.Total)} · вільно {Display.Bytes(Volume.Available)}" : "Місткість недоступна";
}
public sealed record NetworkRow(NetworkAdapterInfo Adapter)
{
    public string Name => string.IsNullOrWhiteSpace(Adapter.Name) ? "Назва недоступна" : Adapter.Name;
    public string Type => Adapter.Kind switch
    {
        NetworkKind.WiFi => "Wi-Fi", NetworkKind.Ethernet => "Ethernet", NetworkKind.Bridge => "Мережевий міст",
        NetworkKind.Virtual => "Віртуальний інтерфейс", NetworkKind.Tunnel => "Тунель", _ => "Інший інтерфейс"
    };
    public string State => Adapter.IsActive switch { true => "Активний", false => "Неактивний", null => "Стан недоступний" };
    public string Details => $"{Type} · {State}";
    public string Indicator => Adapter.IsActive switch { true => "●", false => "○", null => "?" };
    public string Color => Adapter.IsActive switch { true => "#429C86", false => "#999999", null => "#CC9636" };
}
