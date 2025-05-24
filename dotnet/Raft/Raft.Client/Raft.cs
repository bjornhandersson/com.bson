using System.Collections.Concurrent;
using System.Text.Json;

namespace Raft.Client;

/// <summary>
/// Raft consensus algorithm implementation based on "In Search of an Understandable Consensus Algorithm"
/// by Diego Ongaro and John Ousterhout from Stanford University
/// </summary>
public class Raft
{
    /// <summary>
    /// Latest term server has seen (initialized to 0 on first boot, increases monotonically)
    /// </summary>
    public int CurrentTerm { get; private set; } = 0;

    /// <summary>
    /// CandidateId that received vote in current term (empty string if none)
    /// </summary>
    public string VotedFor { get; private set; } = string.Empty;

    /// <summary>
    /// Log entries; each entry contains command for state machine, and term when entry was received by leader
    /// </summary>
    public List<LogEntry> Log { get; private set; } = new();

    /// <summary>
    /// Index of highest log entry known to be committed (initialized to 0, increases monotonically)
    /// </summary>
    public int CommitIndex { get; private set; } = 0;

    /// <summary>
    /// Index of highest log entry applied to state machine (initialized to 0, increases monotonically)
    /// </summary>
    public int LastApplied { get; private set; } = 0;

    /// <summary>
    /// For each server, index of the next log entry to send to that server
    /// </summary>
    private readonly Dictionary<string, int> _nextIndex = new();

    /// <summary>
    /// For each server, index of highest log entry known to be replicated on server
    /// </summary>
    private readonly Dictionary<string, int> _matchIndex = new();

    /// <summary>
    /// Unique identifier for this server
    /// </summary>
    public string ServerId { get; private set; }

    /// <summary>
    /// Static registry of all Raft instances for inter-node communication
    /// </summary>
    private static readonly ConcurrentDictionary<string, Raft> _clusterRegistry = new();

    /// <summary>
    /// List of all servers in the cluster
    /// </summary>
    public List<string> ClusterMembers { get; private set; }

    /// <summary>
    /// Current state of this server
    /// </summary>
    public ServerState State { get; private set; } = ServerState.Follower;

    /// <summary>
    /// Current leader ID (empty string if unknown)
    /// </summary>
    public string CurrentLeader { get; private set; } = string.Empty;

    private readonly Random _random = new();
    private DateTime _lastHeartbeat = DateTime.UtcNow;
    private DateTime _electionTimeout;
    private readonly int _minElectionTimeout = 150; // milliseconds
    private readonly int _maxElectionTimeout = 300; // milliseconds
    private readonly int _heartbeatInterval = 50; // milliseconds

    /// <summary>
    /// Votes received in current election
    /// </summary>
    private readonly HashSet<string> _votesReceived = new();

    /// <summary>
    /// Event fired when a log entry is committed and ready to be applied to state machine
    /// </summary>
    public event Action<LogEntry>? LogEntryCommitted;

    /// <summary>
    /// Event fired when server state changes
    /// </summary>
    public event Action<ServerState, ServerState>? StateChanged;

    /// <summary>
    /// Event fired when leader changes
    /// </summary>
    public event Action<string?>? LeaderChanged;

    /// <summary>
    /// Initialize a new Raft server instance
    /// </summary>
    /// <param name="serverId">Unique identifier for this server</param>
    /// <param name="clusterMembers">List of all servers in the cluster</param>
    public Raft(string serverId, List<string> clusterMembers)
    {
        ServerId = serverId ?? throw new ArgumentNullException(nameof(serverId));
        ClusterMembers = clusterMembers ?? throw new ArgumentNullException(nameof(clusterMembers));

        if (!ClusterMembers.Contains(ServerId))
        {
            throw new ArgumentException("Server ID must be included in cluster members");
        }

        // Register this instance in the cluster registry for inter-node communication
        _clusterRegistry.TryAdd(ServerId, this);

        ResetElectionTimeout();
    }

