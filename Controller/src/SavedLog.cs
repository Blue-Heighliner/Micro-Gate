namespace BlueHeighliner.MicroGate;

/// <summary>
/// The contents of a saved log file.
/// </summary>
internal sealed record SavedLog
{
    /// <summary>
    /// Gets the file format version, so a later change to the format can still read older files.
    /// </summary>
    public int Version { get; init; } = 1;

    /// <summary>
    /// Gets the log rows in order.
    /// </summary>
    public required IReadOnlyList<SavedLogEntry> Entries { get; init; }
}
