namespace BlueHeighliner.MicroGate;

/// <summary>
/// A non-printable ASCII character.
/// </summary>
internal sealed record ControlCharacter
{
    /// <summary>
    /// Gets the byte value, 0-31 or 127.
    /// </summary>
    public required byte Value { get; init; }

    /// <summary>
    /// Gets the short name shown in a cell, such as <c>LF</c>.
    /// </summary>
    public required string Abbreviation { get; init; }

    /// <summary>
    /// Gets the full name, such as <c>Line Feed</c>.
    /// </summary>
    public required string Name { get; init; }
}
