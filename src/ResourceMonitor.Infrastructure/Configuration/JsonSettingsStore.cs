using System.Text.Json;
using ResourceMonitor.Core;
namespace ResourceMonitor.Infrastructure.Configuration;

public sealed class JsonSettingsStore : ISettingsStore
{
    private readonly string path;
    public string? LastError { get; private set; }
    public JsonSettingsStore(string? path = null) => this.path = Path.GetFullPath(path ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ResourceMonitor", "settings.json"));
    public MonitorSettings Load()
    {
        try { if (!File.Exists(path)) return new(); var settings = JsonSerializer.Deserialize<MonitorSettings>(File.ReadAllText(path)) ?? new(); settings.Validate(); return settings; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or ArgumentException) { LastError = $"Налаштування відновлено до стандартних: {e.Message}"; return new(); }
    }
    public void Save(MonitorSettings settings)
    {
        settings.Validate(); Directory.CreateDirectory(Path.GetDirectoryName(path)!); var temporary = path + ".tmp"; File.WriteAllText(temporary, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true })); File.Move(temporary, path, true); LastError = null;
    }
}
