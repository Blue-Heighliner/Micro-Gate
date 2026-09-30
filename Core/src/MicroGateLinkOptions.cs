namespace BlueHeighliner.MicroGate;

/// <summary>
/// The settings of a peer that must match the remote station's, or the link fails or frames are garbled or discarded, including when the remote station does not use this library (any ADCCP or HDLC station in asynchronous balanced mode). Every setting has a default, so only the settings that differ need to be specified.
/// </summary>
/// <remarks>
/// The two stations' addresses must match too, but they are required arguments of <see cref="IMicroGatePeer.Start"/>, since a link always needs two different ones. Settings the remote station merely tolerates, and settings only this station can observe, are on <see cref="MicroGatePeerOptions"/>.
/// </remarks>
public sealed record MicroGateLinkOptions
{
    /// <summary>
    /// Gets the line encoding of the MicroGate device. Defaults to <see cref="MicroGateEncoding.Nrz"/>. Must match the remote station.
    /// </summary>
    public MicroGateEncoding Encoding { get; init; } = MicroGateEncoding.Nrz;

    /// <summary>
    /// Gets the frame check sequence of the MicroGate device. Defaults to <see cref="MicroGateCrc.Crc32Ccitt"/>. Must match the remote station.
    /// </summary>
    public MicroGateCrc Crc { get; init; } = MicroGateCrc.Crc32Ccitt;

    /// <summary>
    /// Gets the source of the receive clock. Defaults to <see cref="MicroGateReceiveClockSource.OwnPin"/>. Must suit how the line is clocked, which the remote station's clocking determines.
    /// </summary>
    public MicroGateReceiveClockSource ReceiveClockSource { get; init; } = MicroGateReceiveClockSource.OwnPin;

    /// <summary>
    /// Gets the source of the transmit clock. Defaults to <see cref="MicroGateTransmitClockSource.OwnPin"/>. Must suit how the line is clocked, which the remote station's clocking determines.
    /// </summary>
    public MicroGateTransmitClockSource TransmitClockSource { get; init; } = MicroGateTransmitClockSource.OwnPin;

    /// <summary>
    /// Gets the divisor the phase locked loop applies when <see cref="ReceiveClockSource"/> or <see cref="TransmitClockSource"/> is <see cref="MicroGateReceiveClockSource.PhaseLockedLoop"/>/<see cref="MicroGateTransmitClockSource.PhaseLockedLoop"/>. Defaults to <see cref="MicroGatePhaseLockedLoopDivisor.DivideBy32"/>. Must suit the remote station's data rate.
    /// </summary>
    public MicroGatePhaseLockedLoopDivisor PhaseLockedLoopDivisor { get; init; } = MicroGatePhaseLockedLoopDivisor.DivideBy32;

    /// <summary>
    /// Gets the speed, in bits per second, of the device's internal baud rate generator. Only takes effect when <see cref="ReceiveClockSource"/> or <see cref="TransmitClockSource"/> is <see cref="MicroGateReceiveClockSource.BaudRateGenerator"/>/<see cref="MicroGateTransmitClockSource.BaudRateGenerator"/>; otherwise the clock comes from an external source and this value is ignored. Defaults to 4800. Must match the remote station's data rate.
    /// </summary>
    public int ClockSpeed { get; init; } = 4800;
}
