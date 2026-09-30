namespace BlueHeighliner.MicroGate;

/// <summary>
/// The source of the receive clock for a MicroGate device in HDLC mode.
/// </summary>
public enum MicroGateReceiveClockSource
{
    /// <summary>
    /// The receive clock (RXC) pin.
    /// </summary>
    OwnPin,

    /// <summary>
    /// The transmit clock (TXC) pin.
    /// </summary>
    OtherPin,

    /// <summary>
    /// The phase locked loop, recovering the clock from the received data stream. Only meaningful with an encoding that carries its own clock, such as <see cref="MicroGateEncoding.NrziSpace"/>. <see cref="MicroGateLinkOptions.PhaseLockedLoopDivisor"/> selects the divisor.
    /// </summary>
    PhaseLockedLoop,

    /// <summary>
    /// The device's internal baud rate generator, running at <see cref="MicroGateLinkOptions.ClockSpeed"/>.
    /// </summary>
    BaudRateGenerator,
}
