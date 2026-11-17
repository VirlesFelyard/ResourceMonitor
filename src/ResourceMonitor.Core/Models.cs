namespace ResourceMonitor.Core;

public enum MetricStatus { Available, WarmingUp, Unavailable, Unsupported, AccessDenied }
public sealed record Metric<T>(T? Value, MetricStatus Status, string? Detail = null) where T : struct
{
    public static Metric<T> Ok(T value) => new(value, MetricStatus.Available);
    public static Metric<T> Missing(MetricStatus status = MetricStatus.Unavailable, string? detail = null) => new(null, status, detail);
}
public readonly record struct CpuCounter(ulong Total, ulong Idle);
public readonly record struct ByteCounter(ulong Read, ulong Write);
public sealed record MemoryInfo(ulong Total, ulong Available, ulong? SwapTotal, ulong? SwapUsed)
{
    public ulong Used => Total - Math.Min(Total, Available);
    public double Percent => Total == 0 ? 0 : Used * 100.0 / Total;
}
public sealed record VolumeInfo(string Name, string FileSystem, ulong Total, ulong Available)
{
    public ulong Used => Total - Math.Min(Total, Available);
    public double Percent => Total == 0 ? 0 : Used * 100.0 / Total;
}
public sealed record SystemInfo(string OperatingSystem, string Architecture, string Host, string CpuModel, string CpuVendor, int? PhysicalCores, int LogicalCores, double? FrequencyMhz);
public sealed record RawProcess(int Pid, string Name, string Identity, DateTimeOffset? Started, TimeSpan CpuTime, ulong ResidentBytes, Metric<ulong> Read, Metric<ulong> Write);
public sealed record ProcessSample(int Pid, string Name, string Identity, DateTimeOffset? Started, Metric<double> Cpu, ulong ResidentBytes, Metric<double> ReadRate, Metric<double> WriteRate);
public sealed record TransferSample(Metric<double> ReadRate, Metric<double> WriteRate, ulong ReadTotal, ulong WriteTotal);
public sealed record SystemSample(DateTimeOffset Time, Metric<double> Cpu, IReadOnlyList<Metric<double>> Cores, MemoryInfo? Memory, TransferSample Disk, TransferSample Network, string? Error = null);
public enum EventKind { Cpu, Ram }
public enum EventSeverity { Information, Warning, Critical }
public sealed record ProcessCandidate(int Pid, string Name, double Change, bool CurrentLeader);
public sealed record LoadEvent(Guid Id, DateTimeOffset Time, EventKind Kind, double Baseline, double Current, double Change, EventSeverity Severity, string Description, IReadOnlyList<ProcessCandidate> Candidates);
public enum NetworkKind { Ethernet, WiFi, Bridge, Virtual, Tunnel, Other }
public sealed record NetworkAdapterInfo(string Id, string Name, NetworkKind Kind, bool? IsActive);
public sealed record MonitoringUpdate(SystemSample Sample, IReadOnlyList<ProcessSample> Processes, IReadOnlyList<LoadEvent> Events, IReadOnlyList<VolumeInfo> Volumes, Metric<TimeSpan> Uptime, IReadOnlyList<NetworkAdapterInfo> NetworkInfo, MetricStatus MemoryStatus = MetricStatus.Available, MetricStatus VolumesStatus = MetricStatus.Available, MetricStatus NetworkStatus = MetricStatus.Available);
public sealed record CounterSet(IReadOnlyDictionary<string, ByteCounter> Counters, MetricStatus Status = MetricStatus.Available, string? Detail = null);
public interface IPlatformProvider : IDisposable
{
    SystemInfo GetSystemInfo();
    IReadOnlyList<CpuCounter> ReadCpu();
    MemoryInfo ReadMemory();
    CounterSet ReadDisk();
    CounterSet ReadNetwork();
    IReadOnlyList<RawProcess> ReadProcesses();
    IReadOnlyList<VolumeInfo> ReadVolumes();
    IReadOnlyList<NetworkAdapterInfo> ReadNetworkInfo();
    TimeSpan ReadUptime();
}
public interface IHistoryStore : IDisposable
{
    void Append(SystemSample sample, IReadOnlyList<LoadEvent> events);
    IReadOnlyList<SystemSample> Query(DateTimeOffset since);
    IReadOnlyList<LoadEvent> Events();
}
public interface ISettingsStore
{
    MonitorSettings Load();
    void Save(MonitorSettings settings);
}
public sealed record MonitorSettings
{
    public int IntervalSeconds { get; init; } = 1;
    public double CpuIncrease { get; init; } = 25;
    public double CpuMinimum { get; init; } = 50;
    public double RamIncreaseMiB { get; init; } = 512;
    public int CpuWindowSeconds { get; init; } = 5;
    public int RamWindowSeconds { get; init; } = 10;
    public int CooldownSeconds { get; init; } = 30;
    public bool DarkTheme { get; init; }
    public void Validate()
    {
        if (IntervalSeconds is < 1 or > 60 || !double.IsFinite(CpuIncrease) || CpuIncrease is <= 0 or > 100 || !double.IsFinite(CpuMinimum) || CpuMinimum is < 0 or > 100 || !double.IsFinite(RamIncreaseMiB) || RamIncreaseMiB is <= 0 or > 1048576 || CpuWindowSeconds is < 1 or > 3600 || RamWindowSeconds is < 1 or > 3600 || CooldownSeconds is < 0 or > 86400)
            throw new ArgumentException("Перевірте інтервали та пороги: CPU 0–100%, RAM 1–1048576 MiB, вікно 1–3600 с, блокування 0–86400 с.");
    }
}
