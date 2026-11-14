using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using ResourceMonitor.Desktop.ViewModels;
namespace ResourceMonitor.Desktop;

internal static class UiSmokeTest
{
    public static async Task RunAsync(Window window, MainViewModel vm)
    {
        var output = Path.Combine(Path.GetTempPath(), "resourcemonitor-ui"); Directory.CreateDirectory(output);
        try
        {
            await Task.Delay(65000);
            var points = ((LineSeries<ObservablePoint>)vm.Charts.CpuSeries[0]).Values!.ToArray();
            if (points.Length < 2 || points[0].X > -60 || points[1].X <= -60 || points[^1].X != 0)
                throw new InvalidOperationException("Хвилинне вікно графіка не зберігає межі");
            var tabs = window.GetVisualDescendants().OfType<TabControl>().Single();
            if (tabs.ItemCount != 5 || vm.Processes.Rows.Count == 0 || vm.Cpu.Contains("Очікування")) throw new InvalidOperationException("Не завантажено інтерфейс або реальні метрики");
            for (var i = 0; i < 5; i++)
            {
                tabs.SelectedIndex = i; await Task.Delay(500);
                using var bitmap = new RenderTargetBitmap(new PixelSize((int)window.Bounds.Width, (int)window.Bounds.Height), new Vector(96, 96)); bitmap.Render(window); bitmap.Save(Path.Combine(output, $"tab-{i}.png"));
            }
            tabs.SelectedIndex = 2; vm.Processes.Search = "dotnet"; vm.Processes.SortIndex = 2; vm.Processes.Descending = false; await Task.Delay(2500);
            if (vm.Processes.Rows.Any(p => !p.Name.Contains("dotnet", StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("Пошук процесів не застосовано");
            vm.Processes.Search = ""; vm.Settings.DarkTheme = true; tabs.SelectedIndex = 0; window.Width = 700; await Task.Delay(1500);
            using (var bitmap = new RenderTargetBitmap(new PixelSize((int)window.Bounds.Width, (int)window.Bounds.Height), new Vector(96, 96))) { bitmap.Render(window); bitmap.Save(Path.Combine(output, "dark-narrow.png")); }
            tabs.SelectedIndex = 1;
            window.Width = 650;
            await Task.Delay(500);
            using (var bitmap = new RenderTargetBitmap(new PixelSize((int)window.Bounds.Width, (int)window.Bounds.Height), new Vector(96, 96))) { bitmap.Render(window); bitmap.Save(Path.Combine(output, "system-narrow.png")); }
            window.Width = 1000;
            await Task.Delay(500);
            using (var bitmap = new RenderTargetBitmap(new PixelSize((int)window.Bounds.Width, (int)window.Bounds.Height), new Vector(96, 96))) { bitmap.Render(window); bitmap.Save(Path.Combine(output, "system-dark.png")); }
            Console.WriteLine($"UI перевірено: 5 вкладок, реальні метрики, пошук, сортування, темна тема, вузьке вікно. Знімки: {output}");
        }
        catch (Exception e) { Console.Error.WriteLine(e); Environment.ExitCode = 1; }
        finally { window.Close(); }
    }
}
