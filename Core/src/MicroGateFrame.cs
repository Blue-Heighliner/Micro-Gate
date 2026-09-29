namespace BlueHeighliner.MicroGate;

/// <summary>
/// A single frame observed by <see cref="IMicroGateMonitor"/>, decoded as far as its bytes allow.
/// </summary>
public sealed record MicroGateFrame
{
    /// <summary>
    /// Gets the local time the frame was received.
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Gets the HDLC address byte of the frame, or <c>0x00</c> if the frame was too short to contain one (see <see cref="Kind"/>).
    /// </summary>
    public required byte Address { get; init; }

    /// <summary>
    /// Gets the kind of the frame.
    /// </summary>
    public required MicroGateFrameKind Kind { get; init; }

    /// <summary>
    /// Gets a value indicating whether the poll/final bit is set on the frame. Always <see langword="false"/> when <see cref="Kind"/> is <see cref="MicroGateFrameKind.Malformed"/>.
    /// </summary>
    public required bool PollFinal { get; init; }

    /// <summary>
    /// Gets the send sequence number, N(S), of an <see cref="MicroGateFrameKind.Information"/> frame, or <see langword="null"/> for every other kind.
    /// </summary>
    public int? SendSequence { get; init; }

    /// <summary>
    /// Gets the receive sequence number, N(R), of an <see cref="MicroGateFrameKind.Information"/> or supervisory frame, or <see langword="null"/> for an unnumbered or malformed frame.
    /// </summary>
    public int? ReceiveSequence { get; init; }

    /// <summary>
    /// Gets the information field of the frame, empty unless <see cref="Kind"/> is <see cref="MicroGateFrameKind.Information"/>.
    /// </summary>
    public ReadOnlyMemory<byte> Payload { get; init; } = ReadOnlyMemory<byte>.Empty;

    /// <summary>
    /// Gets the complete raw bytes of the frame exactly as received, including the address and control bytes.
    /// </summary>
    public required ReadOnlyMemory<byte> Raw { get; init; }

    /// <summary>
    /// Gets why the frame could not be decoded, or <see langword="null"/> unless <see cref="Kind"/> is <see cref="MicroGateFrameKind.Malformed"/>.
    /// </summary>
    public string? ErrorMessage { get; init; }
}
