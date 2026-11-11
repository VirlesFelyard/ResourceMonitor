using ResourceMonitor.Application;
using ResourceMonitor.Infrastructure.Platforms;
using ResourceMonitor.Infrastructure.Persistence;
namespace ResourceMonitor.Desktop;

internal static class SmokeTest
{
    public static async Task<int> RunAsync()
    {
        using var platform = PlatformBase.Create(); using var history = new SqliteHistoryStore(); await using var service = new MonitorService(platform, history, new LoadAnalyzer());
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously); var count = 0;
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(platform.GetSystemInfo()));
        service.Failed += e => completion.TrySetException(new Exception(e));
        service.Updated += update =>
        {
            Console.WriteLine($"CPU={update.Sample.Cpu.Value:F2}; RAM={update.Sample.Memory?.Used}; процесів={update.Processes.Count}; томів={update.Volumes.Count}; мережа={update.Sample.Network.ReadRate.Status}; диск={update.Sample.Disk.ReadRate.Status}");
            if (++count >= 4) completion.TrySetResult(update.Sample.Cpu.Value is >= 0 and <= 100 && update.Sample.Memory?.Used > 0 && update.Processes.Count > 0);
            return Task.CompletedTask;
        };
        service.Start();
        var valid = await completion.Task.WaitAsync(TimeSpan.FromSeconds(20));
        var saved = history.Query(DateTimeOffset.MinValue); Console.WriteLine($"SQLite: {saved.Count} вимірювань");
        return valid && saved.Count >= 4 ? 0 : 1;
    }
}
