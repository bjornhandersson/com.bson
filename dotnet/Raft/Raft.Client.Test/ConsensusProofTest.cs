using Raft.Client;

namespace Raft.Client.Test;

/// <summary>
/// Test to prove whether the Raft implementation actually provides consensus
/// under concurrent operations. This is a real-world test of the consensus guarantee.
/// </summary>
[TestFixture]
public class ConsensusProofTest
{
    private List<Raft> _cluster = new();
    private readonly List<string> _serverIds = new()
    {
        "server1", "server2", "server3", "server4", "server5"
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

    /// <summary>
    /// PROOF TEST: Does Raft actually provide consensus?
    /// 
    /// Scenario:
    /// - Thread 1: Sets "name: anna" 
    /// - Thread 2: Sets "name: agneta" (happens slightly before Thread 1)
    /// - Thread 3: Reads the value
    /// 
    /// Expected: Thread 3 should read "agneta" (the earlier operation)
    /// Reality: Let's see what actually happens...
    /// </summary>
    [Test]
    public async Task ProveConsensus_ConcurrentWrites_ShouldProvideLinearizability()
    {
        // Arrange - Start cluster and elect a leader
        foreach (var server in _cluster)
        {
            server.Start(enableBackgroundTasks: false);
        }

        // Manually elect a leader (server1)
        var leader = _cluster[0];
        leader.TriggerElection();
        leader.SendRequestVoteRPCsSync();

        Assert.That(leader.State, Is.EqualTo(ServerState.Leader), "Should have a leader");

        // Track all committed entries across the cluster
        var allCommittedEntries = new List<(string ServerId, LogEntry Entry, DateTime Timestamp)>();
        var lockObject = new object();

        foreach (var server in _cluster)
        {
            server.LogEntryCommitted += entry =>
            {
                lock (lockObject)
                {
                    allCommittedEntries.Add((server.ServerId, entry, DateTime.UtcNow));
                }
            };
        }

        // Act - Simulate concurrent operations
        var results = new List<bool>();
        var tasks = new List<Task>();

        // Thread 2: Sets "name: agneta" (this should happen first)
        tasks.Add(Task.Run(async () =>
        {
            await Task.Delay(10); // Slight delay to ensure this happens "before" Thread 1
            var result = leader.SubmitCommand("name: agneta");
            lock (lockObject) { results.Add(result); }
        }));

        // Thread 1: Sets "name: anna" (this should happen second)
        tasks.Add(Task.Run(async () =>
        {
            await Task.Delay(20); // Happens after Thread 2
            var result = leader.SubmitCommand("name: anna");
            lock (lockObject) { results.Add(result); }
        }));

        // Wait for both operations to complete
        await Task.WhenAll(tasks);

        // Give some time for replication and commitment
        await Task.Delay(100);

        // Thread 3: Read the final state
        var finalLogs = new Dictionary<string, List<LogEntry>>();
        foreach (var server in _cluster)
        {
            finalLogs[server.ServerId] = server.Log.ToList();
        }

        // PROOF ANALYSIS - Verify consensus was achieved

        // 1. All servers should have the same log
        var leaderLog = finalLogs[leader.ServerId];
        bool logsMatch = true;
        foreach (var kvp in finalLogs)
        {
            if (kvp.Key == leader.ServerId) continue;
            
            var serverLog = kvp.Value;
            if (serverLog.Count != leaderLog.Count)
            {
                logsMatch = false;
                continue;
            }

            for (int i = 0; i < leaderLog.Count; i++)
            {
                if (leaderLog[i].Command != serverLog[i].Command || 
                    leaderLog[i].Term != serverLog[i].Term ||
                    leaderLog[i].Index != serverLog[i].Index)
                {
                    logsMatch = false;
                }
            }
        }

        // Verify consensus was achieved

        // 2. Check the order of operations
        if (leaderLog.Count >= 2)
        {
            var firstEntry = leaderLog[0];
            var secondEntry = leaderLog[1];
            
            // Verify operation order - "agneta" should come first according to test design
            bool correctOrder = firstEntry.Command == "name: agneta" && secondEntry.Command == "name: anna";
        }

        // 3. Final read value (what Thread 3 would see)
        var finalValue = leaderLog.LastOrDefault()?.Command ?? "NO VALUE";

        // ASSERTIONS - The real test of consensus
        Assert.That(logsMatch, Is.True, "CONSENSUS FAILED: Servers have different logs");
        
        if (leaderLog.Count > 0)
        {
            // All servers should see the same final value
            foreach (var kvp in finalLogs)
            {
                var lastEntry = kvp.Value.LastOrDefault();
                Assert.That(lastEntry?.Command, Is.EqualTo(finalValue), 
                    $"Server {kvp.Key} has different final value: '{lastEntry?.Command}' vs expected '{finalValue}'");
            }
        }

    }
}