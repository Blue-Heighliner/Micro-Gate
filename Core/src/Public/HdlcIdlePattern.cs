namespace BlueHeighliner.MicroGate;

/// <summary>
/// The pattern a MicroGate device transmits on the line between frames.
/// </summary>
public enum HdlcIdlePattern
{
    /// <summary>
    /// Repeated HDLC flag bytes (0x7E).
    /// </summary>
    Flags,

    /// <summary>
    /// Alternating zeros and ones.
    /// </summary>
    AlternatingZerosOnes,

    /// <summary>
    /// Continuous zeros.
    /// </summary>
    Zeros,

    /// <summary>
    /// Continuous ones.
    /// </summary>
    Ones,

    /// <summary>
    /// Alternating mark and space.
    /// </summary>
    AlternatingMarkSpace,

    /// <summary>
    /// Continuous space.
    /// </summary>
    Space,

    /// <summary>
    /// Continuous mark.
    /// </summary>
    Mark,
}
