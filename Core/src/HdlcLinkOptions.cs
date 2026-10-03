namespace BlueHeighliner.MicroGate;

/// <summary>
/// The settings of a peer that must match the remote station's, or the link fails or frames are garbled or discarded, including when the remote station does not use this library (any ADCCP or HDLC station in asynchronous balanced mode). Every setting has a default, so only the settings that differ need to be specified.
/// </summary>
/// <remarks>
/// The two stations' addresses must match too, but they are required arguments of <see cref="IHdlcPeer.Start"/>, since a link always needs two different ones. Settings the remote station merely tolerates, and settings only this station can observe, are on <see cref="HdlcPeerOptions"/>.
/// </remarks>
public sealed record HdlcLinkOptions
{
    /// <summary>
    /// Gets the line encoding of the MicroGate device. Defaults to <see cref="HdlcEncoding.Nrz"/>. Must match the remote station.
    /// </summary>
    public HdlcEncoding Encoding { get; init; } = HdlcEncoding.Nrz;

    /// <summary>
    /// Gets the frame check sequence of the MicroGate device. Defaults to <see cref="HdlcCrc.Crc32Ccitt"/>. Must match the remote station.
    /// </summary>
    public HdlcCrc Crc { get; init; } = HdlcCrc.Crc32Ccitt;

    /// <summary>
    /// Gets the source of the receive clock. Defaults to <see cref="HdlcReceiveClockSource.OwnPin"/>. Must suit how the line is clocked, which the remote station's clocking determines.
    /// </summary>
    public HdlcReceiveClockSource ReceiveClockSource { get; init; } = HdlcReceiveClockSource.OwnPin;

    /// <summary>
    /// Gets the source of the transmit clock. Defaults to <see cref="HdlcTransmitClockSource.OwnPin"/>. Must suit how the line is clocked, which the remote station's clocking determines.
    /// </summary>
    public HdlcTransmitClockSource TransmitClockSource { get; init; } = HdlcTransmitClockSource.OwnPin;

    /// <summary>
    /// Gets the divisor the phase locked loop applies when <see cref="ReceiveClockSource"/> or <see cref="TransmitClockSource"/> is <see cref="HdlcReceiveClockSource.PhaseLockedLoop"/>/<see cref="HdlcTransmitClockSource.PhaseLockedLoop"/>. Defaults to <see cref="HdlcPhaseLockedLoopDivisor.DivideBy32"/>. Must suit the remote station's data rate.
    /// </summary>
    public HdlcPhaseLockedLoopDivisor PhaseLockedLoopDivisor { get; init; } = HdlcPhaseLockedLoopDivisor.DivideBy32;

    /// <summary>
    /// Gets the speed, in bits per second, of the device's internal baud rate generator. Only takes effect when <see cref="ReceiveClockSource"/> or <see cref="TransmitClockSource"/> is <see cref="HdlcReceiveClockSource.BaudRateGenerator"/>/<see cref="HdlcTransmitClockSource.BaudRateGenerator"/>; otherwise the clock comes from an external source and this value is ignored. Defaults to 4800. Must match the remote station's data rate.
    /// </summary>
    public int ClockSpeed { get; init; } = 4800;
}
