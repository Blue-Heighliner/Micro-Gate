namespace BlueHeighliner.MicroGate;

/// <summary>
/// The length of the preamble a MicroGate device transmits before each frame. Only sent when <see cref="MicroGatePeerOptions.PreamblePattern"/> is not <see cref="MicroGatePreamblePattern.None"/>.
/// </summary>
public enum MicroGatePreambleLength
{
    /// <summary>
    /// 8 bits.
    /// </summary>
    Bits8,

    /// <summary>
    /// 16 bits.
    /// </summary>
    Bits16,

    /// <summary>
    /// 32 bits.
    /// </summary>
    Bits32,

    /// <summary>
    /// 64 bits.
    /// </summary>
    Bits64,
}
