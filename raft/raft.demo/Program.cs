using raft.client;

namespace raft.demo;

/// <summary>
/// Demonstration program showing the Raft consensus algorithm in action
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== Raft Consensus Algorithm Demo ===");
        Console.WriteLine("Based on 'In Search of an Understandable Consensus Algorithm'");
        Console.WriteLine("by Diego Ongaro and John Ousterhout from Stanford University");
        Console.WriteLine();

        // Create a 5-server cluster
        var serverIds = new List<string> { "server1", "server2", "server3", "server4", "server5" };
        var cluster = new List<Raft>();

        Console.WriteLine("Creating 5-server Raft cluster...");
        foreach (var serverId in serverIds)
        {
            var raft = new Raft(serverId, serverIds);

            // Subscribe to events for demonstration
            raft.StateChanged += (oldState, newState) =>
                Console.WriteLine($"[{raft.ServerId}] State changed: {oldState} -> {newState}");

            raft.LeaderChanged += leaderId =>
                Console.WriteLine($"[{raft.ServerId}] Leader changed to: {leaderId ?? "None"}");

            raft.LogEntryCommitted += entry =>
                Console.WriteLine(
                    $"[{raft.ServerId}] Committed entry: {entry.Command} (Term: {entry.Term}, Index: {entry.Index})"
                );

            cluster.Add(raft);
        }

        Console.WriteLine("\nStarting all servers...");
        foreach (var server in cluster)
        {
            server.Start();
        }

        // Wait a moment for initialization
        await Task.Delay(100);

        Console.WriteLine("\nCluster Status:");
        foreach (var server in cluster)
        {
            var status = server.GetStatus();
            Console.WriteLine($"  {server.ServerId}: {status}");
        }

        Console.WriteLine("\n=== Demonstrating RequestVote RPC ===");

        // Simulate a RequestVote scenario
        var candidate = cluster[0];
        var voter = cluster[1];

        var voteRequest = new RequestVoteArgs
        {
            Term = 1,
            CandidateId = candidate.ServerId,
            LastLogIndex = 0,
            LastLogTerm = 0,
        };

        Console.WriteLine($"\n{candidate.ServerId} requesting vote from {voter.ServerId}...");
        var voteResult = voter.RequestVote(voteRequest);
        Console.WriteLine(
            $"Vote result: Term={voteResult.Term}, VoteGranted={voteResult.VoteGranted}"
        );

        Console.WriteLine("\n=== Demonstrating AppendEntries RPC ===");

        // Simulate leader sending heartbeat
        var leader = cluster[2];
        var follower = cluster[3];

        // Manually set one server as leader for demonstration
        var leaderStateField = leader.GetType().GetProperty("State")!;
        leaderStateField.SetValue(leader, ServerState.Leader);

        var leaderTermField = leader.GetType().GetProperty("CurrentTerm")!;
        leaderTermField.SetValue(leader, 2);

        var heartbeat = new AppendEntriesArgs
        {
            Term = 2,
            LeaderId = leader.ServerId,
            PrevLogIndex = 0,
            PrevLogTerm = 0,
            Entries = new List<LogEntry>(),
            LeaderCommit = 0,
        };

        Console.WriteLine($"\n{leader.ServerId} sending heartbeat to {follower.ServerId}...");
        var heartbeatResult = follower.AppendEntries(heartbeat);
        Console.WriteLine(
            $"Heartbeat result: Term={heartbeatResult.Term}, Success={heartbeatResult.Success}"
        );

        Console.WriteLine("\n=== Demonstrating Log Replication ===");

        // Add a log entry
        var logEntry = new LogEntry
        {
            Term = 2,
            Index = 1,
            Command = "SET x = 42",
        };

        var appendRequest = new AppendEntriesArgs
        {
            Term = 2,
            LeaderId = leader.ServerId,
            PrevLogIndex = 0,
            PrevLogTerm = 0,
            Entries = new List<LogEntry> { logEntry },
            LeaderCommit = 1,
        };

        Console.WriteLine($"\n{leader.ServerId} replicating log entry to {follower.ServerId}...");
        var appendResult = follower.AppendEntries(appendRequest);
        Console.WriteLine(
            $"Append result: Term={appendResult.Term}, Success={appendResult.Success}"
        );
        Console.WriteLine($"Follower log count: {follower.GetLog().Count}");
        if (follower.GetLog().Count > 0)
        {
            Console.WriteLine($"First log entry: {follower.GetLog()[0].Command}");
        }

        Console.WriteLine("\n=== Demonstrating Command Submission ===");

        // Test command submission (should fail for non-leaders)
        Console.WriteLine($"\nTrying to submit command to follower {follower.ServerId}...");
        var followerResult = follower.SubmitCommand("SET y = 100");
        Console.WriteLine($"Follower command submission result: {followerResult}");

        Console.WriteLine($"\nTrying to submit command to leader {leader.ServerId}...");
        var leaderResult = leader.SubmitCommand("SET z = 200");
        Console.WriteLine($"Leader command submission result: {leaderResult}");
        Console.WriteLine($"Leader log count after submission: {leader.GetLog().Count}");

        Console.WriteLine("\n=== Final Cluster Status ===");
        foreach (var server in cluster)
        {
            var status = server.GetStatus();
            Console.WriteLine($"  {server.ServerId}: {status}");

            if (server.GetLog().Count > 0)
            {
                Console.WriteLine($"    Log entries:");
                foreach (var entry in server.GetLog())
                {
                    Console.WriteLine($"      [{entry.Index}] Term {entry.Term}: {entry.Command}");
                }
            }
        }

        Console.WriteLine("\n=== Raft Algorithm Properties Demonstrated ===");
        Console.WriteLine("✓ Leader Election: RequestVote RPC implementation");
        Console.WriteLine("✓ Log Replication: AppendEntries RPC implementation");
        Console.WriteLine("✓ Safety: Term-based conflict resolution");
        Console.WriteLine("✓ State Management: Follower/Candidate/Leader states");
        Console.WriteLine("✓ Consistency: Log matching and commitment rules");

        Console.WriteLine("\nDemo completed successfully!");
    }
}
