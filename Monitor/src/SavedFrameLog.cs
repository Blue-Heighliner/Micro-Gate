namespace BlueHeighliner.MicroGate;

/// <summary>
/// The root of a saved frame log file.
/// </summary>
internal sealed record SavedFrameLog
{
    /// <summary>
    /// Gets the version of this file's schema, for detecting an incompatible file on load.
    /// </summary>
    public int Version { get; init; } = 1;

    /// <summary>
    /// Gets the saved rows, in the order they were logged.
    /// </summary>
    public required IReadOnlyList<SavedFrameLogEntry> Entries { get; init; }
}
