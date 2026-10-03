namespace BlueHeighliner.MicroGate;

/// <summary>
/// The divisor the phase locked loop applies when recovering a clock from the data stream. Only meaningful when <see cref="HdlcLinkOptions.ReceiveClockSource"/> or <see cref="HdlcLinkOptions.TransmitClockSource"/> is <see cref="HdlcReceiveClockSource.PhaseLockedLoop"/>/<see cref="HdlcTransmitClockSource.PhaseLockedLoop"/>.
/// </summary>
public enum HdlcPhaseLockedLoopDivisor
{
    /// <summary>
    /// Divide by 32.
    /// </summary>
    DivideBy32,

    /// <summary>
    /// Divide by 8.
    /// </summary>
    DivideBy8,

    /// <summary>
    /// Divide by 16.
    /// </summary>
    DivideBy16,
}
