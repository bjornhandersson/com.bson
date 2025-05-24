namespace raft.client;

/// <summary>
/// Immutable RequestVote RPC arguments
/// </summary>
public sealed record RequestVoteArgs
{
    public required int Term { get; init; }
    public required string CandidateId { get; init; }
    public required int LastLogIndex { get; init; }
    public required int LastLogTerm { get; init; }
}
