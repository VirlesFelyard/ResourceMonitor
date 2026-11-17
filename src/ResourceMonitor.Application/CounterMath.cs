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
