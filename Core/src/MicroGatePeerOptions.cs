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
    /// Gets the frame check sequence of the MicroGate device. Defaults to <see cref="MicroGateCrc.Crc16Ccitt"/>.
    /// </summary>
    public MicroGateCrc Crc { get; init; } = MicroGateCrc.Crc16Ccitt;

    /// <summary>
    /// Gets the pattern the MicroGate device transmits between frames. Defaults to <see cref="MicroGateIdlePattern.Flags"/>.
    /// </summary>
    public MicroGateIdlePattern IdlePattern { get; init; } = MicroGateIdlePattern.Flags;

    /// <summary>
    /// Gets the address the device's hardware receive filter accepts in addition to the broadcast address <c>0xFF</c>, or <see langword="null"/> to disable hardware filtering (the default), leaving address filtering to the HDLC layer.
    /// </summary>
    public byte? HardwareAddressFilter { get; init; }

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
    /// Gets how many times unacknowledged information frames are sent again, without any acknowledgement arriving, before the remote peer is considered gone and the peer becomes <see cref="MicroGatePeerState.Disconnected"/>, or <see langword="null"/> to keep sending indefinitely. Defaults to 10. It counts timer driven resends, so it has no effect when <see cref="RetransmitInterval"/> is <see langword="null"/>.
    /// </summary>
    public int? MaxRetransmissions { get; init; } = 10;

    /// <summary>
    /// Gets a value indicating whether the poll/final bit is disabled, so that it is always zero.
    /// </summary>
    /// <remarks>
    /// When <see langword="true"/>, the control byte of every HDLC frame sent leaves the poll/final bit at 0, regardless of the frame's role or the poll/final bit of the frame being answered.
    /// </remarks>
    public bool DisablePollFinalBit { get; init; }
}
