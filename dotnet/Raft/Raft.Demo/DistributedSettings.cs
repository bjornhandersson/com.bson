using System.Collections.Concurrent;
using System.Text.Json;
using Raft.Client;

namespace Raft.Demo;

/// <summary>
/// Distributed settings store using Raft consensus for synchronization
/// </summary>
public class DistributedSettings
{
    private readonly Client.Raft _raft;
    private readonly ConcurrentDictionary<string, string> _settings = new();
    private readonly object _lock = new();

    public string NodeId => _raft.ServerId;
    public ServerState State => _raft.State;
    public string? CurrentLeader =>
        string.IsNullOrEmpty(_raft.CurrentLeader) ? null : _raft.CurrentLeader;

    public DistributedSettings(
        string nodeId,
        List<string> clusterNodes,
        Dictionary<string, Client.Raft>? clusterRegistry = null
    )
    {
        _raft = new Raft.Client.Raft(nodeId, clusterNodes, clusterRegistry);

        // Subscribe to committed log entries to apply settings changes
        _raft.LogEntryCommitted += OnLogEntryCommitted;

        // Subscribe to state changes for logging
        _raft.StateChanged += (oldState, newState) => { };
        _raft.LeaderChanged += leaderId => { };
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
            // Setting submitted successfully
        }
        else
        {
            // Failed to submit - not the leader
        }

        return success;
    }

    /// <summary>
    /// Get a setting value with read metadata
    /// </summary>
    public SettingReadResult Get(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Setting name cannot be null or empty", nameof(name));

        var isLeader = _raft.State == ServerState.Leader;

        lock (_lock)
        {
            _settings.TryGetValue(name, out var value);

            return new SettingReadResult
            {
                Value = value,
                IsFromLeader = isLeader,
                NodeId = NodeId,
                CurrentLeader = CurrentLeader,
                NodeState = State,
            };
        }
    }

    /// <summary>
    /// Get a setting value (simple version for backward compatibility)
    /// </summary>
    public string? GetValue(string name)
    {
        return Get(name).Value;
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
            // Deletion submitted successfully
        }
        else
        {
            // Failed to delete - not the leader
        }

        return success;
    }

    /// <summary>
    /// Get all settings with read metadata
    /// </summary>
    public SettingsReadResult GetAll()
    {
        var isLeader = _raft.State == ServerState.Leader;

        lock (_lock)
        {
            var settings = new Dictionary<string, string>(_settings);

            return new SettingsReadResult
            {
                Settings = settings,
                IsFromLeader = isLeader,
                NodeId = NodeId,
                CurrentLeader = CurrentLeader,
                NodeState = State,
            };
        }
    }

    /// <summary>
    /// Get all settings (simple version for backward compatibility)
    /// </summary>
    public Dictionary<string, string> GetAllValues()
    {
        return GetAll().Settings;
    }

    /// <summary>
    /// Check if a setting exists with read metadata
    /// </summary>
    public SettingExistsResult Contains(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return new SettingExistsResult
            {
                Exists = false,
                IsFromLeader = _raft.State == ServerState.Leader,
                NodeId = NodeId,
                CurrentLeader = CurrentLeader,
                NodeState = State,
            };
        }

        var isLeader = _raft.State == ServerState.Leader;

        lock (_lock)
        {
            var exists = _settings.ContainsKey(name);

            return new SettingExistsResult
            {
                Exists = exists,
                IsFromLeader = isLeader,
                NodeId = NodeId,
                CurrentLeader = CurrentLeader,
                NodeState = State,
            };
        }
    }

    /// <summary>
    /// Check if a setting exists (simple version for backward compatibility)
    /// </summary>
    public bool ContainsKey(string name)
    {
        return Contains(name).Exists;
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
                        break;

                    case "DELETE":
                        var removed = _settings.TryRemove(command.Name, out var oldValue);
                        break;

                    default:
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            // Error applying log entry - silently continue
        }
    }

    private record SettingCommand
    {
        public required string Operation { get; init; }
        public required string Name { get; init; }
        public required string Value { get; init; }
    }

    public record SettingReadResult
    {
        public string? Value { get; init; }
        public bool IsFromLeader { get; init; }
        public string NodeId { get; init; } = string.Empty;
        public string? CurrentLeader { get; init; }
        public ServerState NodeState { get; init; }
    }

    public record SettingsReadResult
    {
        public Dictionary<string, string> Settings { get; init; } = new();
        public bool IsFromLeader { get; init; }
        public string NodeId { get; init; } = string.Empty;
        public string? CurrentLeader { get; init; }
        public ServerState NodeState { get; init; }
    }

    public record SettingExistsResult
    {
        public bool Exists { get; init; }
        public bool IsFromLeader { get; init; }
        public string NodeId { get; init; } = string.Empty;
        public string? CurrentLeader { get; init; }
        public ServerState NodeState { get; init; }
    }
}
