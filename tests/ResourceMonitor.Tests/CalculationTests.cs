using ResourceMonitor.Application;
using ResourceMonitor.Core;
using ResourceMonitor.Infrastructure.Platforms;
using ResourceMonitor.Infrastructure.Persistence;
using ResourceMonitor.Infrastructure.Configuration;
using Xunit;
namespace ResourceMonitor.Tests;

public class CalculationTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
    private static TransferSample Transfer() => new(Metric<double>.Ok(0), Metric<double>.Ok(0), 0, 0);
    private static SystemSample Sample(int second, double? cpu = 20, ulong used = 1024UL * 1024 * 1024) => new(Start.AddSeconds(second), cpu is { } value ? Metric<double>.Ok(value) : Metric<double>.Missing(), [], new(8UL * 1024 * 1024 * 1024, 8UL * 1024 * 1024 * 1024 - used, 0, 0), Transfer(), Transfer());
    private static ProcessSample Process(int id, double? cpu, ulong ram = 100) => new(id, $"p{id}", $"{id}:start", Start.AddMinutes(-1), cpu is { } value ? Metric<double>.Ok(value) : Metric<double>.Missing(), ram, Metric<double>.Missing(), Metric<double>.Missing());
    [Fact] public void CpuUsesDifferenceAndExcludesIdle() => Assert.Equal(75, CounterMath.Cpu(new(100, 50), new(200, 75)).Value);
    [Fact] public void FirstCpuSampleIsNotZero() => Assert.Equal(MetricStatus.WarmingUp, CounterMath.Cpu(null, new(100, 50)).Status);
    [Theory]
    [InlineData(100, 500, 2, 200)]
    [InlineData(1024, 4096, 3, 1024)]
    public void RateUsesElapsedSeconds(ulong before, ulong after, double seconds, double expected) => Assert.Equal(expected, CounterMath.Rate(before, after, seconds).Value);
    [Fact] public void ResetDoesNotProduceNegativeRate() => Assert.Null(CounterMath.Rate(1000, 10, 1).Value);
    [Fact] public void CpuResetIsUnavailable() => Assert.Null(CounterMath.Cpu(new(1000, 500), new(10, 5)).Value);
    [Fact]
    public void TransferIgnoresNewDevicesAndReset()
    {
        var tracker = new TransferTracker();
        CounterSet Set(params (string Id, ulong Read, ulong Write)[] values) => new(values.ToDictionary(v => v.Id, v => new ByteCounter(v.Read, v.Write)));
        tracker.Update(Set(("a", 100, 200)), 0);
        var result = tracker.Update(Set(("a", 300, 600), ("b", 9000000, 8000000)), 2);
        Assert.Equal(100, result.ReadRate.Value); Assert.Equal(200, result.WriteRate.Value); Assert.Equal(200UL, result.ReadTotal);
        result = tracker.Update(Set(("a", 10, 10)), 3); Assert.Null(result.ReadRate.Value); Assert.Equal(200UL, result.ReadTotal);
    }
    [Fact] public void MissingTransferIsDistinctFromZero() => Assert.Equal(MetricStatus.Unsupported, new TransferTracker().Update(new(new Dictionary<string, ByteCounter>(), MetricStatus.Unsupported), 0).ReadRate.Status);
    [Fact]
    public void ProcessCpuIsNormalizedAndPidReuseHasNoBaseline()
    {
        var tracker = new ProcessTracker(); RawProcess Raw(string identity, double cpu) => new(7, "p", identity, Start, TimeSpan.FromSeconds(cpu), 100, Metric<ulong>.Ok(0), Metric<ulong>.Ok(0));
        tracker.Update([Raw("7:a", 1)], 0, 4);
        Assert.Equal(25, tracker.Update([Raw("7:a", 3)], 2, 4)[0].Cpu.Value);
        Assert.Null(tracker.Update([Raw("7:b", 10)], 4, 4)[0].Cpu.Value);
    }
    [Fact]
    public void CpuSpikeRequiresConfirmation()
    {
        var analyzer = new LoadAnalyzer(); var settings = new MonitorSettings();
        analyzer.Analyze(Sample(0), [], settings); Assert.Empty(analyzer.Analyze(Sample(5, 80), [], settings));
        var e = Assert.Single(analyzer.Analyze(Sample(6, 85), [], settings)); Assert.Equal(EventKind.Cpu, e.Kind); Assert.Equal(65, e.Change);
    }
    [Fact]
    public void RamSpikeIsDetected()
    {
        var analyzer = new LoadAnalyzer(); var settings = new MonitorSettings(); analyzer.Analyze(Sample(0), [], settings);
        analyzer.Analyze(Sample(10, 20, 2UL * 1024 * 1024 * 1024), [], settings);
        Assert.Equal(EventKind.Ram, Assert.Single(analyzer.Analyze(Sample(11, 20, 2UL * 1024 * 1024 * 1024), [], settings)).Kind);
    }
    [Fact]
    public void BelowThresholdAndShortSpikesDoNotEmit()
    {
        var analyzer = new LoadAnalyzer(); var settings = new MonitorSettings(); analyzer.Analyze(Sample(0), [], settings);
        Assert.Empty(analyzer.Analyze(Sample(5, 40), [], settings)); Assert.Empty(analyzer.Analyze(Sample(6, 80), [], settings)); Assert.Empty(analyzer.Analyze(Sample(7, 20), [], settings));
    }
    [Fact]
    public void CooldownSuppressesRepeatedEvents()
    {
        var analyzer = new LoadAnalyzer(); var settings = new MonitorSettings(); analyzer.Analyze(Sample(0), [], settings); analyzer.Analyze(Sample(5, 80), [], settings); Assert.Single(analyzer.Analyze(Sample(6, 80), [], settings));
        analyzer.Analyze(Sample(7, 20), [], settings); Assert.Empty(analyzer.Analyze(Sample(12, 90), [], settings)); Assert.Empty(analyzer.Analyze(Sample(13, 90), [], settings));
    }
    [Fact]
    public void CandidatesRankGrowthRatherThanAbsoluteLoad()
    {
        var before = new[] { Process(1, 70), Process(2, 0), Process(3, 0), Process(4, 0) }; var after = new[] { Process(1, 71), Process(2, 10), Process(3, 20), Process(4, 15) };
        Assert.Equal(new[] { 3, 4, 2 }, LoadAnalyzer.RankCandidates(EventKind.Cpu, before, after, Start).Select(c => c.Pid));
    }
    [Fact]
    public void MissingCpuDoesNotEmitOrCreateCandidate()
    {
        var analyzer = new LoadAnalyzer(); var settings = new MonitorSettings(); analyzer.Analyze(Sample(0, null), [], settings); analyzer.Analyze(Sample(5, 90), [], settings); Assert.Empty(analyzer.Analyze(Sample(6, 90), [], settings));
        Assert.Empty(LoadAnalyzer.RankCandidates(EventKind.Cpu, [Process(1, null)], [Process(1, null)], Start));
    }
    [Fact]
    public void RamIncludesOnlyProvenNewProcesses()
    {
        var current = Process(1, 1, 1000) with { Started = Start.AddSeconds(1) };
        Assert.Single(LoadAnalyzer.RankCandidates(EventKind.Ram, [], [current], Start)); Assert.Empty(LoadAnalyzer.RankCandidates(EventKind.Ram, [], [current with { Started = null }], Start));
    }
    [Fact]
    public void LinuxStatHandlesSpacesAndParentheses()
    {
        var parsed = LinuxProvider.ParseProcessStat("123 (worker (io) thread) S 1 2 3"); Assert.Equal("worker (io) thread", parsed.Name); Assert.Equal("S", parsed.Fields[0]);
    }
    [Fact] public void LinuxCpuDoesNotCountGuestTwice() => Assert.Equal(new CpuCounter(100, 70), LinuxProvider.ParseCpu("cpu 10 0 20 60 10 0 0 0 5 0"));
    [Fact]
    public void LinuxMemoryUsesAvailableNotFree()
    {
        var m = LinuxProvider.ParseMemory("MemTotal: 1000 kB\nMemFree: 100 kB\nMemAvailable: 600 kB\nSwapTotal: 200 kB\nSwapFree: 150 kB\n"); Assert.Equal(400UL * 1024, m.Used); Assert.Equal(50UL * 1024, m.SwapUsed);
    }
    [Fact]
    public void SqliteRoundTripsEventsAndFullHistory()
    {
        using var store = new SqliteHistoryStore(); var e = new LoadEvent(Guid.NewGuid(), Start, EventKind.Cpu, 20, 90, 70, EventSeverity.Critical, "Стрибок", [new(1, "p", 70, false)]);
        for (var i = 0; i < 1000; i++) store.Append(Sample(i, i == 432 ? 100 : 20), i == 0 ? [e] : []);
        var samples = store.Query(Start); Assert.Equal(1000, samples.Count); Assert.Contains(samples, s => s.Cpu.Value == 100); Assert.Equal(e.Id, Assert.Single(store.Events()).Id);
        Assert.All(store.Query(Start.AddSeconds(990)), s => Assert.True(s.Time >= Start.AddSeconds(990)));
    }
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public void InvalidCpuThresholdIsRejected(double threshold) => Assert.Throws<ArgumentException>(() => (new MonitorSettings { CpuIncrease = threshold }).Validate());
    [Fact]
    public void SettingsPersistAndCorruptFileRecovers()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json"); var store = new JsonSettingsStore(path);
        try { store.Save(new() { IntervalSeconds = 5, DarkTheme = true }); Assert.Equal(5, store.Load().IntervalSeconds); File.WriteAllText(path, "bad json"); Assert.Equal(1, store.Load().IntervalSeconds); Assert.NotNull(store.LastError); }
        finally { File.Delete(path); }
    }
}
