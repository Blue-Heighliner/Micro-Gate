namespace BlueHeighliner.MicroGate;

/// <summary>
/// Configures a peer when it is started with <see cref="IMicroGatePeer.Start"/>. Every setting has a default, so only the settings that differ need to be specified.
/// </summary>
/// <remarks>
/// The settings that must match the remote station are grouped in <see cref="Link"/>. The settings here do not need to match: first come those the remote station can observe on the wire and must tolerate, then those only this station can observe, which are always safe to change.
/// </remarks>
public sealed record MicroGatePeerOptions
{
    /// <summary>
    /// Gets the settings that must match the remote station, including one that does not use this library.
    /// </summary>
    public MicroGateLinkOptions Link { get; init; } = new();

    /// <summary>
    /// Gets the pattern the MicroGate device transmits between frames. Defaults to <see cref="MicroGateIdlePattern.Flags"/>. Visible on the line; the remote station must tolerate it, but need not use the same one.
    /// </summary>
    public MicroGateIdlePattern IdlePattern { get; init; } = MicroGateIdlePattern.Flags;

    /// <summary>
    /// Gets the length of the preamble transmitted before each frame. Only sent when <see cref="PreamblePattern"/> is not <see cref="MicroGatePreamblePattern.None"/>. Defaults to <see cref="MicroGatePreambleLength.Bits8"/>. Visible on the line; the remote station must tolerate it, but need not use the same one.
    /// </summary>
    public MicroGatePreambleLength PreambleLength { get; init; } = MicroGatePreambleLength.Bits8;

    /// <summary>
    /// Gets the pattern transmitted as a preamble before each frame. Defaults to <see cref="MicroGatePreamblePattern.None"/>. Visible on the line; the remote station must tolerate it, but need not use the same one.
    /// </summary>
    public MicroGatePreamblePattern PreamblePattern { get; init; } = MicroGatePreamblePattern.None;

    /// <summary>
    /// Gets what the device transmits when the transmitter underruns before a frame is complete. Defaults to <see cref="MicroGateUnderrunAction.Abort7"/>. Only visible to the remote station when an underrun happens, which it sees as an aborted or invalid frame.
    /// </summary>
    public MicroGateUnderrunAction UnderrunAction { get; init; } = MicroGateUnderrunAction.Abort7;

    /// <summary>
    /// Gets a value indicating whether the poll/final bit is disabled, so that it is always zero. Defaults to <see langword="true"/>. Visible on the line; the remote station need not match it, but ADCCP and HDLC stations that poll and wait for a final response only work with it set to <see langword="false"/>.
    /// </summary>
    /// <remarks>
    /// When <see langword="true"/>, the control byte of every HDLC frame sent leaves the poll/final bit at 0, regardless of the frame's role or the poll/final bit of the frame being answered.
    /// </remarks>
    public bool DisablePollFinalBit { get; init; } = true;

    /// <summary>
    /// Gets the largest payload, in bytes, that may be sent in one information frame, from 1 to 4090 (the MicroGate drivers discard received frames larger than 4096 bytes, and a frame also carries an address, a control field, and a frame check sequence). Defaults to 1500. Must not exceed the largest frame the remote station can receive, which discards larger ones without telling this station; a smaller value is always safe. Devices can deliver less than the driver's 4096-byte limit: the SyncLink USB devices this was tested on delivered frames of up to 3176 bytes of payload and silently lost larger ones, so a link that includes such a device needs a value no higher than that.
    /// </summary>
    public int MaxInfoField { get; init; } = 1500;

    /// <summary>
    /// Gets how often the connection request (SABM) is re-sent until the remote peer answers, or <see langword="null"/> to never send one from this side and only accept the remote peer's request. Defaults to one second. The remote station sees each request, and a request arriving while it is connected resets the link, so keep it longer than the time the remote station needs to answer.
    /// </summary>
    /// <remarks>
    /// Re-sending matters because nothing acknowledges a request that arrives while the remote peer is not yet reading. Both peers may send requests at once, and a request received from the remote peer also establishes the link. A peer with a <see langword="null"/> interval needs the remote peer to send one.
    /// </remarks>
    public TimeSpan? RetryInterval { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Gets how long sent data may go unacknowledged before every unacknowledged information frame is sent again, or <see langword="null"/> to only send frames again when the remote peer rejects them. Defaults to one second. Not negotiated, so any value interoperates. The time counts from when a frame has finished being transmitted, and not while a frame is being transmitted or received, so it only needs to cover the remote station's time to answer; one shorter than that sends duplicates the remote station must discard.
    /// </summary>
    /// <remarks>
    /// A frame lost on the line is normally recovered when the next frame arrives and the remote peer rejects the gap. The interval covers the case where nothing follows the lost frame, or the rejection itself is lost.
    /// </remarks>
    public TimeSpan? RetransmitInterval { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Gets how many times unacknowledged information frames are sent again, without any acknowledgement arriving, before the remote peer is considered gone and the peer becomes <see cref="MicroGatePeerState.Disconnected"/>, or <see langword="null"/> to keep sending indefinitely. Defaults to 21. It counts timer driven resends, so it has no effect when <see cref="RetransmitInterval"/> is <see langword="null"/>. Local only.
    /// </summary>
    public int? MaxRetransmissions { get; init; } = 21;

    /// <summary>
    /// Gets the maximum number of information frames that may be sent before one is acknowledged, from 1 to 7 (the most a modulo-8 HDLC sequence number space allows). Defaults to 7. Local only: HDLC never negotiates it, so the remote station's window is independent.
    /// </summary>
    public int TransmitWindow { get; init; } = 7;

    /// <summary>
    /// Gets a value indicating whether the device's internal loopback mode is enabled, looping transmitted data back to the receiver internally instead of sending it on the line, for self-test without a remote peer. Defaults to <see langword="false"/>. Local only; nothing reaches the remote station while it is enabled.
    /// </summary>
    public bool Loopback { get; init; }
}
