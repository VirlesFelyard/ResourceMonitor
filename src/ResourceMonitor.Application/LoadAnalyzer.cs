using ResourceMonitor.Core;
namespace ResourceMonitor.Application;

public sealed class LoadAnalyzer
{
    private sealed record Frame(SystemSample Sample, IReadOnlyList<ProcessSample> Processes);
    private readonly List<Frame> history = new();
    private readonly Dictionary<EventKind, DateTimeOffset> lastEvents = new();
    private readonly Dictionary<EventKind, Frame> pending = new();
    public IReadOnlyList<LoadEvent> Analyze(SystemSample sample, IReadOnlyList<ProcessSample> processes, MonitorSettings settings)
    {
        var events = new List<LoadEvent>();
        foreach (var kind in new[] { EventKind.Cpu, EventKind.Ram })
        {
            var window = kind == EventKind.Cpu ? settings.CpuWindowSeconds : settings.RamWindowSeconds;
            var boundary = sample.Time.AddSeconds(-window);
            var baseline = history.LastOrDefault(f => f.Sample.Time <= boundary);
            if (baseline is null || (boundary - baseline.Sample.Time).TotalSeconds > Math.Max(10, settings.IntervalSeconds * 2)) continue;
            if (pending.TryGetValue(kind, out var original)) baseline = original;
            var before = Value(baseline.Sample, kind); var current = Value(sample, kind);
            var threshold = kind == EventKind.Cpu ? settings.CpuIncrease : settings.RamIncreaseMiB * 1024 * 1024;
            if (before is null || current is null || current - before < threshold || kind == EventKind.Cpu && current < settings.CpuMinimum)
            { pending.Remove(kind); continue; }
            if (lastEvents.TryGetValue(kind, out var last) && (sample.Time - last).TotalSeconds < settings.CooldownSeconds) { pending.Remove(kind); continue; }
            if (!pending.ContainsKey(kind)) { pending[kind] = baseline; continue; }
            pending.Remove(kind); lastEvents[kind] = sample.Time;
            var candidates = RankCandidates(kind, baseline.Processes, processes, baseline.Sample.Time);
            var unit = kind == EventKind.Cpu ? "%" : "MiB"; var growthUnit = kind == EventKind.Cpu ? "в. п." : unit; var divisor = kind == EventKind.Cpu ? 1d : 1024 * 1024;
            var change = current.Value - before.Value;
            var description = $"{(kind == EventKind.Cpu ? "CPU" : "RAM")}: {before / divisor:F1} → {current / divisor:F1} {unit} (+{change / divisor:F1} {growthUnit}). ";
            description += candidates.Count == 0 ? "Недостатньо даних для визначення процесів-кандидатів." : string.Join("; ", candidates.Select(c => $"{c.Name} ({c.Pid}): {(c.CurrentLeader ? "поточний лідер " : "+")}{c.Change / divisor:F1} {(c.CurrentLeader ? unit : growthUnit)}")) + ".";
            var severity = (kind == EventKind.Cpu ? current >= 90 : sample.Memory?.Percent >= 90) ? EventSeverity.Critical : EventSeverity.Warning;
            events.Add(new(Guid.NewGuid(), sample.Time, kind, before.Value, current.Value, change, severity, description, candidates));
        }
        history.Add(new(sample, processes));
        var cutoff = sample.Time.AddSeconds(-Math.Max(settings.CpuWindowSeconds, settings.RamWindowSeconds) - Math.Max(10, settings.IntervalSeconds * 3));
        history.RemoveAll(f => f.Sample.Time < cutoff);
        return events;
    }
    public void Reset() { history.Clear(); pending.Clear(); }
    private static double? Value(SystemSample sample, EventKind kind) => kind == EventKind.Cpu ? sample.Cpu.Value : sample.Memory?.Used;
    public static IReadOnlyList<ProcessCandidate> RankCandidates(EventKind kind, IReadOnlyList<ProcessSample> before, IReadOnlyList<ProcessSample> current, DateTimeOffset baselineTime)
    {
        var previous = before.ToDictionary(p => p.Identity); var candidates = new List<ProcessCandidate>(); var comparable = false;
        foreach (var p in current)
        {
            double? delta = null;
            if (previous.TryGetValue(p.Identity, out var old))
            {
                delta = kind == EventKind.Cpu ? p.Cpu.Value - old.Cpu.Value : (double)p.ResidentBytes - old.ResidentBytes;
                comparable |= delta.HasValue;
            }
            else if (kind == EventKind.Ram && p.Started > baselineTime) { delta = p.ResidentBytes; comparable = true; }
            if (delta > 0) candidates.Add(new(p.Pid, p.Name, delta.Value, false));
        }
        if (!comparable && kind == EventKind.Cpu) candidates.AddRange(current.Where(p => p.Cpu.Value > 0).Select(p => new ProcessCandidate(p.Pid, p.Name, p.Cpu.Value!.Value, true)));
        return candidates.OrderByDescending(c => c.Change).Take(3).ToArray();
    }
}
