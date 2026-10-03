namespace BlueHeighliner.MicroGate;

/// <summary>
/// The pattern a MicroGate device transmits as a preamble before each frame, of the length given by <see cref="HdlcPeerOptions.PreambleLength"/>.
/// </summary>
public enum HdlcPreamblePattern
{
    /// <summary>
    /// No preamble is sent.
    /// </summary>
    None,

    /// <summary>
    /// Continuous zeros.
    /// </summary>
    Zeros,

    /// <summary>
    /// Repeated HDLC flag bytes (0x7E).
    /// </summary>
    Flags,

    /// <summary>
    /// Alternating one and zero, starting with one.
    /// </summary>
    Alternating10,

    /// <summary>
    /// Alternating zero and one, starting with zero.
    /// </summary>
    Alternating01,

    /// <summary>
    /// Continuous ones.
    /// </summary>
    Ones,
}
