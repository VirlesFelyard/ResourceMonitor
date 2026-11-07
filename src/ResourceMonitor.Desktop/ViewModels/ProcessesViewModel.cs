using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ResourceMonitor.Core;
namespace ResourceMonitor.Desktop.ViewModels;

public partial class ProcessesViewModel : ObservableObject
{
    [ObservableProperty] private string search = "";
    [ObservableProperty] private int sortIndex = 1;
    [ObservableProperty] private bool descending = true;
    [ObservableProperty] private string count = "";
    private IReadOnlyList<ProcessSample> latest = [];
    public string[] SortOptions { get; } = ["Назва", "CPU", "RAM"];
    public ObservableCollection<ProcessRow> Rows { get; } = [];
    partial void OnSearchChanged(string value) => Refresh();
    partial void OnSortIndexChanged(int value) => Refresh();
    partial void OnDescendingChanged(bool value) => Refresh();
    public void Update(IReadOnlyList<ProcessSample> processes) { if (ReferenceEquals(latest, processes)) return; latest = processes; Refresh(); }
    private void Refresh()
    {
        var filtered = latest.Where(p => p.Name.Contains(Search, StringComparison.OrdinalIgnoreCase));
        var sorted = SortIndex switch
        {
            0 => Descending ? filtered.OrderByDescending(p => p.Name, StringComparer.OrdinalIgnoreCase) : filtered.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase),
            2 => Descending ? filtered.OrderByDescending(p => p.ResidentBytes) : filtered.OrderBy(p => p.ResidentBytes),
            _ => Descending ? filtered.OrderByDescending(p => p.Cpu.Value ?? -1) : filtered.OrderBy(p => p.Cpu.Value ?? -1)
        };
        var rows = sorted.ThenBy(p => p.Pid).Select(p => new ProcessRow(p)).ToArray();
        for (var i = 0; i < rows.Length; i++) { if (i < Rows.Count) Rows[i] = rows[i]; else Rows.Add(rows[i]); }
        while (Rows.Count > rows.Length) Rows.RemoveAt(Rows.Count - 1);
        Count = $"Показано {rows.Length} із {latest.Count} процесів";
    }
}
