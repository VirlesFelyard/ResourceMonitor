namespace ResourceMonitor.Core;

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
