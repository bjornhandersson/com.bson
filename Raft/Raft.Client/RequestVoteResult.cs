namespace Raft.Client;

/// <summary>
/// Immutable RequestVote RPC results
/// </summary>
public sealed record RequestVoteResult
{
    public required int Term { get; init; }
    public required bool VoteGranted { get; init; }
}
