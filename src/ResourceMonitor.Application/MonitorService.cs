using System.Diagnostics;
using ResourceMonitor.Core;
namespace ResourceMonitor.Application;

public sealed class MonitorService(IPlatformProvider platform, IHistoryStore history, LoadAnalyzer analyzer) : IAsyncDisposable
{
    private readonly CancellationTokenSource cancellation = new();
    private readonly TransferTracker disk = new(), network = new();
    private readonly ProcessTracker processTracker = new();
    private MonitorSettings settings = new();
    private Task? loop;
    private int resetAnalysis;
    public event Func<MonitoringUpdate, Task>? Updated;
    public event Action<string>? Failed;
    public void Configure(MonitorSettings value) { value.Validate(); Volatile.Write(ref settings, value); Interlocked.Exchange(ref resetAnalysis, 1); }
    public void Start() { loop ??= Task.Run(RunAsync); }
    private async Task RunAsync()
    {
        var clock = Stopwatch.StartNew(); IReadOnlyList<CpuCounter>? previous = null; IReadOnlyList<ProcessSample> processes = []; IReadOnlyList<VolumeInfo> volumes = []; IReadOnlyList<NetworkAdapterInfo> networkInfo = [];
        var volumesStatus = MetricStatus.WarmingUp; var networkStatus = MetricStatus.WarmingUp;
        double processTime = -100, volumeTime = -100; var logical = Environment.ProcessorCount;
        try { logical = platform.GetSystemInfo().LogicalCores; } catch (Exception e) { Failed?.Invoke(e.Message); }
        try
        {
            while (!cancellation.IsCancellationRequested)
            {
                var config = Volatile.Read(ref settings);
                if (Interlocked.Exchange(ref resetAnalysis, 0) == 1) analyzer.Reset();
                var errors = new List<string>();
                T Guard<T>(Func<T> read, T fallback, string name)
                {
                    try { return read(); } catch (Exception e) when (e is not OperationCanceledException) { errors.Add($"{name}: {e.Message}"); return fallback; }
                }
                (T Value, MetricStatus Status) ReadSection<T>(Func<T> read, T fallback, string name)
                {
                    try { return (read(), MetricStatus.Available); }
                    catch (Exception e) when (e is not OperationCanceledException)
                    {
                        errors.Add($"{name}: {e.Message}");
                        var status = e is UnauthorizedAccessException ? MetricStatus.AccessDenied
                            : e is NotSupportedException ? MetricStatus.Unsupported : MetricStatus.Unavailable;
                        return (fallback, status);
                    }
                }
                var cpu = Guard(platform.ReadCpu, Array.Empty<CpuCounter>() as IReadOnlyList<CpuCounter>, "CPU");
                var metrics = cpu.Select((c, i) => CounterMath.Cpu(previous is not null && previous.Count == cpu.Count ? previous[i] : null, c)).ToArray(); previous = cpu;
                var (memory, memoryStatus) = ReadSection<MemoryInfo?>(() => platform.ReadMemory(), null, "RAM");
                var now = clock.Elapsed.TotalSeconds;
                var missing = new CounterSet(new Dictionary<string, ByteCounter>(), MetricStatus.Unavailable);
                var disks = disk.Update(Guard(platform.ReadDisk, missing, "Диски"), clock.Elapsed.TotalSeconds);
                var net = network.Update(Guard(platform.ReadNetwork, missing, "Мережа"), clock.Elapsed.TotalSeconds);
                if (now - processTime >= 2)
                {
                    var raw = Guard(platform.ReadProcesses, Array.Empty<RawProcess>() as IReadOnlyList<RawProcess>, "Процеси");
                    processes = processTracker.Update(raw, clock.Elapsed.TotalSeconds, logical); processTime = now;
                }
                if (now - volumeTime >= 15)
                {
                    (volumes, volumesStatus) = ReadSection(platform.ReadVolumes, Array.Empty<VolumeInfo>() as IReadOnlyList<VolumeInfo>, "Томи");
                    (networkInfo, networkStatus) = ReadSection(platform.ReadNetworkInfo, Array.Empty<NetworkAdapterInfo>() as IReadOnlyList<NetworkAdapterInfo>, "Інтерфейси");
                    volumeTime = now;
                }
                var (uptime, uptimeStatus) = ReadSection<TimeSpan?>(() => platform.ReadUptime(), null, "Час роботи");
                var sample = new SystemSample(DateTimeOffset.UtcNow, metrics.FirstOrDefault() ?? Metric<double>.Missing(), metrics.Skip(1).ToArray(), memory, disks, net, errors.Count > 0 ? string.Join(" · ", errors) : null);
                var events = analyzer.Analyze(sample, processes, config);
                history.Append(sample, events);
                if (Updated is { } handler) await handler(new(sample, processes, events, volumes, new Metric<TimeSpan>(uptime, uptimeStatus), networkInfo, memoryStatus, volumesStatus, networkStatus));
                await Task.Delay(TimeSpan.FromSeconds(config.IntervalSeconds), cancellation.Token);
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception e) { Failed?.Invoke($"Моніторинг зупинено: {e.Message}"); }
    }
    public async ValueTask DisposeAsync()
    {
        await cancellation.CancelAsync();
        if (loop is not null) await loop;
        cancellation.Dispose();
    }
}
