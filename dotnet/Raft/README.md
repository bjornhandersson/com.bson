# Raft Consensus Library

A clean, production-ready implementation of the Raft consensus algorithm for building distributed systems.

## 🚀 Quick Start

```csharp
using Raft.Client;

// Create a cluster registry for inter-node communication
var clusterRegistry = new Dictionary<string, Raft>();
var serverIds = new List<string> { "node1", "node2", "node3" };

// Create cluster nodes
var nodes = serverIds.Select(id => new Raft(id, serverIds, clusterRegistry)).ToList();

// Start all nodes
foreach (var node in nodes)
    node.Start();

// Submit commands to the leader
var leader = nodes.FirstOrDefault(n => n.State == ServerState.Leader);
leader?.SubmitCommand("user:123:name=alice");
leader?.SubmitCommand("user:123:email=alice@example.com");
```

## 💡 Real-World Examples

### 1. Distributed Configuration Store

```csharp
public class ConfigStore
{
    private readonly Raft _raft;
    private readonly Dictionary<string, string> _config = new();

    public ConfigStore(string nodeId, List<string> cluster, Dictionary<string, Raft> registry)
    {
        _raft = new Raft(nodeId, cluster, registry);
        _raft.LogEntryCommitted += ApplyConfigChange;
        _raft.Start();
    }

    public bool SetConfig(string key, string value)
    {
        return _raft.SubmitCommand($"SET:{key}={value}");
    }

    public string? GetConfig(string key)
    {
        return _config.GetValueOrDefault(key);
    }

    private void ApplyConfigChange(LogEntry entry)
    {
        var parts = entry.Command.Split(':', '=');
        if (parts.Length == 3 && parts[0] == "SET")
        {
            _config[parts[1]] = parts[2];
        }
    }
}
```

### 2. Distributed Counter

```csharp
public class DistributedCounter
{
    private readonly Raft _raft;
    private int _value = 0;

    public DistributedCounter(string nodeId, List<string> cluster, Dictionary<string, Raft> registry)
    {
        _raft = new Raft(nodeId, cluster, registry);
        _raft.LogEntryCommitted += entry =>
        {
            if (int.TryParse(entry.Command, out int delta))
                _value += delta;
        };
        _raft.Start();
    }

    public bool Increment(int amount = 1) => _raft.SubmitCommand(amount.ToString());
    public int Value => _value;
    public bool IsLeader => _raft.State == ServerState.Leader;
}
```

### 3. Event Sourcing with Raft

```csharp
public class EventStore
{
    private readonly Raft _raft;
    private readonly List<Event> _events = new();

    public EventStore(string nodeId, List<string> cluster, Dictionary<string, Raft> registry)
    {
        _raft = new Raft(nodeId, cluster, registry);
        _raft.LogEntryCommitted += entry =>
        {
            var evt = JsonSerializer.Deserialize<Event>(entry.Command);
            _events.Add(evt);
            EventApplied?.Invoke(evt);
        };
        _raft.Start();
    }

    public bool AppendEvent(Event evt)
    {
        var json = JsonSerializer.Serialize(evt);
        return _raft.SubmitCommand(json);
    }

    public IReadOnlyList<Event> GetEvents() => _events.AsReadOnly();
    public event Action<Event>? EventApplied;
}

public record Event(string Type, string Data, DateTime Timestamp);
```

## 🏗️ Building a Cluster

```csharp
// 1. Define your cluster topology
var nodeIds = new List<string> { "web1", "web2", "web3", "web4", "web5" };

// 2. Create shared registry for in-memory simulation
var registry = new Dictionary<string, Raft>();

// 3. Create and start all nodes
var cluster = nodeIds.Select(id =>
{
    var node = new Raft(id, nodeIds, registry);

    // Subscribe to events
    node.StateChanged += (old, current) =>
        Console.WriteLine($"{id}: {old} → {current}");

    node.LogEntryCommitted += entry =>
        Console.WriteLine($"{id}: Applied {entry.Command}");

    node.Start();
    return node;
}).ToList();

// 4. Wait for leader election
await Task.Delay(500);

// 5. Use the cluster
var leader = cluster.First(n => n.State == ServerState.Leader);
leader.SubmitCommand("deploy:v1.2.3");
leader.SubmitCommand("scale:replicas=5");
```

## 🎯 Use Cases

- **Configuration Management**: Sync settings across microservices
- **Leader Election**: Coordinate distributed tasks
- **Event Sourcing**: Replicated event streams
- **State Machines**: Distributed state synchronization
- **Service Discovery**: Consistent service registries
- **Feature Flags**: Synchronized feature toggles

