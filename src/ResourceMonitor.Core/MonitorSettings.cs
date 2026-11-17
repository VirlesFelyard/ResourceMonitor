namespace ResourceMonitor.Core;

public sealed record MonitorSettings
{
    public int IntervalSeconds { get; init; } = 1;
    public double CpuIncrease { get; init; } = 25;
    public double CpuMinimum { get; init; } = 50;
    public double RamIncreaseMiB { get; init; } = 512;
    public int CpuWindowSeconds { get; init; } = 5;
    public int RamWindowSeconds { get; init; } = 10;
    public int CooldownSeconds { get; init; } = 30;
    public bool DarkTheme { get; init; }
    public void Validate()
    {
        if (IntervalSeconds is < 1 or > 60 || !double.IsFinite(CpuIncrease) || CpuIncrease is <= 0 or > 100 || !double.IsFinite(CpuMinimum) || CpuMinimum is < 0 or > 100 || !double.IsFinite(RamIncreaseMiB) || RamIncreaseMiB is <= 0 or > 1048576 || CpuWindowSeconds is < 1 or > 3600 || RamWindowSeconds is < 1 or > 3600 || CooldownSeconds is < 0 or > 86400)
            throw new ArgumentException("Перевірте інтервали та пороги: CPU 0–100%, RAM 1–1048576 MiB, вікно 1–3600 с, блокування 0–86400 с.");
    }
}
