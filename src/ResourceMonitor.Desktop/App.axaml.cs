using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using ResourceMonitor.Application;
using ResourceMonitor.Core;
using ResourceMonitor.Infrastructure.Platforms;
using ResourceMonitor.Infrastructure.Persistence;
using ResourceMonitor.Infrastructure.Configuration;
using ResourceMonitor.Desktop.ViewModels;
namespace ResourceMonitor.Desktop;

public partial class App : Avalonia.Application
{
    private ServiceProvider? services;
    public override void Initialize() => AvaloniaXamlLoader.Load(this);
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var collection = new ServiceCollection();
            collection.AddSingleton<IPlatformProvider>(_ => PlatformBase.Create()); collection.AddSingleton<IHistoryStore, SqliteHistoryStore>(); collection.AddSingleton<ISettingsStore>(_ => new JsonSettingsStore());
            collection.AddSingleton<LoadAnalyzer>(); collection.AddSingleton<MonitorService>(); collection.AddSingleton<SettingsViewModel>(); collection.AddSingleton<MainViewModel>();
            services = collection.BuildServiceProvider();
            var vm = services.GetRequiredService<MainViewModel>(); var window = new MainWindow { DataContext = vm }; desktop.MainWindow = window;
            window.Opened += async (_, _) => { await vm.InitializeAsync(); };
            var closing = false; var stopped = false;
            window.Closing += async (_, e) =>
            {
                if (stopped) return; e.Cancel = true; if (closing) return; closing = true;
                await services.DisposeAsync(); stopped = true; window.Close();
            };
        }
        base.OnFrameworkInitializationCompleted();
    }
}
