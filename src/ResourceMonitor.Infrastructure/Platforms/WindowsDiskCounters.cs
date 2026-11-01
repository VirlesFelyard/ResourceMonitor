using System.Runtime.InteropServices;
using ResourceMonitor.Core;
namespace ResourceMonitor.Infrastructure.Platforms;

internal sealed class WindowsDiskCounters : IDisposable
{
    [StructLayout(LayoutKind.Sequential)] private struct RawCounter { public uint Status, TimeLow, TimeHigh; public long First, Second; public uint MultiCount; }
    [DllImport("pdh", CharSet = CharSet.Unicode)] private static extern uint PdhOpenQuery(string? source, nuint user, out IntPtr query);
    [DllImport("pdh", CharSet = CharSet.Unicode)] private static extern uint PdhAddEnglishCounter(IntPtr query, string path, nuint user, out IntPtr counter);
    [DllImport("pdh")] private static extern uint PdhCollectQueryData(IntPtr query);
    [DllImport("pdh")] private static extern uint PdhGetRawCounterValue(IntPtr counter, out uint type, out RawCounter value);
    [DllImport("pdh")] private static extern uint PdhCloseQuery(IntPtr query);
    private IntPtr query, read, write;
    public CounterSet Read()
    {
        uint error = 0;
        if (query == IntPtr.Zero)
        {
            error = PdhOpenQuery(null, 0, out query);
            if (error == 0) error = PdhAddEnglishCounter(query, @"\PhysicalDisk(_Total)\Disk Read Bytes/sec", 0, out read);
            if (error == 0) error = PdhAddEnglishCounter(query, @"\PhysicalDisk(_Total)\Disk Write Bytes/sec", 0, out write);
            if (error != 0) { Dispose(); return Missing(error); }
        }
        error = PdhCollectQueryData(query); if (error != 0) return Missing(error);
        error = PdhGetRawCounterValue(read, out _, out var r); if (error != 0) return Missing(error);
        error = PdhGetRawCounterValue(write, out _, out var w); if (error != 0) return Missing(error);
        if (r.Status > 1 || w.Status > 1 || r.First < 0 || w.First < 0) return Missing(r.Status > 1 ? r.Status : w.Status);
        return new(new Dictionary<string, ByteCounter> { { "PhysicalDisk", new((ulong)r.First, (ulong)w.First) } });
    }
    private static CounterSet Missing(uint error) => new(new Dictionary<string, ByteCounter>(), error == 5 ? MetricStatus.AccessDenied : MetricStatus.Unavailable, $"Лічильники фізичних дисків PDH недоступні: {error:X}");
    public void Dispose() { if (query != IntPtr.Zero) { PdhCloseQuery(query); query = IntPtr.Zero; } }
}