    /// <summary>
    /// Start the Raft server
    /// </summary>
    /// <param name="enableBackgroundTasks">Whether to start background election and heartbeat tasks</param>
    public void Start(bool enableBackgroundTasks = true)
    {
        // Initialize as follower
        ConvertToFollower(CurrentTerm);

        if (enableBackgroundTasks)
        {
            // Start background tasks for election timeout and heartbeats
            _ = Task.Run(ElectionTimeoutLoop);
            _ = Task.Run(HeartbeatLoop);
        }
    }

    /// <summary>
    /// Submit a new command to be replicated across the cluster
    /// </summary>
    /// <param name="command">Command to be replicated</param>
    /// <returns>True if command was accepted (only leaders accept commands)</returns>
    public bool SubmitCommand(string command)
    {
        if (State != ServerState.Leader)
        {
            return false;
        }

        var logEntry = new LogEntry
        {
            Term = CurrentTerm,
            Index = Log.Count + 1,
            Command = command,
            Timestamp = DateTime.UtcNow,
        };

        Log.Add(logEntry);

        // Immediately try to replicate to followers
        _ = Task.Run(() => ReplicateToFollowers());

        return true;
    }

    /// <summary>
    /// RequestVote RPC implementation
    /// Invoked by candidates to gather votes
    /// </summary>
    public RequestVoteResult RequestVote(RequestVoteArgs args)
    {
        // Reply false if term < currentTerm
        if (args.Term < CurrentTerm)
        {
            return new RequestVoteResult { Term = CurrentTerm, VoteGranted = false };
        }

        // If RPC request contains term T > currentTerm: set currentTerm = T, convert to follower
        if (args.Term > CurrentTerm)
        {
            ConvertToFollower(args.Term);
        }

        // If votedFor is empty or candidateId, and candidate's log is at least as up-to-date as receiver's log, grant vote
        if (
            (string.IsNullOrEmpty(VotedFor) || VotedFor == args.CandidateId)
            && IsLogUpToDate(args.LastLogIndex, args.LastLogTerm)
        )
        {
            VotedFor = args.CandidateId;
            _lastHeartbeat = DateTime.UtcNow; // Reset election timeout
            ResetElectionTimeout();
            return new RequestVoteResult { Term = CurrentTerm, VoteGranted = true };
        }

        return new RequestVoteResult { Term = CurrentTerm, VoteGranted = false };
    }

    /// <summary>
    /// AppendEntries RPC implementation
    /// Invoked by leader to replicate log entries; also used as heartbeat
    /// </summary>
    public AppendEntriesResult AppendEntries(AppendEntriesArgs args)
    {
        // Reply false if term < currentTerm
        if (args.Term < CurrentTerm)
        {
            return new AppendEntriesResult { Term = CurrentTerm, Success = false };
        }

        // If RPC request contains term T > currentTerm: set currentTerm = T, convert to follower
        if (args.Term > CurrentTerm)
        {
            ConvertToFollower(args.Term);
        }

        // Valid leader, reset election timeout
        _lastHeartbeat = DateTime.UtcNow;
        ResetElectionTimeout();
        SetCurrentLeader(args.LeaderId);

        // Reply false if log doesn't contain an entry at prevLogIndex whose term matches prevLogTerm
        if (args.PrevLogIndex > 0)
        {
            if (
                args.PrevLogIndex > Log.Count
                || (
                    args.PrevLogIndex <= Log.Count
                    && Log[args.PrevLogIndex - 1].Term != args.PrevLogTerm
                )
            )
            {
                return new AppendEntriesResult { Term = CurrentTerm, Success = false };
            }
        }

        // If an existing entry conflicts with a new one (same index but different terms),
        // delete the existing entry and all that follow it
        for (int i = 0; i < args.Entries.Count; i++)
        {
            var entryIndex = args.PrevLogIndex + i + 1;
            var newEntry = args.Entries[i];

            if (entryIndex <= Log.Count)
            {
                if (Log[entryIndex - 1].Term != newEntry.Term)
                {
                    // Remove conflicting entry and all that follow
                    Log.RemoveRange(entryIndex - 1, Log.Count - (entryIndex - 1));
                    break;
                }
            }
        }

        // Append any new entries not already in the log
        foreach (var entry in args.Entries)
        {
            if (entry.Index > Log.Count)
            {
                Log.Add(entry);
            }
        }

        // If leaderCommit > commitIndex, set commitIndex = min(leaderCommit, index of last new entry)
        if (args.LeaderCommit > CommitIndex)
        {
            var lastNewEntryIndex =
                args.Entries.Count > 0 ? args.Entries.Last().Index : args.PrevLogIndex;
            CommitIndex = Math.Min(args.LeaderCommit, lastNewEntryIndex);

            // Apply newly committed entries to state machine
            ApplyCommittedEntries();
        }

        return new AppendEntriesResult { Term = CurrentTerm, Success = true };
    }

