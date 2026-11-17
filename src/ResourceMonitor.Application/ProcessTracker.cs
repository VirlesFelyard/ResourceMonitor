using ResourceMonitor.Core;
namespace ResourceMonitor.Application;

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
