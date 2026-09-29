namespace BlueHeighliner.MicroGate;

/// <summary>
/// Configures a peer when it is started with <see cref="IMicroGatePeer.Start"/>. Every setting has a default, so only the settings that differ need to be specified.
/// </summary>
public sealed record MicroGatePeerOptions
{
    /// <summary>
    /// Gets the line encoding of the MicroGate device. Defaults to <see cref="MicroGateEncoding.Nrz"/>.
    /// </summary>
    public MicroGateEncoding Encoding { get; init; } = MicroGateEncoding.Nrz;

    /// <summary>
    /// Gets the frame check sequence of the MicroGate device. Defaults to <see cref="MicroGateCrc.Crc32Ccitt"/>.
    /// </summary>
    public MicroGateCrc Crc { get; init; } = MicroGateCrc.Crc32Ccitt;

    /// <summary>
    /// Gets the pattern the MicroGate device transmits between frames. Defaults to <see cref="MicroGateIdlePattern.Flags"/>.
    /// </summary>
    public MicroGateIdlePattern IdlePattern { get; init; } = MicroGateIdlePattern.Flags;

    /// <summary>
    /// Gets a value indicating whether the device's internal loopback mode is enabled, looping transmitted data back to the receiver internally instead of sending it on the line, for self-test without a remote peer. Defaults to <see langword="false"/>.
    /// </summary>
    public bool Loopback { get; init; }

    /// <summary>
    /// Gets the source of the receive clock. Defaults to <see cref="MicroGateReceiveClockSource.OwnPin"/>.
    /// </summary>
    public MicroGateReceiveClockSource ReceiveClockSource { get; init; } = MicroGateReceiveClockSource.OwnPin;

    /// <summary>
    /// Gets the source of the transmit clock. Defaults to <see cref="MicroGateTransmitClockSource.OwnPin"/>.
    /// </summary>
    public MicroGateTransmitClockSource TransmitClockSource { get; init; } = MicroGateTransmitClockSource.OwnPin;

    /// <summary>
    /// Gets the divisor the phase locked loop applies when <see cref="ReceiveClockSource"/> or <see cref="TransmitClockSource"/> is <see cref="MicroGateReceiveClockSource.PhaseLockedLoop"/>/<see cref="MicroGateTransmitClockSource.PhaseLockedLoop"/>. Defaults to <see cref="MicroGatePhaseLockedLoopDivisor.DivideBy32"/>.
    /// </summary>
    public MicroGatePhaseLockedLoopDivisor PhaseLockedLoopDivisor { get; init; } = MicroGatePhaseLockedLoopDivisor.DivideBy32;

    /// <summary>
    /// Gets what the device transmits when the transmitter underruns before a frame is complete. Defaults to <see cref="MicroGateUnderrunAction.Abort7"/>.
    /// </summary>
    public MicroGateUnderrunAction UnderrunAction { get; init; } = MicroGateUnderrunAction.Abort7;

    /// <summary>
    /// Gets the speed, in bits per second, of the device's internal baud rate generator. Only takes effect when <see cref="ReceiveClockSource"/> or <see cref="TransmitClockSource"/> is <see cref="MicroGateReceiveClockSource.BaudRateGenerator"/>/<see cref="MicroGateTransmitClockSource.BaudRateGenerator"/>; otherwise the clock comes from an external source and this value is ignored. Defaults to 4800.
    /// </summary>
    public int ClockSpeed { get; init; } = 4800;

    /// <summary>
    /// Gets the length of the preamble transmitted before each frame. Only sent when <see cref="PreamblePattern"/> is not <see cref="MicroGatePreamblePattern.None"/>. Defaults to <see cref="MicroGatePreambleLength.Bits8"/>.
    /// </summary>
    public MicroGatePreambleLength PreambleLength { get; init; } = MicroGatePreambleLength.Bits8;

    /// <summary>
    /// Gets the pattern transmitted as a preamble before each frame. Defaults to <see cref="MicroGatePreamblePattern.None"/>.
    /// </summary>
    public MicroGatePreamblePattern PreamblePattern { get; init; } = MicroGatePreamblePattern.None;

    /// <summary>
    /// Gets the HDLC address byte this station sends in every frame, and expects to see in every frame it accepts from the peer station. Defaults to <c>0xFF</c>.
    /// </summary>
    public byte Address { get; init; } = 0xFF;

    /// <summary>
    /// Gets how often the connection request (SABM) is re-sent until the remote peer answers, or <see langword="null"/> to never send one from this side and only accept the remote peer's request. Defaults to one second.
    /// </summary>
    /// <remarks>
    /// Re-sending matters because nothing acknowledges a request that arrives while the remote peer is not yet reading. Both peers may send requests at once, and a request received from the remote peer also establishes the link. A peer with a <see langword="null"/> interval needs the remote peer to send one.
    /// </remarks>
    public TimeSpan? RetryInterval { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Gets how long sent data may go unacknowledged before every unacknowledged information frame is sent again, or <see langword="null"/> to only send frames again when the remote peer rejects them. Defaults to one second.
    /// </summary>
    /// <remarks>
    /// A frame lost on the line is normally recovered when the next frame arrives and the remote peer rejects the gap. The interval covers the case where nothing follows the lost frame, or the rejection itself is lost.
    /// </remarks>
    public TimeSpan? RetransmitInterval { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Gets how many times unacknowledged information frames are sent again, without any acknowledgement arriving, before the remote peer is considered gone and the peer becomes <see cref="MicroGatePeerState.Disconnected"/>, or <see langword="null"/> to keep sending indefinitely. Defaults to 21. It counts timer driven resends, so it has no effect when <see cref="RetransmitInterval"/> is <see langword="null"/>.
    /// </summary>
    public int? MaxRetransmissions { get; init; } = 21;

    /// <summary>
    /// Gets a value indicating whether the poll/final bit is disabled, so that it is always zero. Defaults to <see langword="true"/>.
    /// </summary>
    /// <remarks>
    /// When <see langword="true"/>, the control byte of every HDLC frame sent leaves the poll/final bit at 0, regardless of the frame's role or the poll/final bit of the frame being answered.
    /// </remarks>
    public bool DisablePollFinalBit { get; init; } = true;

    /// <summary>
    /// Gets the maximum number of information frames that may be sent before one is acknowledged, from 1 to 7 (the most a modulo-8 HDLC sequence number space allows). Defaults to 7.
    /// </summary>
    /// <remarks>
    /// Unlike the other HDLC-layer settings, this does not default to what the MicroGate library this one replaces used (which defaulted to 1, effectively stop-and-wait sending): the window is a purely local sending policy that HDLC never negotiates between the two stations, so a smaller window on one side does not require a matching value on the other, and there is no interoperability reason to give up the throughput the maximum window allows.
    /// </remarks>
    public int TransmitWindow { get; init; } = 7;

    /// <summary>
    /// Gets the largest payload, in bytes, that may be sent in one information frame, from 1 to 4090 (the MicroGate drivers discard received frames larger than 4096 bytes, and a frame also carries an address, a control field, and a frame check sequence). Defaults to 1500. A smaller value here than the remote peer's own limit protects it from frames it would otherwise silently discard as too large.
    /// </summary>
    public int MaxInfoField { get; init; } = 1500;
}
