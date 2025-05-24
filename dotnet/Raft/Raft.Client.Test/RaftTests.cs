using Raft.Client;

namespace Raft.Client.Test;

[TestFixture]
public class RaftTests
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

        // Create a 5-server cluster with shared registry
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

    [Test]
    public void Constructor_ValidParameters_InitializesCorrectly()
    {
        // Arrange & Act
        var raft = new Raft("test-server", new List<string> { "test-server", "other-server" });

        // Assert
        Assert.That(raft.ServerId, Is.EqualTo("test-server"));
        Assert.That(raft.State, Is.EqualTo(ServerState.Follower));
        Assert.That(raft.CurrentTerm, Is.EqualTo(0));
        Assert.That(raft.VotedFor, Is.EqualTo(string.Empty));
        Assert.That(raft.CommitIndex, Is.EqualTo(0));
        Assert.That(raft.LastApplied, Is.EqualTo(0));
        Assert.That(raft.GetLog().Count, Is.EqualTo(0));
    }

    [Test]
    public void Constructor_ServerIdNotInCluster_ThrowsException()
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentException>(() =>
            new Raft("missing-server", new List<string> { "server1", "server2" })
        );
    }

    [Test]
    public void Constructor_NullServerId_ThrowsException()
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentNullException>(() => new Raft(null!, new List<string> { "server1" }));
    }

    [Test]
    public void Constructor_NullClusterMembers_ThrowsException()
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentNullException>(() => new Raft("server1", null!));
    }

    [Test]
    public void RequestVote_LowerTerm_ReturnsFalse()
    {
        // Arrange
        var raft = _cluster[0];
        raft.CurrentTerm = 5;

        var args = new RequestVoteArgs
        {
            Term = 3,
            CandidateId = "candidate",
            LastLogIndex = 0,
            LastLogTerm = 0,
        };

        // Act
        var result = raft.RequestVote(args);

        // Assert
        Assert.That(result.VoteGranted, Is.False);
        Assert.That(result.Term, Is.EqualTo(5));
    }

    [Test]
    public void RequestVote_HigherTerm_UpdatesTermAndConvertsToFollower()
    {
        // Arrange
        var raft = _cluster[0];
        var args = new RequestVoteArgs
        {
            Term = 5,
            CandidateId = "candidate",
            LastLogIndex = 0,
            LastLogTerm = 0,
        };

        // Act
        var result = raft.RequestVote(args);

        // Assert
        Assert.That(result.Term, Is.EqualTo(5));
        Assert.That(raft.CurrentTerm, Is.EqualTo(5));
        Assert.That(raft.State, Is.EqualTo(ServerState.Follower));
    }

    [Test]
    public void RequestVote_ValidRequest_GrantsVote()
    {
        // Arrange
        var raft = _cluster[0];
        var args = new RequestVoteArgs
        {
            Term = 1,
            CandidateId = "candidate",
            LastLogIndex = 0,
            LastLogTerm = 0,
        };

        // Act
        var result = raft.RequestVote(args);

        // Assert
        Assert.That(result.VoteGranted, Is.True);
        Assert.That(result.Term, Is.EqualTo(1));
        Assert.That(raft.VotedFor, Is.EqualTo("candidate"));
    }

    [Test]
    public void RequestVote_AlreadyVotedForDifferentCandidate_DeniesVote()
    {
        // Arrange
        var raft = _cluster[0];

        // First vote
        var firstArgs = new RequestVoteArgs
        {
            Term = 1,
            CandidateId = "candidate1",
            LastLogIndex = 0,
            LastLogTerm = 0,
        };
        raft.RequestVote(firstArgs);

        // Second vote request
        var secondArgs = new RequestVoteArgs
        {
            Term = 1,
            CandidateId = "candidate2",
            LastLogIndex = 0,
            LastLogTerm = 0,
        };

        // Act
        var result = raft.RequestVote(secondArgs);

        // Assert
        Assert.That(result.VoteGranted, Is.False);
        Assert.That(raft.VotedFor, Is.EqualTo("candidate1"));
    }

    [Test]
    public void AppendEntries_LowerTerm_ReturnsFalse()
    {
        // Arrange
        var raft = _cluster[0];
        raft.CurrentTerm = 5;

        var args = new AppendEntriesArgs
        {
            Term = 3,
            LeaderId = "leader",
            PrevLogIndex = 0,
            PrevLogTerm = 0,
            Entries = new List<LogEntry>(),
            LeaderCommit = 0,
        };

        // Act
        var result = raft.AppendEntries(args);

        // Assert
        Assert.That(result.Success, Is.False);
        Assert.That(result.Term, Is.EqualTo(5));
    }

    [Test]
    public void AppendEntries_HigherTerm_UpdatesTermAndConvertsToFollower()
    {
        // Arrange
        var raft = _cluster[0];
        var args = new AppendEntriesArgs
        {
            Term = 5,
            LeaderId = "leader",
            PrevLogIndex = 0,
            PrevLogTerm = 0,
            Entries = new List<LogEntry>(),
            LeaderCommit = 0,
        };

        // Act
        var result = raft.AppendEntries(args);

        // Assert
        Assert.That(result.Term, Is.EqualTo(5));
        Assert.That(raft.CurrentTerm, Is.EqualTo(5));
        Assert.That(raft.State, Is.EqualTo(ServerState.Follower));
        Assert.That(raft.CurrentLeader, Is.EqualTo("leader"));
    }

    [Test]
    public void AppendEntries_ValidHeartbeat_ReturnsSuccess()
    {
        // Arrange
        var raft = _cluster[0];
        var args = new AppendEntriesArgs
        {
            Term = 1,
            LeaderId = "leader",
            PrevLogIndex = 0,
            PrevLogTerm = 0,
            Entries = new List<LogEntry>(),
            LeaderCommit = 0,
        };

        // Act
        var result = raft.AppendEntries(args);

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.Term, Is.EqualTo(1));
        Assert.That(raft.CurrentLeader, Is.EqualTo("leader"));
    }

    [Test]
    public void AppendEntries_PrevLogIndexMismatch_ReturnsFalse()
    {
        // Arrange
        var raft = _cluster[0];

        // Add a log entry first
        raft.Log.Add(
            new LogEntry
            {
                Term = 1,
                Index = 1,
                Command = "test",
            }
        );

        var args = new AppendEntriesArgs
        {
            Term = 2,
            LeaderId = "leader",
            PrevLogIndex = 1,
            PrevLogTerm = 2, // Wrong term
            Entries = new List<LogEntry>
            {
                new()
                {
                    Term = 2,
                    Index = 2,
                    Command = "new-command",
                },
            },
            LeaderCommit = 0,
        };

        // Act
        var result = raft.AppendEntries(args);

        // Assert
        Assert.That(result.Success, Is.False);
    }

    [Test]
    public void AppendEntries_ValidNewEntry_AppendsToLog()
    {
        // Arrange
        var raft = _cluster[0];
        var args = new AppendEntriesArgs
        {
            Term = 1,
            LeaderId = "leader",
            PrevLogIndex = 0,
            PrevLogTerm = 0,
            Entries = new List<LogEntry>
            {
                new()
                {
                    Term = 1,
                    Index = 1,
                    Command = "test-command",
                },
            },
            LeaderCommit = 0,
        };

        // Act
        var result = raft.AppendEntries(args);

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(raft.GetLog().Count, Is.EqualTo(1));
        Assert.That(raft.GetLog()[0].Command, Is.EqualTo("test-command"));
        Assert.That(raft.GetLog()[0].Term, Is.EqualTo(1));
    }

    [Test]
    public void AppendEntries_ConflictingEntry_RemovesConflictingAndSubsequentEntries()
    {
        // Arrange
        var raft = _cluster[0];

        // Add some existing entries directly to the log (using internal access)
        raft.Log.Add(
            new LogEntry
            {
                Term = 1,
                Index = 1,
                Command = "cmd1",
            }
        );
        raft.Log.Add(
            new LogEntry
            {
                Term = 1,
                Index = 2,
                Command = "cmd2",
            }
        );
        raft.Log.Add(
            new LogEntry
            {
                Term = 2,
                Index = 3,
                Command = "cmd3",
            }
        );

        var args = new AppendEntriesArgs
        {
            Term = 3,
            LeaderId = "leader",
            PrevLogIndex = 1,
            PrevLogTerm = 1,
            Entries = new List<LogEntry>
            {
                new()
                {
                    Term = 3,
                    Index = 2,
                    Command = "new-cmd2",
                }, // Conflicts with existing entry at index 2
                new()
                {
                    Term = 3,
                    Index = 3,
                    Command = "new-cmd3",
                },
            },
            LeaderCommit = 0,
        };

        // Act
        var result = raft.AppendEntries(args);

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(raft.GetLog().Count, Is.EqualTo(3));
        Assert.That(raft.GetLog()[1].Command, Is.EqualTo("new-cmd2"));
        Assert.That(raft.GetLog()[1].Term, Is.EqualTo(3));
        Assert.That(raft.GetLog()[2].Command, Is.EqualTo("new-cmd3"));
        Assert.That(raft.GetLog()[2].Term, Is.EqualTo(3));
    }

    [Test]
    public void AppendEntries_UpdatesCommitIndex_AppliesEntries()
    {
        // Arrange
        var raft = _cluster[0];
        var appliedEntries = new List<LogEntry>();
        raft.LogEntryCommitted += entry => appliedEntries.Add(entry);

        var args = new AppendEntriesArgs
        {
            Term = 1,
            LeaderId = "leader",
            PrevLogIndex = 0,
            PrevLogTerm = 0,
            Entries = new List<LogEntry>
            {
                new()
                {
                    Term = 1,
                    Index = 1,
                    Command = "cmd1",
                },
                new()
                {
                    Term = 1,
                    Index = 2,
                    Command = "cmd2",
                },
            },
            LeaderCommit = 2,
        };

        // Act
        var result = raft.AppendEntries(args);

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(raft.CommitIndex, Is.EqualTo(2));
        Assert.That(raft.LastApplied, Is.EqualTo(2));
        Assert.That(appliedEntries.Count, Is.EqualTo(2));
        Assert.That(appliedEntries[0].Command, Is.EqualTo("cmd1"));
        Assert.That(appliedEntries[1].Command, Is.EqualTo("cmd2"));
    }

    [Test]
    public void SubmitCommand_AsFollower_ReturnsFalse()
    {
        // Arrange
        var raft = _cluster[0];
        Assert.That(raft.State, Is.EqualTo(ServerState.Follower));

        // Act
        var result = raft.SubmitCommand("test-command");

        // Assert
        Assert.That(result, Is.False);
        Assert.That(raft.GetLog().Count, Is.EqualTo(0));
    }

    [Test]
    public void SubmitCommand_AsLeader_AddsToLogAndReturnsTrue()
    {
        // Arrange
        var raft = _cluster[0];

        // Manually set as leader for testing
        raft.State = ServerState.Leader;
        raft.CurrentTerm = 1;

        // Act
        var result = raft.SubmitCommand("test-command");

        // Assert
        Assert.That(result, Is.True);
        Assert.That(raft.GetLog().Count, Is.EqualTo(1));
        Assert.That(raft.GetLog()[0].Command, Is.EqualTo("test-command"));
        Assert.That(raft.GetLog()[0].Term, Is.EqualTo(1));
        Assert.That(raft.GetLog()[0].Index, Is.EqualTo(1));
    }

    [Test]
    public void GetStatus_ReturnsCorrectInformation()
    {
        // Arrange
        var raft = _cluster[0];

        // Act
        var status = raft.GetStatus();

        // Assert
        Assert.That(status, Is.Not.Null);

        // Use dynamic to access the anonymous object properties
        dynamic dynamicStatus = status;
        
        Assert.That(dynamicStatus.ServerId, Is.EqualTo("server1"));
        Assert.That(dynamicStatus.State, Is.EqualTo("Follower"));
        Assert.That(dynamicStatus.CurrentTerm, Is.EqualTo(0));
        Assert.That(dynamicStatus.ClusterSize, Is.EqualTo(5));
    }

    [Test]
    public void StateChanged_Event_FiresWhenStateChanges()
    {
        // Arrange
        var raft = _cluster[0];
        var stateChanges = new List<(ServerState oldState, ServerState newState)>();
        raft.StateChanged += (oldState, newState) => stateChanges.Add((oldState, newState));

        // Act - Simulate state change by calling RequestVote with higher term
        var args = new RequestVoteArgs
        {
            Term = 5,
            CandidateId = "candidate",
            LastLogIndex = 0,
            LastLogTerm = 0,
        };
        raft.RequestVote(args);

        // Assert
        Assert.That(stateChanges.Count, Is.EqualTo(1));
        Assert.That(stateChanges[0].oldState, Is.EqualTo(ServerState.Follower));
        Assert.That(stateChanges[0].newState, Is.EqualTo(ServerState.Follower));
    }

    [Test]
    public void LeaderChanged_Event_FiresWhenLeaderChanges()
    {
        // Arrange
        var raft = _cluster[0];
        var leaderChanges = new List<string?>();
        raft.LeaderChanged += leaderId => leaderChanges.Add(leaderId);

        // Act - Simulate leader change through AppendEntries
        var args = new AppendEntriesArgs
        {
            Term = 1,
            LeaderId = "new-leader",
            PrevLogIndex = 0,
            PrevLogTerm = 0,
            Entries = new List<LogEntry>(),
            LeaderCommit = 0,
        };
        raft.AppendEntries(args);

        // Assert
        Assert.That(leaderChanges.Count, Is.EqualTo(1));
        Assert.That(leaderChanges[0], Is.EqualTo("new-leader"));
    }

    [Test]
    public void LogEntryCommitted_Event_FiresWhenEntryIsCommitted()
    {
        // Arrange
        var raft = _cluster[0];
        var committedEntries = new List<LogEntry>();
        raft.LogEntryCommitted += entry => committedEntries.Add(entry);

        // Act - Add entry and commit it
        var args = new AppendEntriesArgs
        {
            Term = 1,
            LeaderId = "leader",
            PrevLogIndex = 0,
            PrevLogTerm = 0,
            Entries = new List<LogEntry>
            {
                new()
                {
                    Term = 1,
                    Index = 1,
                    Command = "test-command",
                },
            },
            LeaderCommit = 1,
        };
        raft.AppendEntries(args);

        // Assert
        Assert.That(committedEntries.Count, Is.EqualTo(1));
        Assert.That(committedEntries[0].Command, Is.EqualTo("test-command"));
    }

    [Test]
    public void MultipleAppendEntries_MaintainsLogConsistency()
    {
        // Arrange
        var raft = _cluster[0];

        // Act - Send multiple AppendEntries in sequence
        var args1 = new AppendEntriesArgs
        {
            Term = 1,
            LeaderId = "leader",
            PrevLogIndex = 0,
            PrevLogTerm = 0,
            Entries = new List<LogEntry>
            {
                new()
                {
                    Term = 1,
                    Index = 1,
                    Command = "cmd1",
                },
            },
            LeaderCommit = 0,
        };
        var result1 = raft.AppendEntries(args1);

        var args2 = new AppendEntriesArgs
        {
            Term = 1,
            LeaderId = "leader",
            PrevLogIndex = 1,
            PrevLogTerm = 1,
            Entries = new List<LogEntry>
            {
                new()
                {
                    Term = 1,
                    Index = 2,
                    Command = "cmd2",
                },
            },
            LeaderCommit = 0,
        };
        var result2 = raft.AppendEntries(args2);

        // Assert
        Assert.That(result1.Success, Is.True);
        Assert.That(result2.Success, Is.True);
        Assert.That(raft.GetLog().Count, Is.EqualTo(2));
        Assert.That(raft.GetLog()[0].Command, Is.EqualTo("cmd1"));
        Assert.That(raft.GetLog()[1].Command, Is.EqualTo("cmd2"));
    }

    [Test]
    public void Start_InitializesAsFollower()
    {
        // Arrange
        var raft = _cluster[0];

        // Act
        raft.Start();

        // Assert
        Assert.That(raft.State, Is.EqualTo(ServerState.Follower));
    }
}
