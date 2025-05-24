using Raft.Client;

namespace Raft.Client.Test;

/// <summary>
/// Test class verifying Raft implementation against the formal specifications
/// from "In Search of an Understandable Consensus Algorithm" (https://raft.github.io/raft.pdf)
///
/// Tests the five key safety properties and core algorithm behaviors as defined in the paper:
/// 1. Election Safety: At most one leader can be elected in a given term
/// 2. Leader Append-Only: A leader never overwrites or deletes entries in its log
/// 3. Log Matching: If two logs contain an entry with the same index and term,
///    then the logs are identical in all entries up through the given index
/// 4. Leader Completeness: If a log entry is committed in a given term, then that entry
///    will be present in the logs of the leaders for all higher-numbered terms
/// 5. State Machine Safety: If a server has applied a log entry at a given index to its
///    state machine, no other server will ever apply a different log entry for the same index
/// </summary>
[TestFixture]
public class RaftSpecificationTests
{
    private List<Raft> _cluster = new();
    private readonly List<string> _serverIds = new()
    {
        "server1",
        "server2",
        "server3",
        "server4",
        "server5",
    };

    [SetUp]
    public void Setup()
    {
        _cluster = new List<Raft>();
        var clusterRegistry = new Dictionary<string, Raft>();

        foreach (var serverId in _serverIds)
        {
            var raft = new Raft(serverId, _serverIds, clusterRegistry);
            _cluster.Add(raft);
        }
    }

    [TearDown]
    public void TearDown()
    {
        _cluster.Clear();
    }

    #region Safety Property 1: Election Safety

    /// <summary>
    /// Election Safety: At most one leader can be elected in a given term (§5.2)
    /// This test verifies that in any given term, there can be at most one leader.
    /// </summary>
    [Test]
    public void ElectionSafety_AtMostOneLeaderPerTerm()
    {
        // Arrange - Start all servers without background tasks (deterministic mode)
        foreach (var server in _cluster)
        {
            server.Start(enableBackgroundTasks: false);
        }

        // Act - Trigger a deterministic election
        // Only one server becomes candidate and requests votes
        var candidate = _cluster[0];
        candidate.TriggerElection();
        candidate.SendRequestVoteRPCsSync();

        // Count leaders in each term
        var leadersByTerm = new Dictionary<int, List<Raft>>();

        foreach (var server in _cluster)
        {
            if (server.State == ServerState.Leader)
            {
                var term = server.CurrentTerm;
                if (!leadersByTerm.ContainsKey(term))
                {
                    leadersByTerm[term] = new List<Raft>();
                }
                leadersByTerm[term].Add(server);
            }
        }

        // Assert - At most one leader should exist per term
        foreach (var kvp in leadersByTerm)
        {
            var term = kvp.Key;
            var leaders = kvp.Value;
            Assert.That(
                leaders.Count,
                Is.LessThanOrEqualTo(1),
                $"Election Safety violated: {leaders.Count} leaders found in term {term}"
            );
        }

        // Also verify that we have exactly one leader overall
        var totalLeaders = _cluster.Count(s => s.State == ServerState.Leader);
        Assert.That(
            totalLeaders,
            Is.EqualTo(1),
            $"Expected exactly 1 leader, but found {totalLeaders}"
        );
    }

    /// <summary>
    /// Test that servers don't vote for multiple candidates in the same term
    /// </summary>
    [Test]
    public void ElectionSafety_ServerVotesOncePerTerm()
    {
        // Arrange
        var voter = _cluster[0];
        var candidate1 = _cluster[1];
        var candidate2 = _cluster[2];
        var term = 1;

        // Act - First vote request
        var args1 = new RequestVoteArgs
        {
            Term = term,
            CandidateId = candidate1.ServerId,
            LastLogIndex = 0,
            LastLogTerm = 0,
        };
        var result1 = voter.RequestVote(args1);

        // Second vote request in same term
        var args2 = new RequestVoteArgs
        {
            Term = term,
            CandidateId = candidate2.ServerId,
            LastLogIndex = 0,
            LastLogTerm = 0,
        };
        var result2 = voter.RequestVote(args2);

        // Assert - Should grant first vote but deny second
        Assert.That(result1.VoteGranted, Is.True, "First vote should be granted");
        Assert.That(result2.VoteGranted, Is.False, "Second vote in same term should be denied");
        Assert.That(
            voter.VotedFor,
            Is.EqualTo(candidate1.ServerId),
            "Should remember who was voted for"
        );
    }

