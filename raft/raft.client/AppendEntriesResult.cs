namespace Raft.Client;

/// <summary>
/// Immutable AppendEntries RPC results
/// </summary>
public sealed record AppendEntriesResult
{
    public required int Term { get; init; }
    public required bool Success { get; init; }
}
