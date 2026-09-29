namespace BlueHeighliner.MicroGate;

/// <summary>
/// The saved form of a <see cref="MicroGateFrame"/>: the same fields, with <see cref="MicroGateFrame.Payload"/> and <see cref="MicroGateFrame.Raw"/> as plain byte arrays, which <see cref="System.Text.Json.JsonSerializer"/> writes as base64 rather than needing a converter for <see cref="ReadOnlyMemory{T}"/>.
/// </summary>
internal sealed record SavedMicroGateFrame
{
    /// <summary>
    /// Gets the local time the frame was received.
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Gets the HDLC address byte of the frame.
    /// </summary>
    public required byte Address { get; init; }

    /// <summary>
    /// Gets the kind of the frame.
    /// </summary>
    public required MicroGateFrameKind Kind { get; init; }

    /// <summary>
    /// Gets a value indicating whether the poll/final bit is set on the frame.
    /// </summary>
    public required bool PollFinal { get; init; }

    /// <summary>
    /// Gets the send sequence number, N(S), or <see langword="null"/> if the frame did not carry one.
    /// </summary>
    public int? SendSequence { get; init; }

    /// <summary>
    /// Gets the receive sequence number, N(R), or <see langword="null"/> if the frame did not carry one.
    /// </summary>
    public int? ReceiveSequence { get; init; }

    /// <summary>
    /// Gets the information field of the frame.
    /// </summary>
    public byte[] Payload { get; init; } = [];

    /// <summary>
    /// Gets the complete raw bytes of the frame exactly as received.
    /// </summary>
    public required byte[] Raw { get; init; }

    /// <summary>
    /// Gets why the frame could not be decoded, or <see langword="null"/> if it decoded successfully.
    /// </summary>
    public string? ErrorMessage { get; init; }
}
