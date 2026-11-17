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
