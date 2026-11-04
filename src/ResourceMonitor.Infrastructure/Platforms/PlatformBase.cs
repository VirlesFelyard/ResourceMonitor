using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using ResourceMonitor.Core;
namespace ResourceMonitor.Infrastructure.Platforms;

public abstract class PlatformBase : IPlatformProvider
{
    public abstract SystemInfo GetSystemInfo();
    public abstract IReadOnlyList<CpuCounter> ReadCpu();
    public abstract MemoryInfo ReadMemory();
    public virtual CounterSet ReadDisk() => new(new Dictionary<string, ByteCounter>(), MetricStatus.Unsupported, "Фізичний дисковий I/O недоступний у цій реалізації платформи");
    protected virtual bool IncludeInterface(NetworkInterface n) => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType is NetworkInterfaceType.Ethernet or NetworkInterfaceType.Wireless80211;
    public CounterSet ReadNetwork()
    {
        var counters = new Dictionary<string, ByteCounter>();
        foreach (var n in NetworkInterface.GetAllNetworkInterfaces().Where(IncludeInterface))
        {
            try { var s = n.GetIPStatistics(); counters[n.Id] = new((ulong)Math.Max(0, s.BytesReceived), (ulong)Math.Max(0, s.BytesSent)); }
            catch (NetworkInformationException e) { Trace.WriteLine(e.Message); }
        }
        return new(counters, counters.Count > 0 ? MetricStatus.Available : MetricStatus.Unavailable, counters.Count == 0 ? "Немає активного фізичного мережевого інтерфейсу" : null);
    }
    public IReadOnlyList<NetworkAdapterInfo> ReadNetworkInfo() => NetworkInterface.GetAllNetworkInterfaces()
        .Where(n => n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
        .Select(n => new NetworkAdapterInfo(n.Id, n.Name, GetNetworkKind(n),
            n.OperationalStatus == OperationalStatus.Unknown ? null : n.OperationalStatus == OperationalStatus.Up))
        .OrderBy(n => n.Kind is NetworkKind.WiFi or NetworkKind.Ethernet ? 0 : 1)
        .ThenByDescending(n => n.IsActive).ThenBy(n => n.Name).ToArray();
    private static NetworkKind GetNetworkKind(NetworkInterface adapter)
    {
        if (adapter.NetworkInterfaceType == NetworkInterfaceType.Wireless80211) return NetworkKind.WiFi;
        if (adapter.NetworkInterfaceType == NetworkInterfaceType.Tunnel) return NetworkKind.Tunnel;
        if (OperatingSystem.IsLinux())
        {
            var path = "/sys/class/net/" + adapter.Name;
            if (Directory.Exists(path + "/wireless") || Directory.Exists(path + "/phy80211")) return NetworkKind.WiFi;
            if (Directory.Exists(path + "/bridge")) return NetworkKind.Bridge;
            if (Directory.Exists("/sys/devices/virtual/net/" + adapter.Name)) return NetworkKind.Virtual;
        }
        return adapter.NetworkInterfaceType == NetworkInterfaceType.Ethernet ? NetworkKind.Ethernet : NetworkKind.Other;
    }

    public virtual IReadOnlyList<VolumeInfo> ReadVolumes()
    {
        var result = new List<VolumeInfo>();
        foreach (var d in DriveInfo.GetDrives())
        {
            try { if (d.IsReady && d.DriveType is DriveType.Fixed or DriveType.Removable && d.TotalSize > 0) result.Add(new(d.Name, d.DriveFormat, (ulong)d.TotalSize, (ulong)d.AvailableFreeSpace)); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Trace.WriteLine(e.Message); }
        }
        return result;
    }
    public virtual IReadOnlyList<RawProcess> ReadProcesses()
    {
        var result = new List<RawProcess>();
        foreach (var p in Process.GetProcesses())
        {
            using (p)
            {
                try
                {
                    var start = new DateTimeOffset(p.StartTime.ToUniversalTime());
                    var io = ReadProcessIo(p);
                    result.Add(new(p.Id, p.ProcessName, $"{p.Id}:{start.UtcTicks}", start, p.TotalProcessorTime, (ulong)Math.Max(0, p.WorkingSet64), io.Read, io.Write));
                }
                catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException) { Trace.WriteLine(e.Message); }
            }
        }
        return result;
    }
    protected virtual (Metric<ulong> Read, Metric<ulong> Write) ReadProcessIo(Process p) => (Metric<ulong>.Missing(MetricStatus.Unsupported), Metric<ulong>.Missing(MetricStatus.Unsupported));
    public virtual TimeSpan ReadUptime() => TimeSpan.FromMilliseconds(Environment.TickCount64);
    public virtual void Dispose() { GC.SuppressFinalize(this); }
    protected static SystemInfo Info(string model, string vendor, int? physical, int logical, double? mhz) => new(RuntimeInformation.OSDescription, RuntimeInformation.OSArchitecture.ToString(), Environment.MachineName, model, vendor, physical, logical, mhz);
    public static IPlatformProvider Create() => OperatingSystem.IsLinux() ? new LinuxProvider() : OperatingSystem.IsWindows() ? new WindowsProvider() : OperatingSystem.IsMacOS() ? new MacProvider() : throw new PlatformNotSupportedException("Операційна система не підтримується");
}
