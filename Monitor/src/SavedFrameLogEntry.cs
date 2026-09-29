namespace BlueHeighliner.MicroGate;

/// <summary>
/// The saved form of a <see cref="FrameLogEntry"/>.
/// </summary>
internal sealed record SavedFrameLogEntry
{
    /// <summary>
    /// Gets the row's display text.
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// Gets the saved frame the row reports, or <see langword="null"/> for a status message.
    /// </summary>
    public SavedMicroGateFrame? Frame { get; init; }
}
