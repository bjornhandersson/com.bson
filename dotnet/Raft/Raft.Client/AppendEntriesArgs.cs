namespace Raft.Client;

/// <summary>
/// Immutable AppendEntries RPC arguments
/// </summary>
public sealed record AppendEntriesArgs
{
    public required int Term { get; init; }
    public required string LeaderId { get; init; }
    public required int PrevLogIndex { get; init; }
    public required int PrevLogTerm { get; init; }
    public required IReadOnlyList<LogEntry> Entries { get; init; }
    public required int LeaderCommit { get; init; }
}
