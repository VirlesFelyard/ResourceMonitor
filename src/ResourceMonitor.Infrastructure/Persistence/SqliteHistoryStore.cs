using Microsoft.Data.Sqlite;
using System.Text.Json;
using ResourceMonitor.Core;
namespace ResourceMonitor.Infrastructure.Persistence;

public sealed class SqliteHistoryStore : IHistoryStore
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly object gate = new();
    public SqliteHistoryStore()
    {
        connection.Open(); using var cmd = connection.CreateCommand();
        cmd.CommandText = "CREATE TABLE samples(time INTEGER PRIMARY KEY, data TEXT NOT NULL); CREATE TABLE cores(time INTEGER NOT NULL, core INTEGER NOT NULL, value REAL, status INTEGER NOT NULL, PRIMARY KEY(time,core)); CREATE TABLE events(id TEXT PRIMARY KEY,time INTEGER NOT NULL,data TEXT NOT NULL); CREATE INDEX event_time ON events(time);";
        cmd.ExecuteNonQuery();
    }
    public void Append(SystemSample sample, IReadOnlyList<LoadEvent> events)
    {
        lock (gate)
        {
            using var transaction = connection.BeginTransaction();
            using var cmd = connection.CreateCommand(); cmd.Transaction = transaction;
            cmd.CommandText = "INSERT INTO samples VALUES($time,$data)";
            cmd.Parameters.AddWithValue("$time", sample.Time.UtcTicks);
            cmd.Parameters.AddWithValue("$data", JsonSerializer.Serialize(sample));
            cmd.ExecuteNonQuery();
            for (var i = 0; i < sample.Cores.Count; i++)
            {
                cmd.Parameters.Clear(); cmd.CommandText = "INSERT INTO cores VALUES($time,$core,$value,$status)";
                cmd.Parameters.AddWithValue("$time", sample.Time.UtcTicks); cmd.Parameters.AddWithValue("$core", i); cmd.Parameters.AddWithValue("$value", (object?)sample.Cores[i].Value ?? DBNull.Value); cmd.Parameters.AddWithValue("$status", (int)sample.Cores[i].Status); cmd.ExecuteNonQuery();
            }
            foreach (var e in events)
            {
                cmd.Parameters.Clear(); cmd.CommandText = "INSERT INTO events VALUES($id,$time,$data)"; cmd.Parameters.AddWithValue("$id", e.Id.ToString()); cmd.Parameters.AddWithValue("$time", e.Time.UtcTicks); cmd.Parameters.AddWithValue("$data", JsonSerializer.Serialize(e)); cmd.ExecuteNonQuery();
            }
            transaction.Commit();
        }
    }
    public IReadOnlyList<SystemSample> Query(DateTimeOffset since)
    {
        lock (gate)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT data FROM samples WHERE time >= $since ORDER BY time";
            cmd.Parameters.AddWithValue("$since", since.UtcTicks);
            using var reader = cmd.ExecuteReader();
            var result = new List<SystemSample>();
            while (reader.Read()) result.Add(JsonSerializer.Deserialize<SystemSample>(reader.GetString(0))!);
            return result;
        }
    }
    public IReadOnlyList<LoadEvent> Events()
    {
        lock (gate)
        {
            using var cmd = connection.CreateCommand(); cmd.CommandText = "SELECT data FROM events ORDER BY time DESC"; using var reader = cmd.ExecuteReader(); var events = new List<LoadEvent>();
            while (reader.Read()) events.Add(JsonSerializer.Deserialize<LoadEvent>(reader.GetString(0))!); return events;
        }
    }
    public void Dispose() { lock (gate) connection.Dispose(); }
}
