using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;
using ResourceMonitor.Core;
namespace ResourceMonitor.Infrastructure.Platforms;

[SupportedOSPlatform("windows")]
public sealed class WindowsProvider : PlatformBase
{
    private readonly WindowsDiskCounters disk = new();
    public override CounterSet ReadDisk() => disk.Read();
    public override void Dispose() { disk.Dispose(); base.Dispose(); }
    [StructLayout(LayoutKind.Sequential)] private struct MemoryStatus { public uint Length, Load; public ulong Total, Available, TotalPage, AvailablePage, TotalVirtual, AvailableVirtual, Extended; }
    [StructLayout(LayoutKind.Sequential)] private struct CpuTimes { public long Idle, Kernel, User, Dpc, Interrupt; public uint InterruptCount; }
    [StructLayout(LayoutKind.Sequential)] private struct IoCounters { public ulong ReadOperations, WriteOperations, OtherOperations, ReadBytes, WriteBytes, OtherBytes; }
    [DllImport("kernel32", SetLastError = true)] private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);
    [DllImport("ntdll")] private static extern int NtQuerySystemInformation(int type, [Out] CpuTimes[] data, int size, out int returned);
    [DllImport("kernel32", SetLastError = true)] private static extern bool GetLogicalProcessorInformationEx(int relation, IntPtr buffer, ref uint length);
    [DllImport("kernel32", SetLastError = true)] private static extern bool GetProcessIoCounters(IntPtr process, out IoCounters counters);
    [DllImport("kernel32")] private static extern uint GetActiveProcessorCount(ushort group);
    public override SystemInfo GetSystemInfo()
    {
        using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
        uint length = 0; GetLogicalProcessorInformationEx(0, IntPtr.Zero, ref length); int? cores = null;
        if (length > 0)
        {
            var buffer = Marshal.AllocHGlobal((int)length);
            try
            {
                if (GetLogicalProcessorInformationEx(0, buffer, ref length))
                {
                    var count = 0; var offset = 0;
                    while (offset + 8 <= length) { var size = Marshal.ReadInt32(buffer, offset + 4); if (size < 8) break; count++; offset += size; }
                    cores = count;
                }
            }
            finally { Marshal.FreeHGlobal(buffer); }
        }
        return Info(key?.GetValue("ProcessorNameString")?.ToString()?.Trim() ?? "Недоступно", key?.GetValue("VendorIdentifier")?.ToString() ?? "Недоступно", cores, (int)GetActiveProcessorCount(ushort.MaxValue), key?.GetValue("~MHz") is int mhz ? mhz : null);
    }
    public override IReadOnlyList<CpuCounter> ReadCpu()
    {
        var data = new CpuTimes[GetActiveProcessorCount(ushort.MaxValue)];
        var status = NtQuerySystemInformation(8, data, Marshal.SizeOf<CpuTimes>() * data.Length, out var length);
        if (status != 0) throw new IOException($"Не вдалося прочитати CPU: {status:X}");
        var cores = data.Take(length / Marshal.SizeOf<CpuTimes>()).Select(t => new CpuCounter((ulong)(t.Kernel + t.User), (ulong)t.Idle)).ToArray();
        return new[] { new CpuCounter(cores.Aggregate(0UL, (s, c) => s + c.Total), cores.Aggregate(0UL, (s, c) => s + c.Idle)) }.Concat(cores).ToArray();
    }
    public override MemoryInfo ReadMemory()
    {
        var s = new MemoryStatus { Length = (uint)Marshal.SizeOf<MemoryStatus>() };
        if (!GlobalMemoryStatusEx(ref s)) throw new System.ComponentModel.Win32Exception();
        return new(s.Total, s.Available, null, null);
    }
    protected override (Metric<ulong> Read, Metric<ulong> Write) ReadProcessIo(Process p)
    {
        try { return GetProcessIoCounters(p.Handle, out var io) ? (Metric<ulong>.Ok(io.ReadBytes), Metric<ulong>.Ok(io.WriteBytes)) : (Metric<ulong>.Missing(MetricStatus.AccessDenied), Metric<ulong>.Missing(MetricStatus.AccessDenied)); }
        catch (System.ComponentModel.Win32Exception) { return (Metric<ulong>.Missing(MetricStatus.AccessDenied), Metric<ulong>.Missing(MetricStatus.AccessDenied)); }
    }
}
