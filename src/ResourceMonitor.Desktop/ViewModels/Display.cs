using ResourceMonitor.Core;
namespace ResourceMonitor.Desktop.ViewModels;

public static class Display
{
    public static string Bytes(double value)
    {
        string[] units = ["B", "KiB", "MiB", "GiB", "TiB"]; var index = 0;
        while (value >= 1024 && index < units.Length - 1) { value /= 1024; index++; }
        return $"{value:F1} {units[index]}";
    }
    public static string Status(MetricStatus status) => status switch { MetricStatus.WarmingUp => "Очікування вимірювання", MetricStatus.Unsupported => "Не підтримується", MetricStatus.AccessDenied => "Недостатньо прав", _ => "Тимчасово недоступно" };
    public static string Percent(Metric<double> metric) => metric.Value is { } v ? $"{v:F1}%" : Status(metric.Status);
    public static string Rate(Metric<double> metric) => metric.Value is { } v ? Bytes(v) + "/с" : Status(metric.Status);
}
public sealed record ProcessRow(ProcessSample Sample)
{
    public int Pid => Sample.Pid;
    public string Name => Sample.Name;
    public string Cpu => Display.Percent(Sample.Cpu);
    public string Ram => Display.Bytes(Sample.ResidentBytes);
    public string Read => Display.Rate(Sample.ReadRate);
    public string Write => Display.Rate(Sample.WriteRate);
    public string Started => Sample.Started?.LocalDateTime.ToString("dd.MM HH:mm:ss") ?? "Недоступно";
}
public sealed record EventRow(LoadEvent Event)
{
    public string Time => Event.Time.LocalDateTime.ToString("HH:mm:ss");
    public string Kind => Event.Kind == EventKind.Cpu ? "CPU" : "RAM";
    public string Severity => Event.Severity switch { EventSeverity.Critical => "Критична", EventSeverity.Warning => "Попередження", _ => "Інформація" };
    public string Description => Event.Description;
    public string Color => Event.Severity switch { EventSeverity.Critical => "#D85B62", EventSeverity.Warning => "#CC9636", _ => "#4779EC" };
}
