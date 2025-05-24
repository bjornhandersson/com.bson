namespace raft.client;

/// <summary>
/// Immutable log entry structure containing command and term information
/// </summary>
public sealed record LogEntry
{
    public required int Term { get; init; }
    public required int Index { get; init; }
    public required string Command { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
