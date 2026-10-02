namespace BlueHeighliner.MicroGate;

/// <summary>
/// The non-printable ASCII characters.
/// </summary>
internal interface IControlCharacters
{
    /// <summary>
    /// Gets every control character in value order: 0-31, then 127.
    /// </summary>
    IReadOnlyList<ControlCharacter> All { get; }

    /// <summary>
    /// Finds the control character with a byte value.
    /// </summary>
    /// <param name="value">The byte value.</param>
    /// <returns>The control character, or <see langword="null"/> if the value is not one.</returns>
    ControlCharacter? Find(byte value);
}

/// <inheritdoc />
internal sealed class ControlCharacters : IControlCharacters
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ControlCharacters"/> class.
    /// </summary>
    public ControlCharacters()
    {
        string[] names =
        [
            "NUL|Null", "SOH|Start of Heading", "STX|Start of Text", "ETX|End of Text", "EOT|End of Transmission", "ENQ|Enquiry",
            "ACK|Acknowledge", "BEL|Bell", "BS|Backspace", "HT|Horizontal Tab", "LF|Line Feed", "VT|Vertical Tab", "FF|Form Feed",
            "CR|Carriage Return", "SO|Shift Out", "SI|Shift In", "DLE|Data Link Escape", "DC1|Device Control 1", "DC2|Device Control 2",
            "DC3|Device Control 3", "DC4|Device Control 4", "NAK|Negative Acknowledge", "SYN|Synchronous Idle", "ETB|End of Transmission Block",
            "CAN|Cancel", "EM|End of Medium", "SUB|Substitute", "ESC|Escape", "FS|File Separator", "GS|Group Separator",
            "RS|Record Separator", "US|Unit Separator", "DEL|Delete",
        ];

        All = [.. names.Select((entry, index) => new ControlCharacter
        {
            Value = (byte)(index == 32 ? 127 : index),
            Abbreviation = entry.Split('|')[0],
            Name = entry.Split('|')[1],
        })];
        lookup = All.ToDictionary(character => character.Value);
    }

    private readonly Dictionary<byte, ControlCharacter> lookup;

    /// <inheritdoc />
    public IReadOnlyList<ControlCharacter> All { get; }

    /// <inheritdoc />
    public ControlCharacter? Find(byte value) => lookup.GetValueOrDefault(value);
}
