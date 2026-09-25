namespace BlueHeighliner.MicroGate.Hdlc;

/// <summary>
/// Represents the outcome of feeding a raw received frame into an <see cref="IHdlcStateMachine"/>.
/// </summary>
internal sealed record HdlcReceiveResult
{
    /// <summary>
    /// Gets the connection state of the state machine after processing the frame.
    /// </summary>
    public required HdlcConnectionState State { get; init; }

    /// <summary>
    /// Gets the information field delivered by the frame, or <see langword="null"/> if the frame did not carry payload accepted for delivery. It references the memory the frame was received into.
    /// </summary>
    public ReadOnlyMemory<byte>? Payload { get; init; }

    /// <summary>
    /// Gets the number of previously sent information frames that the frame acknowledged, or that were discarded because the sequence numbers were reset.
    /// </summary>
    public int Acknowledged { get; init; }

    /// <summary>
    /// Gets a value indicating whether the unacknowledged information frames must be transmitted again, because the peer rejected them or the link was reset. The caller obtains the frames from <see cref="IHdlcStateMachine.CreateRetransmission"/> at the moment it writes them, so they carry current sequence numbers.
    /// </summary>
    public bool Retransmit { get; init; }

    /// <summary>
    /// Gets the raw bytes of a frame that must be transmitted back to the peer in response, or <see langword="null"/> if no response is required.
    /// </summary>
    public ReadOnlyMemory<byte>? Response { get; init; }
}
