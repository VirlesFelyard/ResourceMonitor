using System.Globalization;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using ResourceMonitor.Core;
namespace ResourceMonitor.Infrastructure.Platforms;

public sealed class LinuxProvider : PlatformBase
{
    [DllImport("libc")] private static extern long sysconf(int name);
    private readonly long ticks = sysconf(2);
    private readonly long pageSize = Environment.SystemPageSize;
    private readonly DateTimeOffset boot = DateTimeOffset.FromUnixTimeSeconds(long.Parse(File.ReadLines("/proc/stat").First(x => x.StartsWith("btime "))[6..]));
    public override SystemInfo GetSystemInfo()
    {
        var blocks = File.ReadAllText("/proc/cpuinfo").Split("\n\n", StringSplitOptions.RemoveEmptyEntries);
        var fields = blocks.Select(b => b.Split('\n').Where(l => l.Contains(':')).Select(l => l.Split(':', 2)).ToDictionary(x => x[0].Trim(), x => x[1].Trim())).ToArray();
        var first = fields.First();
        var physical = fields.Where(f => f.ContainsKey("core id") && f.ContainsKey("physical id")).Select(f => f["physical id"] + ":" + f["core id"]).Distinct().Count();
        return Info(first.GetValueOrDefault("model name", "Недоступно"), first.GetValueOrDefault("vendor_id", "Недоступно"), physical == 0 ? null : physical, fields.Length, double.TryParse(first.GetValueOrDefault("cpu MHz"), CultureInfo.InvariantCulture, out var mhz) ? mhz : null);
    }
    public static CpuCounter ParseCpu(string line)
    {
        var v = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Skip(1).Take(8).Select(ulong.Parse).ToArray();
        return new(v.Aggregate(0UL, (a, b) => a + b), v[3] + (v.Length > 4 ? v[4] : 0));
    }
    public override IReadOnlyList<CpuCounter> ReadCpu() => File.ReadLines("/proc/stat").TakeWhile(l => l.StartsWith("cpu")).Select(ParseCpu).ToArray();
    public static MemoryInfo ParseMemory(string text)
    {
        var m = text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToDictionary(a => a[0].TrimEnd(':'), a => ulong.Parse(a[1]) * 1024);
        var available = m.GetValueOrDefault("MemAvailable", m.GetValueOrDefault("MemFree") + m.GetValueOrDefault("Buffers") + m.GetValueOrDefault("Cached") + m.GetValueOrDefault("SReclaimable"));
        if (!m.ContainsKey("MemAvailable")) available -= Math.Min(available, m.GetValueOrDefault("Shmem"));
        return new(m["MemTotal"], Math.Min(m["MemTotal"], available), m.GetValueOrDefault("SwapTotal"), m.GetValueOrDefault("SwapTotal") - m.GetValueOrDefault("SwapFree"));
    }
    public override MemoryInfo ReadMemory() => ParseMemory(File.ReadAllText("/proc/meminfo"));
    public override CounterSet ReadDisk()
    {
        var result = new Dictionary<string, ByteCounter>();
        foreach (var line in File.ReadLines("/proc/diskstats"))
        {
            var a = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries); var name = a[2];
            var root = "/sys/block/" + name;
            if (!Directory.Exists(root) || name.StartsWith("loop") || name.StartsWith("ram") || name.StartsWith("zram") || Directory.EnumerateFileSystemEntries(root + "/slaves").Any()) continue;
            result[name] = new(ulong.Parse(a[5]) * 512, ulong.Parse(a[9]) * 512);
        }
        return new(result, result.Count > 0 ? MetricStatus.Available : MetricStatus.Unavailable);
    }
    protected override bool IncludeInterface(NetworkInterface n) => base.IncludeInterface(n) && Directory.Exists("/sys/class/net/" + n.Name + "/device");
    public static (string Name, string[] Fields) ParseProcessStat(string stat)
    {
        var first = stat.IndexOf('('); var last = stat.LastIndexOf(')');
        if (first < 0 || last <= first) throw new FormatException("Некоректний /proc stat");
        return (stat[(first + 1)..last], stat[(last + 2)..].Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
    public override IReadOnlyList<RawProcess> ReadProcesses()
    {
        var result = new List<RawProcess>();
        foreach (var directory in Directory.EnumerateDirectories("/proc"))
        {
            if (!int.TryParse(Path.GetFileName(directory), out var pid)) continue;
            try
            {
                var (name, a) = ParseProcessStat(File.ReadAllText(directory + "/stat"));
                var startTicks = ulong.Parse(a[19]);
                var read = Metric<ulong>.Missing(); var write = Metric<ulong>.Missing();
                try
                {
                    var io = File.ReadLines(directory + "/io").Select(l => l.Split(':', 2)).ToDictionary(x => x[0], x => ulong.Parse(x[1].Trim()));
                    read = Metric<ulong>.Ok(io["read_bytes"]); write = Metric<ulong>.Ok(io["write_bytes"]);
                }
                catch (UnauthorizedAccessException) { read = write = Metric<ulong>.Missing(MetricStatus.AccessDenied); }
                catch (IOException e) { read = write = Metric<ulong>.Missing(MetricStatus.Unavailable, e.Message); }
                result.Add(new(pid, name, $"{pid}:{startTicks}", boot.AddSeconds(startTicks / (double)ticks), TimeSpan.FromSeconds((ulong.Parse(a[11]) + ulong.Parse(a[12])) / (double)ticks), (ulong)Math.Max(0, long.Parse(a[21])) * (ulong)pageSize, read, write));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or FormatException or IndexOutOfRangeException) { Trace.WriteLine(e.Message); }
        }
        return result;
    }
    public override IReadOnlyList<VolumeInfo> ReadVolumes()
    {
        var volumes = new List<VolumeInfo>(); var seen = new HashSet<string>();
        foreach (var line in File.ReadLines("/proc/self/mountinfo"))
        {
            var a = line.Split(' '); var separator = Array.IndexOf(a, "-");
            if (separator < 0 || !a[separator + 2].StartsWith("/dev/") || !seen.Add(a[2])) continue;
            try
            {
                var mount = a[4].Replace("\\040", " ").Replace("\\011", "\t").Replace("\\134", "\\");
                var d = new DriveInfo(mount);
                if (d.IsReady && d.TotalSize > 0) volumes.Add(new(mount, a[separator + 1], (ulong)d.TotalSize, (ulong)d.AvailableFreeSpace));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Trace.WriteLine(e.Message); }
        }
        return volumes;
    }
    public override TimeSpan ReadUptime() => TimeSpan.FromSeconds(double.Parse(File.ReadAllText("/proc/uptime").Split(' ')[0], CultureInfo.InvariantCulture));
}