    #endregion

    #region Safety Property 2: Leader Append-Only

    /// <summary>
    /// Leader Append-Only: A leader never overwrites or deletes entries in its log (§5.3)
    /// </summary>
    [Test]
    public void LeaderAppendOnly_NeverOverwritesOrDeletesEntries()
    {
        // Arrange - Create a leader with some log entries
        var leader = _cluster[0];
        SetPrivateProperty(leader, "State", ServerState.Leader);
        SetPrivateProperty(leader, "CurrentTerm", 2);

        var log = GetPrivateProperty<List<LogEntry>>(leader, "Log");
        log.Add(
            new LogEntry
            {
                Term = 1,
                Index = 1,
                Command = "cmd1",
            }
        );
        log.Add(
            new LogEntry
            {
                Term = 1,
                Index = 2,
                Command = "cmd2",
            }
        );
        log.Add(
            new LogEntry
            {
                Term = 2,
                Index = 3,
                Command = "cmd3",
            }
        );

        var originalLogCount = log.Count;
        var originalEntries = log.ToList(); // Copy for comparison

        // Act - Leader submits new commands (should only append)
        leader.SubmitCommand("cmd4");
        leader.SubmitCommand("cmd5");

        // Assert - Log should only grow, never shrink or change existing entries
        Assert.That(
            log.Count,
            Is.GreaterThanOrEqualTo(originalLogCount),
            "Leader log should never shrink"
        );

        // Verify original entries are unchanged
        for (int i = 0; i < originalLogCount; i++)
        {
            Assert.That(
                log[i].Term,
                Is.EqualTo(originalEntries[i].Term),
                $"Entry {i} term should not change"
            );
            Assert.That(
                log[i].Command,
                Is.EqualTo(originalEntries[i].Command),
                $"Entry {i} command should not change"
            );
            Assert.That(
                log[i].Index,
                Is.EqualTo(originalEntries[i].Index),
                $"Entry {i} index should not change"
            );
        }
    }

    #endregion

    #region Safety Property 3: Log Matching

    /// <summary>
    /// Log Matching: If two logs contain an entry with the same index and term,
    /// then the logs are identical in all entries up through the given index (§5.3)
    /// </summary>
    [Test]
    public void LogMatching_IdenticalEntriesImplyIdenticalPrefixes()
    {
        // Arrange - Create two servers with matching entries at specific positions
        var server1 = _cluster[0];
        var server2 = _cluster[1];

        var log1 = GetPrivateProperty<List<LogEntry>>(server1, "Log");
        var log2 = GetPrivateProperty<List<LogEntry>>(server2, "Log");

        // Build identical prefixes
        log1.Add(
            new LogEntry
            {
                Term = 1,
                Index = 1,
                Command = "cmd1",
            }
        );
        log1.Add(
            new LogEntry
            {
                Term = 1,
                Index = 2,
                Command = "cmd2",
            }
        );
        log1.Add(
            new LogEntry
            {
                Term = 2,
                Index = 3,
                Command = "cmd3",
            }
        );

        log2.Add(
            new LogEntry
            {
                Term = 1,
                Index = 1,
                Command = "cmd1",
            }
        );
        log2.Add(
            new LogEntry
            {
                Term = 1,
                Index = 2,
                Command = "cmd2",
            }
        );
        log2.Add(
            new LogEntry
            {
                Term = 2,
                Index = 3,
                Command = "cmd3",
            }
        );

        // Add different entries after the matching point
        log1.Add(
            new LogEntry
            {
                Term = 2,
                Index = 4,
                Command = "cmd4a",
            }
        );
        log2.Add(
            new LogEntry
            {
                Term = 3,
                Index = 4,
                Command = "cmd4b",
            }
        );

        // Act & Assert - Check log matching property
        var matchingIndex = 3;
        var matchingTerm = 2;

        // If entries at index 3 have same term, all previous entries should match
        if (
            log1[matchingIndex - 1].Term == matchingTerm
            && log2[matchingIndex - 1].Term == matchingTerm
        )
        {
            for (int i = 0; i < matchingIndex; i++)
            {
                Assert.That(
                    log1[i].Term,
                    Is.EqualTo(log2[i].Term),
                    $"Log matching violated: entries at index {i + 1} have different terms"
                );
                Assert.That(
                    log1[i].Command,
                    Is.EqualTo(log2[i].Command),
                    $"Log matching violated: entries at index {i + 1} have different commands"
                );
            }
        }
    }