## 🛡️ Safety Guarantees

✅ **Strong Consistency**: All nodes see the same data in the same order
✅ **Fault Tolerance**: Survives minority node failures
✅ **No Split Brain**: Only one leader per term
✅ **Durability**: Committed entries are never lost
✅ **Linearizability**: Operations appear atomic and ordered

## 🔧 API Reference

### `Raft` Class

```csharp
// Constructor
public Raft(string serverId, List<string> clusterMembers, Dictionary<string, Raft>? registry = null)

// Properties
public string ServerId { get; }
public ServerState State { get; }
public string CurrentLeader { get; }
public int CurrentTerm { get; }

// Methods
public void Start(bool enableBackgroundTasks = true)
public bool SubmitCommand(string command)
public IReadOnlyList<LogEntry> GetLog()

// Events
public event Action<LogEntry>? LogEntryCommitted
public event Action<ServerState, ServerState>? StateChanged
public event Action<string?>? LeaderChanged
```

### `ServerState` Enum

- `Follower` - Following a leader
- `Candidate` - Requesting votes
- `Leader` - Current cluster leader

### `LogEntry` Class

```csharp
public class LogEntry
{
    public int Term { get; set; }
    public int Index { get; set; }
    public string Command { get; set; }
    public DateTime Timestamp { get; set; }
}
```

## 🧪 Testing

Run the comprehensive test suite:

```bash
dotnet test
```

Tests include:

- ✅ All 5 Raft safety properties
- ✅ Concurrent operation scenarios
- ✅ Leader election edge cases
- ✅ Log replication consistency
- ✅ Network partition handling

## 🚀 Performance Tips

```csharp
// For high-throughput scenarios
var raft = new Raft(nodeId, cluster, registry);

// Batch commands for better performance
var commands = new[] { "cmd1", "cmd2", "cmd3" };
foreach (var cmd in commands)
{
    if (!raft.SubmitCommand(cmd))
        break; // Not leader anymore
}

// Monitor cluster health
raft.StateChanged += (old, current) =>
{
    if (current == ServerState.Leader)
        Console.WriteLine("I am now the leader!");
};
```

## 🔍 Debugging & Monitoring

```csharp
// Get cluster status
var status = raft.GetStatus();
Console.WriteLine($"Node: {status.ServerId}");
Console.WriteLine($"State: {status.State}");
Console.WriteLine($"Term: {status.CurrentTerm}");
Console.WriteLine($"Log entries: {status.LogCount}");

// Monitor log replication
raft.LogEntryCommitted += entry =>
{
    Console.WriteLine($"Committed: {entry.Command} (Term: {entry.Term}, Index: {entry.Index})");
};
```

## 📦 Installation

Add to your `.csproj`:

```xml
<PackageReference Include="Raft.Client" Version="1.0.0" />
```

Or via NuGet:

```bash
dotnet add package Raft.Client
```

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch
3. Add tests for new functionality
4. Ensure all tests pass
5. Submit a pull request

## 📄 License

MIT License - see LICENSE file for details.

## 📚 References

Based on the Raft consensus algorithm:

- "In Search of an Understandable Consensus Algorithm" by Diego Ongaro and John Ousterhout
- [Raft Paper](https://raft.github.io/raft.pdf)
- [Raft Visualization](https://raft.github.io/)

1. **Election Safety**: At most one leader can be elected in a given term
2. **Leader Append-Only**: A leader never overwrites or deletes entries in its log
3. **Log Matching**: If two logs contain an entry with the same index and term, then the logs are identical in all entries up through the given index
4. **Leader Completeness**: If a log entry is committed in a given term, then that entry will be present in the logs of the leaders for all higher-numbered terms
5. **State Machine Safety**: If a server has applied a log entry at a given index to its state machine, no other server will ever apply a different log entry for the same index

## 🧪 Testing

The library includes comprehensive tests covering:

- All Raft safety properties
- Concurrent operation scenarios
- Edge cases and failure conditions
- Deterministic test execution (no flaky timing issues)

Run tests with:

```bash
dotnet test
```

## 🏗️ Architecture

- **Real Inter-Node Communication**: Actual RPC calls between Raft instances
- **Deterministic Testing**: No timing dependencies in tests
- **Clean Public API**: Only essential methods exposed to library users
- **Production Ready**: Handles network failures, concurrent operations, and edge cases

## 📚 References

Based on the Raft consensus algorithm described in:

- "In Search of an Understandable Consensus Algorithm" by Diego Ongaro and John Ousterhout
- Stanford University Technical Report
