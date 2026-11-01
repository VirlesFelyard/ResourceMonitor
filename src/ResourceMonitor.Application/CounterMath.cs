using ResourceMonitor.Core;
namespace ResourceMonitor.Application;

public static class CounterMath
{
    public static Metric<double> Cpu(CpuCounter? previous, CpuCounter current)
    {
        if (previous is not { } p) return Metric<double>.Missing(MetricStatus.WarmingUp);
        if (current.Total <= p.Total || current.Idle < p.Idle) return Metric<double>.Missing(MetricStatus.WarmingUp);
        var total = current.Total - p.Total;
        var idle = current.Idle - p.Idle;
        return idle > total ? Metric<double>.Missing() : Metric<double>.Ok(100.0 * (total - idle) / total);
    }
    public static Metric<double> Rate(ulong? previous, ulong current, double seconds)
    {
        if (previous is null || current < previous || seconds <= 0) return Metric<double>.Missing(MetricStatus.WarmingUp);
        return Metric<double>.Ok((current - previous.Value) / seconds);
    }
}
public sealed class TransferTracker
{
    private IReadOnlyDictionary<string, ByteCounter> previous = new Dictionary<string, ByteCounter>();
    private double? previousTime;
    private ulong readTotal, writeTotal;
    public TransferSample Update(CounterSet set, double time)
    {
        if (set.Status != MetricStatus.Available)
        {
            previous = new Dictionary<string, ByteCounter>(); previousTime = null;
            return new(Metric<double>.Missing(set.Status, set.Detail), Metric<double>.Missing(set.Status, set.Detail), readTotal, writeTotal);
        }
        ulong read = 0, write = 0; var comparable = false;
        foreach (var (key, value) in set.Counters)
        {
            if (!previous.TryGetValue(key, out var old) || value.Read < old.Read || value.Write < old.Write) continue;
            read += value.Read - old.Read; write += value.Write - old.Write; comparable = true;
        }
        var seconds = previousTime is { } t ? time - t : 0;
        previous = set.Counters; previousTime = time;
        if (!comparable || seconds <= 0) return new(Metric<double>.Missing(MetricStatus.WarmingUp), Metric<double>.Missing(MetricStatus.WarmingUp), readTotal, writeTotal);
        readTotal += read; writeTotal += write;
        return new(Metric<double>.Ok(read / seconds), Metric<double>.Ok(write / seconds), readTotal, writeTotal);
    }
}
public sealed class ProcessTracker
{
    private Dictionary<string, RawProcess> previous = new();
    private double? previousTime;
    public IReadOnlyList<ProcessSample> Update(IReadOnlyList<RawProcess> processes, double time, int cores)
    {
        var seconds = previousTime is { } t ? time - t : 0;
        var result = processes.Select(p =>
        {
            previous.TryGetValue(p.Identity, out var old);
            var cpu = old is not null && seconds > 0 && p.CpuTime >= old.CpuTime
                ? Metric<double>.Ok(Math.Clamp((p.CpuTime - old.CpuTime).TotalSeconds / seconds / Math.Max(1, cores) * 100, 0, 100))
                : Metric<double>.Missing(MetricStatus.WarmingUp);
            Metric<double> Rate(Metric<ulong> now, Metric<ulong>? before) => now.Value is { } n ? CounterMath.Rate(before?.Value, n, seconds) : Metric<double>.Missing(now.Status);
            return new ProcessSample(p.Pid, p.Name, p.Identity, p.Started, cpu, p.ResidentBytes, Rate(p.Read, old?.Read), Rate(p.Write, old?.Write));
        }).ToArray();
        previous = processes.ToDictionary(p => p.Identity); previousTime = time;
        return result;
    }
}