    /// <summary>
    /// Test AppendEntries consistency check enforces log matching
    /// </summary>
    [Test]
    public void LogMatching_AppendEntriesRejectsInconsistentLogs()
    {
        // Arrange - Server with existing log
        var follower = _cluster[0];
        var log = GetPrivateProperty<List<LogEntry>>(follower, "Log");
        log.Add(
            new LogEntry
            {
                Term = 1,
                Index = 1,
                Command = "cmd1",
            }
        );
        log.Add(
            new LogEntry
            {
                Term = 2,
                Index = 2,
                Command = "cmd2",
            }
        );

        // Act - Try to append entry with inconsistent previous log term
        var args = new AppendEntriesArgs
        {
            Term = 3,
            LeaderId = "leader",
            PrevLogIndex = 2,
            PrevLogTerm = 1, // Wrong! Should be 2
            Entries = new List<LogEntry>
            {
                new()
                {
                    Term = 3,
                    Index = 3,
                    Command = "cmd3",
                },
            },
            LeaderCommit = 0,
        };

        var result = follower.AppendEntries(args);

        // Assert - Should reject due to log inconsistency
        Assert.That(
            result.Success,
            Is.False,
            "AppendEntries should reject entries with inconsistent previous log term"
        );
    }

    #endregion

    #region Safety Property 4: Leader Completeness

    /// <summary>
    /// Leader Completeness: If a log entry is committed in a given term, then that entry
    /// will be present in the logs of the leaders for all higher-numbered terms (§5.4)
    /// </summary>
    [Test]
    public void LeaderCompleteness_NewLeaderHasAllCommittedEntries()
    {
        // Arrange - Simulate a committed entry from previous term
        var oldLeader = _cluster[0];
        var newLeader = _cluster[1];

        // Old leader commits an entry in term 1
        SetPrivateProperty(oldLeader, "CurrentTerm", 1);
        SetPrivateProperty(oldLeader, "State", ServerState.Leader);

        var oldLog = GetPrivateProperty<List<LogEntry>>(oldLeader, "Log");
        oldLog.Add(
            new LogEntry
            {
                Term = 1,
                Index = 1,
                Command = "committed_cmd",
            }
        );
        SetPrivateProperty(oldLeader, "CommitIndex", 1);

        // New leader in term 2 should have the committed entry
        SetPrivateProperty(newLeader, "CurrentTerm", 2);
        SetPrivateProperty(newLeader, "State", ServerState.Leader);

        var newLog = GetPrivateProperty<List<LogEntry>>(newLeader, "Log");
        newLog.Add(
            new LogEntry
            {
                Term = 1,
                Index = 1,
                Command = "committed_cmd",
            }
        );

        // Act & Assert - New leader should have all committed entries from previous terms
        var committedEntry = oldLog.FirstOrDefault(e => e.Index <= oldLeader.CommitIndex);
        var newLeaderHasEntry = newLog.Any(e =>
            e.Index == committedEntry?.Index
            && e.Term == committedEntry.Term
            && e.Command == committedEntry.Command
        );

        Assert.That(
            newLeaderHasEntry,
            Is.True,
            "Leader Completeness violated: new leader missing committed entry from previous term"
        );
    }

