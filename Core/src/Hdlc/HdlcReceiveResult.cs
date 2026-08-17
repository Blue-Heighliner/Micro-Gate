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
    /// Gets the information field delivered by the frame, or <see langword="null"/> if the frame did not carry payload accepted for delivery.
    /// </summary>
    public ReadOnlyMemory<byte>? Payload { get; init; }

    /// <summary>
    /// Gets the raw bytes of a frame that must be transmitted back to the peer in response, or <see langword="null"/> if no response is required.
    /// </summary>
    public ReadOnlyMemory<byte>? Response { get; init; }
}
