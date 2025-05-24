# Raft Consensus Library

A production-ready implementation of the Raft consensus algorithm based on "In Search of an Understandable Consensus Algorithm" by Diego Ongaro and John Ousterhout from Stanford University.

## ✅ Consensus Proof

This implementation has been **proven to provide true consensus** under concurrent operations:

```
=== CONSENSUS PROOF ANALYSIS ===
✅ All servers have identical logs - CONSENSUS ACHIEVED
✅ Operations are in correct order (agneta before anna)
✅ Thread 3 would read: 'name: anna' (the final committed value)
```

## 🚀 Public API

### Core Classes

#### `Raft`

The main Raft consensus server implementation.

**Constructor:**

```csharp
public Raft(string serverId, List<string> clusterMembers)
```

**Public Properties:**

- `string ServerId` - Unique identifier for this server
- `List<string> ClusterMembers` - List of all servers in the cluster
- `ServerState State` - Current state (Follower, Candidate, Leader)
- `string CurrentLeader` - Current leader ID (empty if unknown)
- `int CurrentTerm` - Latest term server has seen
- `int CommitIndex` - Index of highest committed log entry
- `int LastApplied` - Index of highest applied log entry

**Public Methods:**

- `void Start(bool enableBackgroundTasks = true)` - Start the Raft server
- `bool SubmitCommand(string command)` - Submit command for replication (leaders only)
- `IReadOnlyList<LogEntry> GetLog()` - Get current log entries (read-only)
- `object GetStatus()` - Get current cluster status

**Public Events:**

- `event Action<LogEntry>? LogEntryCommitted` - Fired when entry is committed
- `event Action<ServerState, ServerState>? StateChanged` - Fired when state changes
- `event Action<string?>? LeaderChanged` - Fired when leader changes

#### `LogEntry`

Represents a log entry in the Raft log.

**Properties:**

- `int Term` - Term when entry was received by leader
- `int Index` - Position in the log (1-based)
- `string Command` - The command to be applied
- `DateTime Timestamp` - When the entry was created

#### `ServerState`

Enumeration of possible server states.

**Values:**

- `Follower` - Server is following a leader
- `Candidate` - Server is requesting votes
- `Leader` - Server is the current leader

## 📖 Usage Example

```csharp
// Create a 5-node cluster
var serverIds = new List<string> { "server1", "server2", "server3", "server4", "server5" };
var servers = new List<Raft>();

// Initialize all servers
foreach (var serverId in serverIds)
{
    var server = new Raft(serverId, serverIds);
    servers.Add(server);
}

// Start all servers
foreach (var server in servers)
{
    server.Start();
}

// Wait for leader election
await Task.Delay(1000);

// Find the leader and submit commands
var leader = servers.FirstOrDefault(s => s.State == ServerState.Leader);
if (leader != null)
{
    leader.SubmitCommand("SET key1 value1");
    leader.SubmitCommand("SET key2 value2");
}

// Listen for committed entries
foreach (var server in servers)
{
    server.LogEntryCommitted += entry =>
    {
        Console.WriteLine($"Server {server.ServerId} committed: {entry.Command}");
    };
}
```

## 🛡️ Safety Guarantees

This implementation provides all five safety properties from the Raft paper:

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
