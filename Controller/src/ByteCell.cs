namespace BlueHeighliner.MicroGate;

/// <summary>
/// One byte shown in a <see cref="ByteGrid"/>, together with whether it is shown as its raw value or as its ASCII form.
/// </summary>
internal sealed class ByteCell
{
    /// <summary>
    /// Gets or sets the byte value.
    /// </summary>
    public required byte Value { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the cell shows the 0-255 value instead of its ASCII character (or control character abbreviation). Values above 127 have no ASCII form and are always shown as values.
    /// </summary>
    public bool ShowRaw { get; set; }
}
