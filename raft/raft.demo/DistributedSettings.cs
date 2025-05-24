using System.Collections.Concurrent;
using System.Text.Json;
using Raft.Client;

namespace Raft.Demo;

/// <summary>
/// Distributed settings store using Raft consensus for synchronization
/// </summary>
public class DistributedSettings
{
    private readonly Raft.Client.Raft _raft;
    private readonly ConcurrentDictionary<string, string> _settings = new();
    private readonly object _lock = new();

    public string NodeId => _raft.ServerId;
    public ServerState State => _raft.State;
    public string? CurrentLeader =>
        string.IsNullOrEmpty(_raft.CurrentLeader) ? null : _raft.CurrentLeader;

    public DistributedSettings(string nodeId, List<string> clusterNodes)
    {
        _raft = new Raft.Client.Raft(nodeId, clusterNodes);

        // Subscribe to committed log entries to apply settings changes
        _raft.LogEntryCommitted += OnLogEntryCommitted;

        // Subscribe to state changes for logging
        _raft.StateChanged += (oldState, newState) =>
            Console.WriteLine($"[{NodeId}] State changed: {oldState} -> {newState}");

        _raft.LeaderChanged += leaderId =>
            Console.WriteLine($"[{NodeId}] Leader changed to: {leaderId ?? "None"}");
    }

    public void Start()
    {
        _raft.Start();
    }

    /// <summary>
    /// Add or update a setting. Only works if this node is the leader.
    /// </summary>
    public bool Add(string name, string value)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Setting name cannot be null or empty", nameof(name));

        var command = JsonSerializer.Serialize(
            new SettingCommand
            {
                Operation = "SET",
                Name = name,
                Value = value ?? string.Empty,
            }
        );

        var success = _raft.SubmitCommand(command);

        if (success)
        {
            Console.WriteLine($"[{NodeId}] Setting '{name}' = '{value}' submitted to cluster");
        }
        else
        {
            Console.WriteLine(
                $"[{NodeId}] Failed to submit setting '{name}' - not the leader. Current leader: {CurrentLeader ?? "Unknown"}"
            );
        }

        return success;
    }

    /// <summary>
    /// Get a setting value
    /// </summary>
    public string? Get(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Setting name cannot be null or empty", nameof(name));

        lock (_lock)
        {
            _settings.TryGetValue(name, out var value);
            Console.WriteLine($"[{NodeId}] Get '{name}' = '{value ?? "null"}'");
            return value;
        }
    }

    /// <summary>
    /// Delete a setting. Only works if this node is the leader.
    /// </summary>
    public bool Delete(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Setting name cannot be null or empty", nameof(name));

        var command = JsonSerializer.Serialize(
            new SettingCommand
            {
                Operation = "DELETE",
                Name = name,
                Value = string.Empty,
            }
        );

        var success = _raft.SubmitCommand(command);

        if (success)
        {
            Console.WriteLine($"[{NodeId}] Setting '{name}' deletion submitted to cluster");
        }
        else
        {
            Console.WriteLine(
                $"[{NodeId}] Failed to delete setting '{name}' - not the leader. Current leader: {CurrentLeader ?? "Unknown"}"
            );
        }

        return success;
    }

    /// <summary>
    /// Get all settings
    /// </summary>
    public Dictionary<string, string> GetAll()
    {
        lock (_lock)
        {
            var result = new Dictionary<string, string>(_settings);
            Console.WriteLine($"[{NodeId}] GetAll() returned {result.Count} settings");
            return result;
        }
    }

    /// <summary>
    /// Check if a setting exists
    /// </summary>
    public bool Contains(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        lock (_lock)
        {
            var exists = _settings.ContainsKey(name);
            Console.WriteLine($"[{NodeId}] Contains '{name}' = {exists}");
            return exists;
        }
    }

    /// <summary>
    /// Get the current status of this node
    /// </summary>
    public object GetStatus()
    {
        lock (_lock)
        {
            return new
            {
                NodeId,
                State = State.ToString(),
                CurrentLeader,
                SettingsCount = _settings.Count,
                RaftStatus = _raft.GetStatus(),
            };
        }
    }

    private void OnLogEntryCommitted(LogEntry entry)
    {
        try
        {
            var command = JsonSerializer.Deserialize<SettingCommand>(entry.Command);
            if (command == null)
                return;

            lock (_lock)
            {
                switch (command.Operation.ToUpper())
                {
                    case "SET":
                        _settings[command.Name] = command.Value;
                        Console.WriteLine(
                            $"[{NodeId}] Applied: SET '{command.Name}' = '{command.Value}' (Term: {entry.Term}, Index: {entry.Index})"
                        );
                        break;

                    case "DELETE":
                        var removed = _settings.TryRemove(command.Name, out var oldValue);
                        Console.WriteLine(
                            $"[{NodeId}] Applied: DELETE '{command.Name}' (was: '{oldValue}', removed: {removed}) (Term: {entry.Term}, Index: {entry.Index})"
                        );
                        break;

                    default:
                        Console.WriteLine($"[{NodeId}] Unknown operation: {command.Operation}");
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{NodeId}] Error applying log entry: {ex.Message}");
        }
    }

    private record SettingCommand
    {
        public required string Operation { get; init; }
        public required string Name { get; init; }
        public required string Value { get; init; }
    }
}