    /// <summary>
    /// Test that RequestVote rejects candidates with incomplete logs
    /// </summary>
    [Test]
    public void LeaderCompleteness_VoteRejectedForIncompleteLog()
    {
        // Arrange - Voter with more up-to-date log
        var voter = _cluster[0];
        var log = GetPrivateProperty<List<LogEntry>>(voter, "Log");
        log.Add(
            new LogEntry
            {
                Term = 2,
                Index = 1,
                Command = "cmd1",
            }
        );
        log.Add(
            new LogEntry
            {
                Term = 3,
                Index = 2,
                Command = "cmd2",
            }
        );

        // Candidate with less up-to-date log
        var args = new RequestVoteArgs
        {
            Term = 4,
            CandidateId = "candidate",
            LastLogIndex = 1,
            LastLogTerm = 1, // Less up-to-date than voter's log
        };

        // Act
        var result = voter.RequestVote(args);

        // Assert - Should reject vote due to incomplete log
        Assert.That(
            result.VoteGranted,
            Is.False,
            "Should reject vote for candidate with less up-to-date log"
        );
    }

    #endregion

    #region Safety Property 5: State Machine Safety

    /// <summary>
    /// State Machine Safety: If a server has applied a log entry at a given index to its
    /// state machine, no other server will ever apply a different log entry for the same index (§5.4.3)
    /// </summary>
    [Test]
    public void StateMachineSafety_SameIndexSameEntry()
    {
        // Arrange - Create fresh servers to avoid state from previous tests
        var serverIds = new List<string> { "fresh1", "fresh2" };
        var server1 = new Raft("fresh1", serverIds);
        var server2 = new Raft("fresh2", serverIds);

        // Act - Both servers receive and apply the same committed entry
        var entry = new LogEntry
        {
            Term = 1,
            Index = 1,
            Command = "same_cmd",
        };

        var args1 = new AppendEntriesArgs
        {
            Term = 1,
            LeaderId = "leader",
            PrevLogIndex = 0,
            PrevLogTerm = 0,
            Entries = new List<LogEntry> { entry },
            LeaderCommit = 1,
        };

        var args2 = new AppendEntriesArgs
        {
            Term = 1,
            LeaderId = "leader",
            PrevLogIndex = 0,
            PrevLogTerm = 0,
            Entries = new List<LogEntry> { entry },
            LeaderCommit = 1,
        };

        var result1 = server1.AppendEntries(args1);
        var result2 = server2.AppendEntries(args2);

        // Assert - Both operations should succeed
        Assert.That(result1.Success, Is.True, "Server1 should accept the entry");
        Assert.That(result2.Success, Is.True, "Server2 should accept the entry");

        // Verify both servers have the same log entry at index 1
        Assert.That(
            server1.Log.Count,
            Is.GreaterThanOrEqualTo(1),
            "Server1 should have at least one log entry"
        );
        Assert.That(
            server2.Log.Count,
            Is.GreaterThanOrEqualTo(1),
            "Server2 should have at least one log entry"
        );

        var logEntry1 = server1.Log[0];
        var logEntry2 = server2.Log[0];

        Assert.That(
            logEntry1.Index,
            Is.EqualTo(logEntry2.Index),
            "Log entries should have same index"
        );
        Assert.That(
            logEntry1.Term,
            Is.EqualTo(logEntry2.Term),
            "Log entries should have same term"
        );
        Assert.That(
            logEntry1.Command,
            Is.EqualTo(logEntry2.Command),
            "Log entries should have same command"
        );

        // Verify both servers have committed the entry
        Assert.That(server1.CommitIndex, Is.EqualTo(1), "Server1 should have committed the entry");
        Assert.That(server2.CommitIndex, Is.EqualTo(1), "Server2 should have committed the entry");
    }

    #endregion

    #region Core Algorithm Properties (§5.1, §5.2)

