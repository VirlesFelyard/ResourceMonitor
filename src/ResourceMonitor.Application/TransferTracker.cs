using ResourceMonitor.Core;
namespace ResourceMonitor.Application;

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
