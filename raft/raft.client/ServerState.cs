namespace raft.client;

/// <summary>
/// Server states as defined in the Raft algorithm
/// </summary>
public enum ServerState
{
    Follower,
    Candidate,
    Leader,
}
