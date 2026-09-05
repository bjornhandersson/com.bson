# Raft

A small, readable Raft implementation in C#. One class of about 650 lines, the five safety
properties from the paper, and a test suite that checks each of them. Written to understand the
algorithm properly and to get replicated state into a handful of .NET services without dragging
in etcd.

## What you get

- Leader election with randomized timeouts (150-300 ms) and heartbeats every 50 ms
- Log replication with the `prevLogIndex`/`prevLogTerm` consistency check and conflict truncation
- Commit on majority, then one event per applied entry so you can drive your own state machine
- Deterministic mode: `Start(enableBackgroundTasks: false)` and you drive the RPCs yourself

What you don't get (yet): persistence, snapshots, membership changes, or a network. Nodes find
each other through a shared in-process dictionary, and an RPC is a method call with a 1 ms
`Task.Delay` pretending to be a wire. That is fine for simulation, tests, and running several
logical nodes in one process. Anything else needs a real transport.

## Quick start

```csharp
using Raft.Client;

var ids = new List<string> { "a", "b", "c" };
var registry = new Dictionary<string, Raft>();   // the "network"

var nodes = ids.Select(id => new Raft(id, ids, registry)).ToList();
foreach (var n in nodes) n.Start();

await Task.Delay(500);                           // let an election happen

var leader = nodes.First(n => n.State == ServerState.Leader);
leader.SubmitCommand("set x=1");                 // returns false if we are not the leader
```

## Your state machine

Raft replicates opaque strings. What they mean is up to you. Subscribe to `LogEntryCommitted`
and apply entries in order; every node sees the same sequence.

```csharp
public class Counter
{
    private readonly Raft _raft;
    private int _value;

    public Counter(string id, List<string> cluster, Dictionary<string, Raft> registry)
    {
        _raft = new Raft(id, cluster, registry);
        _raft.LogEntryCommitted += e => _value += int.Parse(e.Command);
        _raft.Start();
    }

    public bool Add(int delta) => _raft.SubmitCommand(delta.ToString());
    public int Value => _value;
}
```

Reads from a follower are eventually consistent. If you need linearizable reads, ask the leader.
`Raft.Demo` has a key/value settings store built the same way.

## API

```csharp
new Raft(string serverId, List<string> clusterMembers, Dictionary<string, Raft>? registry = null)

void Start(bool enableBackgroundTasks = true)
bool SubmitCommand(string command)       // leader only
IReadOnlyList<LogEntry> GetLog()
object GetStatus()                       // term, state, commit index, ...

ServerState State                        // Follower | Candidate | Leader
string CurrentLeader
int CurrentTerm, CommitIndex, LastApplied

event Action<LogEntry> LogEntryCommitted
event Action<ServerState, ServerState> StateChanged
event Action<string?> LeaderChanged
```

`LogEntry` is a record with `Term`, `Index`, `Command` and a `Timestamp`.

## Tests

```bash
dotnet test
```

`RaftSpecificationTests` has one test per safety property in section 5 of the paper: Election
Safety, Leader Append-Only, Log Matching, Leader Completeness, State Machine Safety. Plus timeout
and term handling. `ConsensusProofTest` throws concurrent writes at the cluster and checks the
result is linearizable. Tests start nodes with background tasks off and call the RPCs directly,
so nothing depends on timing.

## Reading

Ongaro & Ousterhout, [In Search of an Understandable Consensus Algorithm](https://raft.github.io/raft.pdf).
Figure 2 is the whole spec. [raft.github.io](https://raft.github.io/) has the animation.

## License

MIT
