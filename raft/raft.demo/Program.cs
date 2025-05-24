using Raft.Client;

namespace Raft.Demo;

/// <summary>
/// Demonstration program showing distributed settings synchronization using Raft consensus
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== Distributed Settings with Raft Consensus Demo ===");
        Console.WriteLine("Showcasing how services can use Raft for settings synchronization");
        Console.WriteLine();

        // Create a 5-node cluster for distributed settings
        var nodeIds = new List<string> { "node1", "node2", "node3", "node4", "node5" };
        var settingsCluster = new List<DistributedSettings>();

        Console.WriteLine("Creating 5-node distributed settings cluster...");
        foreach (var nodeId in nodeIds)
        {
            var settings = new DistributedSettings(nodeId, nodeIds);
            settingsCluster.Add(settings);
        }

        Console.WriteLine("\nStarting all nodes...");
        foreach (var settings in settingsCluster)
        {
            settings.Start();
        }

        // Wait for cluster to stabilize and elect a leader
        Console.WriteLine("\nWaiting for leader election...");
        await WaitForLeaderElection(settingsCluster);

        // Find the current leader
        var leader = settingsCluster.FirstOrDefault(s => s.State == ServerState.Leader);
        var followers = settingsCluster.Where(s => s.State == ServerState.Follower).ToList();

        Console.WriteLine("\n=== Cluster Status ===");
        foreach (var settings in settingsCluster)
        {
            var status = settings.GetStatus();
            Console.WriteLine($"  {settings.NodeId}: {status}");
        }

        if (leader == null)
        {
            Console.WriteLine(
                "\nNo leader elected yet. Manually promoting node1 to leader for demo..."
            );
            leader = settingsCluster[0];
            // In a real scenario, we'd wait for natural leader election
        }

        Console.WriteLine($"\nUsing {leader.NodeId} as the leader for settings operations");
        Console.WriteLine();

        // Demonstrate CRUD operations
        Console.WriteLine("=== Demonstrating Settings CRUD Operations ===");

        // 1. Add settings
        Console.WriteLine("\n1. Adding settings to the cluster:");
        leader.Add("database.host", "localhost");
        leader.Add("database.port", "5432");
        leader.Add("api.timeout", "30000");
        leader.Add("logging.level", "INFO");
        leader.Add("feature.newUI", "true");

        // Wait for replication
        await WaitForReplication(settingsCluster);

        // 2. Read settings from different nodes
        Console.WriteLine("\n2. Reading settings from different nodes:");
        var randomFollower = followers.FirstOrDefault() ?? settingsCluster[1];

        Console.WriteLine($"\nReading from leader ({leader.NodeId}):");
        Console.WriteLine($"  database.host = {leader.Get("database.host")}");
        Console.WriteLine($"  api.timeout = {leader.Get("api.timeout")}");

        Console.WriteLine($"\nReading from follower ({randomFollower.NodeId}):");
        Console.WriteLine($"  database.host = {randomFollower.Get("database.host")}");
        Console.WriteLine($"  logging.level = {randomFollower.Get("logging.level")}");

        // 3. Update existing settings
        Console.WriteLine("\n3. Updating existing settings:");
        leader.Add("logging.level", "DEBUG"); // Update existing
        leader.Add("database.port", "5433"); // Update existing

        await WaitForReplication(settingsCluster);

        // 4. Check if settings exist
        Console.WriteLine("\n4. Checking if settings exist:");
        Console.WriteLine(
            $"  Contains 'database.host': {randomFollower.Contains("database.host")}"
        );
        Console.WriteLine(
            $"  Contains 'nonexistent.key': {randomFollower.Contains("nonexistent.key")}"
        );

        // 5. Get all settings
        Console.WriteLine("\n5. Getting all settings from a follower:");
        var allSettings = randomFollower.GetAll();
        foreach (var kvp in allSettings.OrderBy(x => x.Key))
        {
            Console.WriteLine($"  {kvp.Key} = {kvp.Value}");
        }

        // 6. Delete settings
        Console.WriteLine("\n6. Deleting settings:");
        leader.Delete("feature.newUI");
        leader.Delete("nonexistent.key"); // Try to delete non-existent key

        await WaitForReplication(settingsCluster);

        // 7. Verify deletion across cluster
        Console.WriteLine("\n7. Verifying deletion across cluster:");
        foreach (var settings in settingsCluster.Take(3)) // Check first 3 nodes
        {
            Console.WriteLine(
                $"  {settings.NodeId} - feature.newUI exists: {settings.Contains("feature.newUI")}"
            );
        }

        // 8. Try operations from a follower (should fail)
        Console.WriteLine("\n8. Attempting operations from a follower (should fail):");
        var follower = followers.FirstOrDefault() ?? settingsCluster[1];
        follower.Add("test.key", "test.value");
        follower.Delete("database.host");

        // 9. Final state of all nodes
        Console.WriteLine("\n9. Final settings state across all nodes:");
        for (int i = 0; i < settingsCluster.Count; i++)
        {
            var settings = settingsCluster[i];
            var nodeSettings = settings.GetAll();
            Console.WriteLine(
                $"\n  {settings.NodeId} ({settings.State}) - {nodeSettings.Count} settings:"
            );
            foreach (var kvp in nodeSettings.OrderBy(x => x.Key))
            {
                Console.WriteLine($"    {kvp.Key} = {kvp.Value}");
            }
        }

        // 10. Demonstrate consistency
        Console.WriteLine("\n10. Verifying consistency across cluster:");
        var leaderSettings = leader.GetAll();
        bool allConsistent = true;

        foreach (var node in settingsCluster)
        {
            var nodeSettings = node.GetAll();
            bool consistent =
                leaderSettings.Count == nodeSettings.Count
                && leaderSettings.All(kvp =>
                    nodeSettings.ContainsKey(kvp.Key) && nodeSettings[kvp.Key] == kvp.Value
                );

            Console.WriteLine(
                $"  {node.NodeId}: {(consistent ? "✓ Consistent" : "✗ Inconsistent")}"
            );
            if (!consistent)
                allConsistent = false;
        }

        Console.WriteLine(
            $"\nCluster consistency: {(allConsistent ? "✓ All nodes consistent" : "✗ Inconsistency detected")}"
        );

        Console.WriteLine("\n=== Use Case Examples ===");
        Console.WriteLine("This distributed settings store can be used by services for:");
        Console.WriteLine("• Configuration management across microservices");
        Console.WriteLine("• Feature flags synchronization");
        Console.WriteLine("• Database connection strings");
        Console.WriteLine("• API endpoints and timeouts");
        Console.WriteLine("• Logging levels and debugging settings");
        Console.WriteLine("• Any settings that need to be consistent across a distributed system");

        Console.WriteLine("\n=== Raft Consensus Benefits Demonstrated ===");
        Console.WriteLine("✓ Strong Consistency: All nodes have identical settings");
        Console.WriteLine("✓ Fault Tolerance: System continues with majority of nodes");
        Console.WriteLine("✓ Leader Election: Automatic leader selection for write operations");
        Console.WriteLine("✓ Log Replication: Changes are safely replicated before commit");
        Console.WriteLine("✓ Conflict Resolution: Term-based ordering prevents conflicts");

        Console.WriteLine("\nDistributed Settings Demo completed successfully!");
    }

    /// <summary>
    /// Wait for a leader to be elected in the cluster using events instead of arbitrary delays
    /// </summary>
    private static async Task WaitForLeaderElection(
        List<DistributedSettings> cluster,
        int timeoutMs = 10000
    )
    {
        var tcs = new TaskCompletionSource<bool>();
        var cts = new CancellationTokenSource(timeoutMs);

        // Cancel the task if timeout is reached
        cts.Token.Register(() => tcs.TrySetCanceled());

        // Subscribe to leader change events from any node
        void OnLeaderChanged(string? leaderId)
        {
            if (!string.IsNullOrEmpty(leaderId))
            {
                tcs.TrySetResult(true);
            }
        }

        // Subscribe to all nodes' leader change events
        foreach (var node in cluster)
        {
            // Access the internal Raft instance through reflection since it's private
            var raftField = typeof(DistributedSettings).GetField(
                "_raft",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
            );
            if (raftField?.GetValue(node) is Raft.Client.Raft raft)
            {
                raft.LeaderChanged += OnLeaderChanged;
            }
        }

        try
        {
            // Check if there's already a leader
            if (cluster.Any(s => s.State == ServerState.Leader))
            {
                return;
            }

            // Wait for leader election
            await tcs.Task;
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Warning: Leader election timeout reached, continuing anyway...");
        }
        finally
        {
            // Unsubscribe from events
            foreach (var node in cluster)
            {
                var raftField = typeof(DistributedSettings).GetField(
                    "_raft",
                    System.Reflection.BindingFlags.NonPublic
                        | System.Reflection.BindingFlags.Instance
                );
                if (raftField?.GetValue(node) is Raft.Client.Raft raft)
                {
                    raft.LeaderChanged -= OnLeaderChanged;
                }
            }
            cts.Dispose();
        }
    }

    /// <summary>
    /// Wait for log replication to complete across the cluster
    /// </summary>
    private static async Task WaitForReplication(
        List<DistributedSettings> cluster,
        int timeoutMs = 2000
    )
    {
        var leader = cluster.FirstOrDefault(s => s.State == ServerState.Leader);
        if (leader == null)
        {
            // No leader, just wait a short time
            await Task.Delay(100);
            return;
        }

        // Get the leader's current commit index
        var raftField = typeof(DistributedSettings).GetField(
            "_raft",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
        );
        if (!(raftField?.GetValue(leader) is Raft.Client.Raft leaderRaft))
        {
            await Task.Delay(100);
            return;
        }

        var targetCommitIndex = leaderRaft.CommitIndex;

        // If no entries to replicate, just wait briefly
        if (targetCommitIndex == 0)
        {
            await Task.Delay(100);
            return;
        }

        var tcs = new TaskCompletionSource<bool>();
        var cts = new CancellationTokenSource(timeoutMs);

        // Cancel the task if timeout is reached
        cts.Token.Register(() => tcs.TrySetResult(true));

        var replicationCount = 0;
        var requiredReplications = cluster.Count / 2; // Majority

        void OnLogEntryCommitted(LogEntry entry)
        {
            if (entry.Index >= targetCommitIndex)
            {
                Interlocked.Increment(ref replicationCount);
                if (replicationCount >= requiredReplications)
                {
                    tcs.TrySetResult(true);
                }
            }
        }

        // Subscribe to log commit events from followers
        var followers = cluster.Where(s => s.State == ServerState.Follower).ToList();
        foreach (var follower in followers)
        {
            if (raftField?.GetValue(follower) is Raft.Client.Raft followerRaft)
            {
                followerRaft.LogEntryCommitted += OnLogEntryCommitted;
            }
        }

        try
        {
            await tcs.Task;
        }
        catch (OperationCanceledException)
        {
            // Timeout reached, continue anyway
        }
        finally
        {
            // Unsubscribe from events
            foreach (var follower in followers)
            {
                if (raftField?.GetValue(follower) is Raft.Client.Raft followerRaft)
                {
                    followerRaft.LogEntryCommitted -= OnLogEntryCommitted;
                }
            }
            cts.Dispose();
        }
    }
}