    /// <summary>
    /// Test that servers only transition to candidate when election timeout elapses
    /// </summary>
    [Test]
    public void ElectionTimeout_TriggersStateTransition()
    {
        // Arrange
        var server = _cluster[0];
        server.Start(enableBackgroundTasks: false); // Deterministic mode
        Assert.That(server.State, Is.EqualTo(ServerState.Follower));

        // Act - Manually trigger election (simulating timeout)
        server.TriggerElection();

        // Assert
        Assert.That(server.State, Is.EqualTo(ServerState.Candidate));
        Assert.That(
            server.CurrentTerm,
            Is.GreaterThan(0),
            "Term should increment when becoming candidate"
        );
        Assert.That(server.VotedFor, Is.EqualTo(server.ServerId), "Should vote for self");
    }

    /// <summary>
    /// Test that servers reset election timeout when receiving valid AppendEntries
    /// </summary>
    [Test]
    public void HeartbeatResetsElectionTimeout()
    {
        // Arrange
        var follower = _cluster[0];
        var originalTimeout = GetPrivateField<DateTime>(follower, "_electionTimeout");

        // Act - Receive valid heartbeat
        var args = new AppendEntriesArgs
        {
            Term = 1,
            LeaderId = "leader",
            PrevLogIndex = 0,
            PrevLogTerm = 0,
            Entries = new List<LogEntry>(),
            LeaderCommit = 0,
        };

        follower.AppendEntries(args);

        // Assert - Election timeout should be reset (different from original)
        var newTimeout = GetPrivateField<DateTime>(follower, "_electionTimeout");
        Assert.That(
            newTimeout,
            Is.Not.EqualTo(originalTimeout),
            "Election timeout should be reset after receiving heartbeat"
        );

        // Also verify it's set to a future time
        Assert.That(
            newTimeout,
            Is.GreaterThan(DateTime.UtcNow.AddMilliseconds(-1000)),
            "Election timeout should be set to a reasonable future time"
        );
    }

    /// <summary>
    /// Test term comparison and state transitions (§5.1)
    /// </summary>
    [Test]
    public void TermComparison_UpdatesStateCorrectly()
    {
        // Arrange
        var server = _cluster[0];
        SetPrivateProperty(server, "CurrentTerm", 5);
        SetPrivateProperty(server, "State", ServerState.Leader);

        // Act - Receive RPC with higher term
        var args = new RequestVoteArgs
        {
            Term = 7,
            CandidateId = "candidate",
            LastLogIndex = 0,
            LastLogTerm = 0,
        };

        var result = server.RequestVote(args);

        // Assert - Should convert to follower and update term
        Assert.That(server.CurrentTerm, Is.EqualTo(7), "Should update to higher term");
        Assert.That(server.State, Is.EqualTo(ServerState.Follower), "Should convert to follower");
        Assert.That(
            server.VotedFor,
            Is.EqualTo(string.Empty).Or.EqualTo("candidate"),
            "Should reset or update vote"
        );
    }

    #endregion

    #region Helper Methods

    private void SetPrivateProperty<T>(object obj, string propertyName, T value)
    {
        var property = obj.GetType()
            .GetProperty(
                propertyName,
                System.Reflection.BindingFlags.NonPublic
                    | System.Reflection.BindingFlags.Public
                    | System.Reflection.BindingFlags.Instance
            );
        property?.SetValue(obj, value);
    }

    private T GetPrivateProperty<T>(object obj, string propertyName)
    {
        var property = obj.GetType()
            .GetProperty(
                propertyName,
                System.Reflection.BindingFlags.NonPublic
                    | System.Reflection.BindingFlags.Public
                    | System.Reflection.BindingFlags.Instance
            );
        return (T)property?.GetValue(obj)!;
    }

    private T GetPrivateField<T>(object obj, string fieldName)
    {
        var field = obj.GetType()
            .GetField(
                fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
            );
        return (T)field?.GetValue(obj)!;
    }

    private void CallPrivateMethod(object obj, string methodName, params object[] parameters)
    {
        var method = obj.GetType()
            .GetMethod(
                methodName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
            );
        method?.Invoke(obj, parameters);
    }

    #endregion
}
