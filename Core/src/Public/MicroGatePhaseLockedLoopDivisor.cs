namespace BlueHeighliner.MicroGate;

/// <summary>
/// The divisor the phase locked loop applies when recovering a clock from the data stream. Only meaningful when <see cref="MicroGatePeerOptions.ReceiveClockSource"/> or <see cref="MicroGatePeerOptions.TransmitClockSource"/> is <see cref="MicroGateReceiveClockSource.PhaseLockedLoop"/>/<see cref="MicroGateTransmitClockSource.PhaseLockedLoop"/>.
/// </summary>
public enum MicroGatePhaseLockedLoopDivisor
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
