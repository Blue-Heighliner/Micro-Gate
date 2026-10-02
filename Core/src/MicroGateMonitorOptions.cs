namespace BlueHeighliner.MicroGate;

/// <summary>
/// Configures a monitor when it is started with <see cref="IMicroGateMonitor.Start"/>. Covers only the physical layer settings needed to receive and decode frames correctly (no transmit settings); there is no HDLC-layer configuration, since a monitor never participates in the asynchronous balanced mode connection.
/// </summary>
public sealed record MicroGateMonitorOptions
{
    /// <summary>
    /// Gets the line encoding of the MicroGate device. Must match the link being monitored. Defaults to <see cref="MicroGateEncoding.Nrz"/>.
    /// </summary>
    public MicroGateEncoding Encoding { get; init; } = MicroGateEncoding.Nrz;

    /// <summary>
    /// Gets the frame check sequence of the MicroGate device. Must match the link being monitored. Defaults to <see cref="MicroGateCrc.Crc32Ccitt"/>, like a peer.
    /// </summary>
    public MicroGateCrc Crc { get; init; } = MicroGateCrc.Crc32Ccitt;

    /// <summary>
    /// Gets the address the device's hardware receive filter accepts in addition to the broadcast address <c>0xFF</c>, or <see langword="null"/> to disable hardware filtering (the default), reporting every frame on the link regardless of address.
    /// </summary>
    public byte? HardwareAddressFilter { get; init; }

    /// <summary>
    /// Gets where the device takes the receive clock from. Must match how the link being monitored is clocked. Defaults to <see cref="MicroGateReceiveClockSource.OwnPin"/>, like a peer.
    /// </summary>
    public MicroGateReceiveClockSource ReceiveClockSource { get; init; } = MicroGateReceiveClockSource.OwnPin;

    /// <summary>
    /// Gets the divisor of the phase locked loop. Only used when <see cref="ReceiveClockSource"/> is <see cref="MicroGateReceiveClockSource.PhaseLockedLoop"/>. Defaults to <see cref="MicroGatePhaseLockedLoopDivisor.DivideBy32"/>, like a peer.
    /// </summary>
    public MicroGatePhaseLockedLoopDivisor PhaseLockedLoopDivisor { get; init; } = MicroGatePhaseLockedLoopDivisor.DivideBy32;

    /// <summary>
    /// Gets the line speed in bits per second. Only used when <see cref="ReceiveClockSource"/> is <see cref="MicroGateReceiveClockSource.BaudRateGenerator"/>. Defaults to 4800, like a peer.
    /// </summary>
    public int ClockSpeed { get; init; } = 4800;
}
