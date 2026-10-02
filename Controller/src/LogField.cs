namespace BlueHeighliner.MicroGate;

/// <summary>
/// One named field of a frame, such as its address or sequence number, shown when a log row is expanded.
/// </summary>
internal sealed record LogField
{
    /// <summary>
    /// Gets the field's name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the field's value as text.
    /// </summary>
    public required string Value { get; init; }
}