    /// <summary>
    /// Convert to follower state
    /// </summary>
    private void ConvertToFollower(int term)
    {
        var oldState = State;
        var oldTerm = CurrentTerm;
        CurrentTerm = term;
        VotedFor = string.Empty;
        State = ServerState.Follower;

        // Fire event if state changed OR term changed
        if (oldState != ServerState.Follower || oldTerm != term)
        {
            StateChanged?.Invoke(oldState, State);
        }
    }

    /// <summary>
    /// Convert to candidate state and start election
    /// </summary>
    private void ConvertToCandidate()
    {
        var oldState = State;
        State = ServerState.Candidate;
        CurrentTerm++;
        VotedFor = ServerId;
        _votesReceived.Clear();
        _votesReceived.Add(ServerId); // Vote for self
        ResetElectionTimeout();

        StateChanged?.Invoke(oldState, State);

        // Send RequestVote RPCs to all other servers
        _ = Task.Run(SendRequestVoteRPCs);
    }

    /// <summary>
    /// Convert to leader state
    /// </summary>
    private void ConvertToLeader()
    {
        var oldState = State;
        State = ServerState.Leader;
        SetCurrentLeader(ServerId);

        // Initialize leader state
        _nextIndex.Clear();
        _matchIndex.Clear();

        foreach (var server in ClusterMembers.Where(s => s != ServerId))
        {
            _nextIndex[server] = Log.Count + 1;
            _matchIndex[server] = 0;
        }

        StateChanged?.Invoke(oldState, State);

        // Send initial empty AppendEntries RPCs (heartbeat) to each server
        _ = Task.Run(SendHeartbeats);
    }

    /// <summary>
    /// Set the current leader
    /// </summary>
    private void SetCurrentLeader(string leaderId)
    {
        if (CurrentLeader != leaderId)
        {
            CurrentLeader = leaderId ?? string.Empty;
            LeaderChanged?.Invoke(leaderId);
        }
    }

    /// <summary>
    /// Reset election timeout to a random value
    /// </summary>
    private void ResetElectionTimeout()
    {
        var timeout = _random.Next(_minElectionTimeout, _maxElectionTimeout);
        _electionTimeout = DateTime.UtcNow.AddMilliseconds(timeout);
    }

    /// <summary>
    /// Main election timeout loop
    /// </summary>
    private async Task ElectionTimeoutLoop()
    {
        while (true)
        {
            await Task.Delay(10); // Check every 10ms

            if (State != ServerState.Leader && DateTime.UtcNow > _electionTimeout)
            {
                // Election timeout elapsed, start new election
                ConvertToCandidate();
            }
        }
    }

    /// <summary>
    /// Main heartbeat loop for leaders
    /// </summary>
    private async Task HeartbeatLoop()
    {
        while (true)
        {
            await Task.Delay(_heartbeatInterval);

            if (State == ServerState.Leader)
            {
                _ = Task.Run(SendHeartbeats);
            }
        }
    }

