# Raft Consensus Algorithm Implementation

A comprehensive C# implementation of the Raft consensus algorithm based on the paper "In Search of an Understandable Consensus Algorithm" by Diego Ongaro and John Ousterhout from Stanford University.

## Overview

This project implements the core Raft consensus algorithm with the following key features:

- **Leader Election**: Randomized timeout-based leader election
- **Log Replication**: Reliable log replication across cluster members
- **Safety**: Strong consistency guarantees and conflict resolution
- **State Management**: Proper handling of Follower, Candidate, and Leader states
- **RPC Implementation**: RequestVote and AppendEntries RPCs as specified in the paper
- **Immutable Design**: All data structures use immutable records with init-only properties
- **Null-Safe**: Eliminates nullable reference types in favor of empty strings and non-null defaults

## Project Structure

```
raft/
├── raft.client/           # Core Raft implementation library
│   ├── Raft.cs           # Main Raft algorithm implementation
│   ├── ServerState.cs    # Server state enumeration
│   ├── LogEntry.cs       # Log entry data structure
│   ├── RequestVoteArgs.cs # RequestVote RPC arguments
│   ├── RequestVoteResult.cs # RequestVote RPC results
│   ├── AppendEntriesArgs.cs # AppendEntries RPC arguments
│   ├── AppendEntriesResult.cs # AppendEntries RPC results
│   └── raft.client.csproj
├── raft.client.test/      # Comprehensive unit tests
│   ├── RaftTests.cs      # Test suite covering all major functionality
│   └── raft.client.test.csproj
├── raft.demo/             # Demonstration console application
│   ├── Program.cs        # Interactive demo showing Raft features
│   └── raft.demo.csproj
├── raft.sln              # Visual Studio solution file
└── README.md             # This file
```

## Key Features Implemented

### 1. Server States

- **Follower**: Default state, responds to RPCs from leaders and candidates
- **Candidate**: Intermediate state during leader election
- **Leader**: Handles client requests and replicates log entries

### 2. Core RPCs

- **RequestVote**: Used by candidates to gather votes during elections
- **AppendEntries**: Used by leaders for log replication and heartbeats

### 3. Persistent State

- `currentTerm`: Latest term server has seen
- `votedFor`: Candidate that received vote in current term
- `log[]`: Log entries with commands and terms

### 4. Volatile State

- `commitIndex`: Index of highest log entry known to be committed
- `lastApplied`: Index of highest log entry applied to state machine

### 5. Leader State

- `nextIndex[]`: For each server, index of next log entry to send
- `matchIndex[]`: For each server, index of highest log entry known to be replicated

## Algorithm Properties Guaranteed

1. **Election Safety**: At most one leader can be elected in a given term
2. **Leader Append-Only**: A leader never overwrites or deletes entries in its log
3. **Log Matching**: If two logs contain an entry with the same index and term, then the logs are identical in all entries up through the given index
4. **Leader Completeness**: If a log entry is committed in a given term, then that entry will be present in the logs of the leaders for all higher-numbered terms
5. **State Machine Safety**: If a server has applied a log entry at a given index to its state machine, no other server will ever apply a different log entry for the same index

## Getting Started

### Prerequisites

- .NET 8.0 SDK or later
- Visual Studio 2022 or VS Code (optional)

### Building the Project

```bash
dotnet build
```

### Running Tests

```bash
dotnet test
```

### Running the Demo

```bash
dotnet run --project raft.demo
```

## Usage Example

```csharp
using raft.client;

// Create a 5-server cluster
var serverIds = new List<string> { "server1", "server2", "server3", "server4", "server5" };
var raft = new Raft("server1", serverIds);

// Subscribe to events
raft.StateChanged += (oldState, newState) =>
    Console.WriteLine($"State changed: {oldState} -> {newState}");

raft.LogEntryCommitted += entry =>
    Console.WriteLine($"Committed: {entry.Command}");

// Start the server
raft.Start();

// Submit commands (only works if this server is the leader)
bool success = raft.SubmitCommand("SET x = 42");

// Check server status
var status = raft.GetStatus();
Console.WriteLine($"Server: {status}");
```

## Testing

The project includes comprehensive unit tests covering:

- Constructor validation and initialization
- RequestVote RPC implementation and edge cases
- AppendEntries RPC implementation and log consistency
- State transitions and event handling
- Command submission and log replication
- Error handling and conflict resolution

Run tests with:

```bash
dotnet test --verbosity normal
```

## Demo Application

The demo application (`raft.demo`) provides an interactive demonstration of:

1. **Cluster Creation**: Setting up a 5-server Raft cluster
2. **RequestVote RPC**: Demonstrating vote requests and responses
3. **AppendEntries RPC**: Showing heartbeats and log replication
4. **Command Submission**: Testing leader vs follower command handling
5. **State Management**: Observing state changes and leader election

## Implementation Notes

### Timing and Randomization

- Election timeouts: 150-300ms (randomized to prevent split votes)
- Heartbeat interval: 50ms
- Uses randomized election timeouts as specified in the paper

### Safety Mechanisms

- Term-based conflict resolution
- Log consistency checks in AppendEntries
- Up-to-date log validation in RequestVote
- Proper state transitions and event handling

### Limitations

This implementation is designed for educational and demonstration purposes. For production use, consider:

- Persistent storage for state
- Network communication layer
- Cluster membership changes
- Log compaction/snapshotting
- Performance optimizations

## References

- [Raft Paper](https://raft.github.io/raft.pdf): "In Search of an Understandable Consensus Algorithm" by Diego Ongaro and John Ousterhout
- [Raft Website](https://raft.github.io/): Official Raft consensus algorithm website
- [Raft Visualization](http://thesecretlivesofdata.com/raft/): Interactive visualization of the Raft algorithm

## License

This implementation is provided for educational purposes. Please refer to the original Raft paper for the algorithm specification and theoretical foundations.

## Contributing

This is an educational implementation. For improvements or bug fixes, please ensure all tests pass and maintain compatibility with the original Raft specification.
