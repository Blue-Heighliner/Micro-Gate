namespace BlueHeighliner.MicroGate.Hdlc;

/// <summary>
/// Represents a single HDLC frame's address and control fields together with its information field, encoded and decoded per the basic (modulo 8) control field format described at
/// https://en.wikipedia.org/wiki/High-Level_Data_Link_Control.
/// </summary>
internal sealed record HdlcFrame
{
    private const byte ReceiveReadySequenceValue = 0x00;
    private const byte ReceiveNotReadySequenceValue = 0x04;
    private const byte RejectSequenceValue = 0x08;
    private const byte SetAsynchronousBalancedModeControl = 0x2F;
    private const byte DisconnectControl = 0x43;
    private const byte UnnumberedAcknowledgeControl = 0x63;
    private const byte DisconnectedModeControl = 0x0F;
    private const byte FrameRejectControl = 0x87;

    private static readonly byte informationControlMask = 0x01;
    private static readonly byte informationControlValue = 0x00;
    private static readonly byte supervisoryControlMask = 0x03;
    private static readonly byte supervisoryControlValue = 0x01;
    private static readonly byte supervisorySequenceMask = 0x0C;
    private static readonly byte unnumberedControlMask = 0x03;
    private static readonly byte unnumberedControlValue = 0x03;
    private static readonly byte pollFinalBit = 0x10;
    private static readonly byte sequenceMask = 0x07;

    /// <summary>
    /// Parses the address and control fields, and any remaining bytes as the information field, from a raw HDLC frame.
    /// </summary>
    /// <param name="data">The raw frame bytes, as delivered by the underlying HDLC bit-framing transport. The information field of the result references this memory rather than copying it.</param>
    /// <returns>The parsed <see cref="HdlcFrame"/>.</returns>
    /// <exception cref="HdlcFrameException">The frame is too short to contain an address and control field, or its control field does not encode a recognized frame kind.</exception>
    public static HdlcFrame Parse(ReadOnlyMemory<byte> data)
    {
        if (data.Length < 2)
        {
            throw new HdlcFrameException("Frame is too short to contain an address and control field.");
        }

        byte address = data.Span[0];
        byte control = data.Span[1];
        ReadOnlyMemory<byte> payload = data[2..];
        bool pollFinal = (control & pollFinalBit) != 0;

        if ((control & informationControlMask) == informationControlValue)
        {
            return new HdlcFrame
            {
                Address = address,
                Kind = HdlcFrameKind.Information,
                PollFinal = pollFinal,
                SendSequence = (control >> 1) & sequenceMask,
                ReceiveSequence = (control >> 5) & sequenceMask,
                Payload = payload,
            };
        }

        if ((control & supervisoryControlMask) == supervisoryControlValue)
        {
            HdlcFrameKind kind = (control & supervisorySequenceMask) switch
            {
                ReceiveReadySequenceValue => HdlcFrameKind.ReceiveReady,
                ReceiveNotReadySequenceValue => HdlcFrameKind.ReceiveNotReady,
                RejectSequenceValue => HdlcFrameKind.Reject,
                _ => throw new HdlcFrameException($"Unsupported supervisory control byte 0x{control:X2}."),
            };

            return new HdlcFrame
            {
                Address = address,
                Kind = kind,
                PollFinal = pollFinal,
                ReceiveSequence = (control >> 5) & sequenceMask,
                Payload = payload,
            };
        }

        if ((control & unnumberedControlMask) == unnumberedControlValue)
        {
            HdlcFrameKind kind = (byte)(control & ~pollFinalBit) switch
            {
                SetAsynchronousBalancedModeControl => HdlcFrameKind.SetAsynchronousBalancedMode,
                DisconnectControl => HdlcFrameKind.Disconnect,
                UnnumberedAcknowledgeControl => HdlcFrameKind.UnnumberedAcknowledge,
                DisconnectedModeControl => HdlcFrameKind.DisconnectedMode,
                FrameRejectControl => HdlcFrameKind.FrameReject,
                _ => throw new HdlcFrameException($"Unsupported unnumbered control byte 0x{control:X2}."),
            };

            return new HdlcFrame
            {
                Address = address,
                Kind = kind,
                PollFinal = pollFinal,
                Payload = payload,
            };
        }

        throw new HdlcFrameException($"Control byte 0x{control:X2} does not encode a recognized frame kind.");
    }

    /// <summary>
    /// Gets the HDLC address byte of the frame.
    /// </summary>
    public required byte Address { get; init; }

    /// <summary>
    /// Gets the kind of the frame.
    /// </summary>
    public required HdlcFrameKind Kind { get; init; }

    /// <summary>
    /// Gets a value indicating whether the poll/final bit is set on the frame.
    /// </summary>
    public required bool PollFinal { get; init; }

    /// <summary>
    /// Gets the send sequence number, N(S), of an <see cref="HdlcFrameKind.Information"/> frame.
    /// </summary>
    public int SendSequence { get; init; }

    /// <summary>
    /// Gets the receive sequence number, N(R), of an <see cref="HdlcFrameKind.Information"/> or supervisory frame.
    /// </summary>
    public int ReceiveSequence { get; init; }

    /// <summary>
    /// Gets the information field of the frame.
    /// </summary>
    public ReadOnlyMemory<byte> Payload { get; init; } = ReadOnlyMemory<byte>.Empty;

    /// <summary>
    /// Encodes the frame as raw HDLC address, control, and information field bytes, ready to be handed to the underlying HDLC bit-framing transport.
    /// </summary>
    /// <returns>The encoded frame bytes.</returns>
    public byte[] ToArray()
    {
        byte control = EncodeControl();
        byte[] result = new byte[2 + Payload.Length];
        result[0] = Address;
        result[1] = control;
        Payload.Span.CopyTo(result.AsSpan(2));
        return result;
    }

    private byte EncodeControl()
    {
        byte pollFinal = PollFinal ? pollFinalBit : (byte)0;

        return Kind switch
        {
            HdlcFrameKind.Information => (byte)(((SendSequence & sequenceMask) << 1) | pollFinal | ((ReceiveSequence & sequenceMask) << 5)),
            HdlcFrameKind.ReceiveReady => (byte)(supervisoryControlValue | ReceiveReadySequenceValue | pollFinal | ((ReceiveSequence & sequenceMask) << 5)),
            HdlcFrameKind.ReceiveNotReady => (byte)(supervisoryControlValue | ReceiveNotReadySequenceValue | pollFinal | ((ReceiveSequence & sequenceMask) << 5)),
            HdlcFrameKind.Reject => (byte)(supervisoryControlValue | RejectSequenceValue | pollFinal | ((ReceiveSequence & sequenceMask) << 5)),
            HdlcFrameKind.SetAsynchronousBalancedMode => (byte)(SetAsynchronousBalancedModeControl | pollFinal),
            HdlcFrameKind.Disconnect => (byte)(DisconnectControl | pollFinal),
            HdlcFrameKind.UnnumberedAcknowledge => (byte)(UnnumberedAcknowledgeControl | pollFinal),
            HdlcFrameKind.DisconnectedMode => (byte)(DisconnectedModeControl | pollFinal),
            HdlcFrameKind.FrameReject => (byte)(FrameRejectControl | pollFinal),
            _ => throw new ArgumentOutOfRangeException(nameof(Kind), Kind, "Unrecognized frame kind."),
        };
    }
}