    /// <summary>
    /// Send RequestVote RPCs to all other servers
    /// </summary>
    private async Task SendRequestVoteRPCs()
    {
        var args = new RequestVoteArgs
        {
            Term = CurrentTerm,
            CandidateId = ServerId,
            LastLogIndex = Log.Count,
            LastLogTerm = Log.Count > 0 ? Log.Last().Term : 0,
        };

        var tasks = ClusterMembers
            .Where(server => server != ServerId)
            .Select(server => SendRequestVoteRPC(server, args))
            .ToArray();

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Send RequestVote RPC to a specific server
    /// </summary>
    private async Task SendRequestVoteRPC(string serverId, RequestVoteArgs args)
    {
        try
        {
            await Task.Delay(1); // Simulate network delay

            // Get the target server from the cluster registry
            if (_clusterRegistry.TryGetValue(serverId, out var targetServer))
            {
                // Make actual RPC call to the target server
                var result = targetServer.RequestVote(args);
                
                // Process the vote result
                ProcessRequestVoteResult(serverId, result);
            }
        }
        catch (Exception)
        {
            // Handle RPC failure - server might be down
        }
    }

    /// <summary>
    /// Process RequestVote RPC result
    /// </summary>
    private void ProcessRequestVoteResult(string serverId, RequestVoteResult result)
    {
        if (result.Term > CurrentTerm)
        {
            ConvertToFollower(result.Term);
            return;
        }

        if (State == ServerState.Candidate && result.VoteGranted && result.Term == CurrentTerm)
        {
            _votesReceived.Add(serverId); // Track actual server IDs

            // Check if we have majority votes
            if (_votesReceived.Count > ClusterMembers.Count / 2)
            {
                ConvertToLeader();
            }
        }
    }

    /// <summary>
    /// Send heartbeats to all followers
    /// </summary>
    private async Task SendHeartbeats()
    {
        if (State != ServerState.Leader)
            return;

        var tasks = ClusterMembers
            .Where(server => server != ServerId)
            .Select(SendAppendEntriesRPC)
            .ToArray();

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Replicate log entries to followers
    /// </summary>
    private async Task ReplicateToFollowers()
    {
        if (State != ServerState.Leader)
            return;

        var tasks = ClusterMembers
            .Where(server => server != ServerId)
            .Select(SendAppendEntriesRPC)
            .ToArray();

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Send AppendEntries RPC to a specific server
    /// </summary>
    private async Task SendAppendEntriesRPC(string serverId)
    {
        if (State != ServerState.Leader)
            return;

        try
        {
            var nextIndex = _nextIndex.GetValueOrDefault(serverId, Log.Count + 1);
            var prevLogIndex = nextIndex - 1;
            var prevLogTerm =
                prevLogIndex > 0 && prevLogIndex <= Log.Count ? Log[prevLogIndex - 1].Term : 0;

            var entries = new List<LogEntry>();
            if (nextIndex <= Log.Count)
            {
                entries = Log.Skip(nextIndex - 1).ToList();
            }

            var args = new AppendEntriesArgs
            {
                Term = CurrentTerm,
                LeaderId = ServerId,
                PrevLogIndex = prevLogIndex,
                PrevLogTerm = prevLogTerm,
                Entries = entries,
                LeaderCommit = CommitIndex,
            };

            await Task.Delay(1); // Simulate network delay

            // Get the target server from the cluster registry
            if (_clusterRegistry.TryGetValue(serverId, out var targetServer))
            {
                // Make actual RPC call to the target server
                var result = targetServer.AppendEntries(args);
                ProcessAppendEntriesResult(serverId, args, result);
            }
        }
        catch (Exception)
        {
            // Handle RPC failure
        }
    }

    /// <summary>
    /// Process AppendEntries RPC result
    /// </summary>
    private void ProcessAppendEntriesResult(
        string serverId,
        AppendEntriesArgs args,
        AppendEntriesResult result
    )
    {
        if (result.Term > CurrentTerm)
        {
            ConvertToFollower(result.Term);
            return;
        }

        if (State != ServerState.Leader || result.Term != CurrentTerm)
            return;

        if (result.Success)
        {
            // Update nextIndex and matchIndex for follower
            if (args.Entries.Count > 0)
            {
                _nextIndex[serverId] = args.Entries.Last().Index + 1;
                _matchIndex[serverId] = args.Entries.Last().Index;
            }

            // Check if we can advance commitIndex
            UpdateCommitIndex();
        }
        else
        {
            // Decrement nextIndex and retry
            _nextIndex[serverId] = Math.Max(1, _nextIndex.GetValueOrDefault(serverId, 1) - 1);
        }
    }

    /// <summary>
    /// Update commit index based on majority replication
    /// </summary>
    private void UpdateCommitIndex()
    {
        if (State != ServerState.Leader)
            return;

        for (int n = Log.Count; n > CommitIndex; n--)
        {
            if (Log[n - 1].Term == CurrentTerm)
            {
                var replicationCount = 1; // Count self
                replicationCount += _matchIndex.Values.Count(index => index >= n);

                if (replicationCount > ClusterMembers.Count / 2)
                {
                    CommitIndex = n;
                    ApplyCommittedEntries();
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Apply committed entries to state machine
    /// </summary>
    private void ApplyCommittedEntries()
    {
        while (LastApplied < CommitIndex)
        {
            LastApplied++;
            if (LastApplied <= Log.Count)
            {
                var entry = Log[LastApplied - 1];
                LogEntryCommitted?.Invoke(entry);
            }
        }
    }

    /// <summary>
    /// Check if candidate's log is at least as up-to-date as receiver's log
    /// </summary>
    private bool IsLogUpToDate(int lastLogIndex, int lastLogTerm)
    {
        var ourLastLogTerm = Log.Count > 0 ? Log.Last().Term : 0;
        var ourLastLogIndex = Log.Count;

        // If logs have last entries with different terms, then the log with the later term is more up-to-date
        if (lastLogTerm != ourLastLogTerm)
        {
            return lastLogTerm >= ourLastLogTerm;
        }

        // If logs end with the same term, then whichever log is longer is more up-to-date
        return lastLogIndex >= ourLastLogIndex;
    }

    /// <summary>
    /// Get current log entries (read-only)
    /// </summary>
    public IReadOnlyList<LogEntry> GetLog() => Log.AsReadOnly();

    /// <summary>
    /// Get current cluster status
    /// </summary>
    public object GetStatus()
    {
        return new
        {
            ServerId,
            State = State.ToString(),
            CurrentTerm,
            VotedFor,
            CurrentLeader,
            LogCount = Log.Count,
            CommitIndex,
            LastApplied,
            ClusterSize = ClusterMembers.Count,
        };
    }

    /// <summary>
    /// Clear the cluster registry (for testing purposes)
    /// </summary>
    public static void ClearClusterRegistry()
    {
        _clusterRegistry.Clear();
    }

    /// <summary>
    /// Manually trigger an election (for testing purposes)
    /// </summary>
    public void TriggerElection()
    {
        if (State != ServerState.Leader)
        {
            ConvertToCandidate();
        }
    }

    /// <summary>
    /// Manually send RequestVote RPCs synchronously (for testing purposes)
    /// </summary>
    public void SendRequestVoteRPCsSync()
    {
        if (State != ServerState.Candidate) return;

        var args = new RequestVoteArgs
        {
            Term = CurrentTerm,
            CandidateId = ServerId,
            LastLogIndex = Log.Count,
            LastLogTerm = Log.Count > 0 ? Log.Last().Term : 0,
        };

        foreach (var serverId in ClusterMembers.Where(s => s != ServerId))
        {
            if (_clusterRegistry.TryGetValue(serverId, out var targetServer))
            {
                var result = targetServer.RequestVote(args);
                ProcessRequestVoteResult(serverId, result);
            }
        }
    }
}
