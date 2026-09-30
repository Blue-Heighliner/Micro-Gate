namespace BlueHeighliner.MicroGate;

/// <summary>
/// The source of the transmit clock for a MicroGate device in HDLC mode.
/// </summary>
public enum MicroGateTransmitClockSource
{
    /// <summary>
    /// The transmit clock (TXC) pin.
    /// </summary>
    OwnPin,

    /// <summary>
    /// The receive clock (RXC) pin.
    /// </summary>
    OtherPin,

    /// <summary>
    /// The phase locked loop recovered from the received data stream, so the transmit clock tracks the receive clock. <see cref="MicroGateLinkOptions.PhaseLockedLoopDivisor"/> selects the divisor.
    /// </summary>
    PhaseLockedLoop,

    /// <summary>
    /// The device's internal baud rate generator, running at <see cref="MicroGateLinkOptions.ClockSpeed"/>.
    /// </summary>
    BaudRateGenerator,
}
