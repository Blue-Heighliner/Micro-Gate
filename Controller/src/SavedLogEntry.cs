namespace BlueHeighliner.MicroGate;

/// <summary>
/// One <see cref="LogEntry"/> as stored in a saved log file.
/// </summary>
internal sealed record SavedLogEntry
{
    /// <summary>
    /// Gets the row's display text.
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// Gets the row's data as Base64, or <see langword="null"/> for a status message.
    /// </summary>
    public string? Data { get; init; }

    /// <summary>
    /// Gets the indexes of the data cells that were shown as their 0-255 value.
    /// </summary>
    public IReadOnlyList<int> RawCells { get; init; } = [];
}
