using System.Runtime.InteropServices;
using System.Text;
using ResourceMonitor.Core;
namespace ResourceMonitor.Infrastructure.Platforms;

public sealed class MacProvider : PlatformBase
{
    private const string Lib = "/usr/lib/libSystem.B.dylib";
    [DllImport(Lib)] private static extern int sysctlbyname(string name, byte[]? value, ref nuint size, IntPtr newValue, nuint newSize);
    [DllImport(Lib)] private static extern uint mach_host_self();
    [DllImport(Lib)] private static extern uint mach_task_self();
    [DllImport(Lib)] private static extern int host_processor_info(uint host, int flavor, out uint count, out IntPtr info, out uint infoCount);
    [DllImport(Lib)] private static extern int host_statistics64(uint host, int flavor, IntPtr info, ref uint count);
    [DllImport(Lib)] private static extern int host_page_size(uint host, out uint size);
    [DllImport(Lib)] private static extern int vm_deallocate(uint task, nuint address, nuint size);
    [DllImport(Lib)] private static extern int mach_port_deallocate(uint task, uint port);
    private static byte[]? Sysctl(string name)
    {
        nuint size = 0;
        if (sysctlbyname(name, null, ref size, IntPtr.Zero, 0) != 0) return null;
        var buffer = new byte[(int)size];
        return sysctlbyname(name, buffer, ref size, IntPtr.Zero, 0) == 0 ? buffer : null;
    }
    private static ulong? Number(string name) => Sysctl(name) is { } b ? b.Length >= 8 ? BitConverter.ToUInt64(b) : b.Length >= 4 ? BitConverter.ToUInt32(b) : null : null;
    public override SystemInfo GetSystemInfo() => Info(Sysctl("machdep.cpu.brand_string") is { } b ? Encoding.UTF8.GetString(b).TrimEnd('\0') : "Недоступно", RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "Apple" : Sysctl("machdep.cpu.vendor") is { } vendor ? Encoding.UTF8.GetString(vendor).TrimEnd('\0') : "Недоступно", (int?)Number("hw.physicalcpu"), (int)(Number("hw.logicalcpu") ?? (ulong)Environment.ProcessorCount), Number("hw.cpufrequency") is { } hz ? hz / 1e6 : null);
    public override IReadOnlyList<CpuCounter> ReadCpu()
    {
        var host = mach_host_self(); IntPtr info = IntPtr.Zero; uint size = 0;
        try
        {
            if (host_processor_info(host, 2, out var count, out info, out size) != 0) throw new IOException("Не вдалося прочитати CPU Mach");
            var cores = new List<CpuCounter>();
            for (var i = 0; i < count; i++)
            {
                var user = (uint)Marshal.ReadInt32(info, (int)i * 16); var system = (uint)Marshal.ReadInt32(info, (int)i * 16 + 4); var idle = (uint)Marshal.ReadInt32(info, (int)i * 16 + 8); var nice = (uint)Marshal.ReadInt32(info, (int)i * 16 + 12);
                cores.Add(new((ulong)user + system + idle + nice, idle));
            }
            return new[] { new CpuCounter(cores.Aggregate(0UL, (s, c) => s + c.Total), cores.Aggregate(0UL, (s, c) => s + c.Idle)) }.Concat(cores).ToArray();
        }
        finally { if (info != IntPtr.Zero) vm_deallocate(mach_task_self(), (nuint)info, size * 4); mach_port_deallocate(mach_task_self(), host); }
    }
    public override MemoryInfo ReadMemory()
    {
        var total = Number("hw.memsize") ?? throw new IOException("Недоступний hw.memsize");
        var host = mach_host_self(); var info = Marshal.AllocHGlobal(256);
        try
        {
            uint count = 64;
            if (host_statistics64(host, 4, info, ref count) != 0 || count < 38 || host_page_size(host, out var page) != 0) throw new IOException("Недоступна статистика пам'яті Mach");
            var wired = (uint)Marshal.ReadInt32(info, 12); var purgeable = (uint)Marshal.ReadInt32(info, 88);
            var compressor = (uint)Marshal.ReadInt32(info, 128); var anonymous = (uint)Marshal.ReadInt32(info, 140);
            var used = Math.Min(total, ((ulong)anonymous - Math.Min(anonymous, purgeable) + wired + compressor) * page);
            var available = total - used;
            var swap = Sysctl("vm.swapusage");
            return new(total, available, swap is { Length: >= 24 } ? BitConverter.ToUInt64(swap, 0) : null, swap is { Length: >= 24 } ? BitConverter.ToUInt64(swap, 16) : null);
        }
        finally { Marshal.FreeHGlobal(info); mach_port_deallocate(mach_task_self(), host); }
    }
    public override TimeSpan ReadUptime()
    {
        var boot = Sysctl("kern.boottime");
        return boot is { Length: >= 8 } ? DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeSeconds(BitConverter.ToInt64(boot)) : base.ReadUptime();
    }
}
