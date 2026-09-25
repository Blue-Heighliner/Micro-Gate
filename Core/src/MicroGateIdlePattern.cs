namespace BlueHeighliner.MicroGate;

/// <summary>
/// The pattern a MicroGate device transmits on the line between frames.
/// </summary>
public enum MicroGateIdlePattern
{
    /// <summary>
    /// Repeated HDLC flag bytes (0x7E).
    /// </summary>
    Flags = 0,

    /// <summary>
    /// Alternating zeros and ones.
    /// </summary>
    AlternatingZerosOnes = 1,

    /// <summary>
    /// Continuous zeros.
    /// </summary>
    Zeros = 2,

    /// <summary>
    /// Continuous ones.
    /// </summary>
    Ones = 3,

    /// <summary>
    /// Alternating mark and space.
    /// </summary>
    AlternatingMarkSpace = 4,

    /// <summary>
    /// Continuous space.
    /// </summary>
    Space = 5,

    /// <summary>
    /// Continuous mark.
    /// </summary>
    Mark = 6,
}
