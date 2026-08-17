namespace BlueHeighliner.MicroGate.Hdlc;

/// <summary>
/// Represents a single HDLC frame's address and control fields together with its information field, encoded and decoded per the basic (modulo 8) control field format described at
/// https://en.wikipedia.org/wiki/High-Level_Data_Link_Control.
/// </summary>
internal sealed record HdlcFrame
{
    private const byte InformationControlMask = 0x01;
    private const byte InformationControlValue = 0x00;
    private const byte SupervisoryControlMask = 0x03;
    private const byte SupervisoryControlValue = 0x01;
    private const byte SupervisorySequenceMask = 0x0C;
    private const byte ReceiveReadySequenceValue = 0x00;
    private const byte ReceiveNotReadySequenceValue = 0x04;
    private const byte RejectSequenceValue = 0x08;
    private const byte UnnumberedControlMask = 0x03;
    private const byte UnnumberedControlValue = 0x03;
    private const byte SetAsynchronousBalancedModeControl = 0x2F;
    private const byte DisconnectControl = 0x43;
    private const byte UnnumberedAcknowledgeControl = 0x63;
    private const byte DisconnectedModeControl = 0x0F;
    private const byte FrameRejectControl = 0x87;
    private const byte PollFinalBit = 0x10;
    private const byte SequenceMask = 0x07;

    /// <summary>
    /// Parses the address and control fields, and any remaining bytes as the information field, from a raw HDLC frame.
    /// </summary>
    /// <param name="data">The raw frame bytes, as delivered by the underlying HDLC bit-framing transport.</param>
    /// <returns>The parsed <see cref="HdlcFrame"/>.</returns>
    /// <exception cref="HdlcFrameException">The frame is too short to contain an address and control field, or its control field does not encode a recognized frame kind.</exception>
    public static HdlcFrame Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length < 2)
        {
            throw new HdlcFrameException("Frame is too short to contain an address and control field.");
        }

        byte address = data[0];
        byte control = data[1];
        ReadOnlyMemory<byte> payload = data[2..].ToArray();
        bool pollFinal = (control & PollFinalBit) != 0;

        if ((control & InformationControlMask) == InformationControlValue)
        {
            return new HdlcFrame
            {
                Address = address,
                Kind = HdlcFrameKind.Information,
                PollFinal = pollFinal,
                SendSequence = (control >> 1) & SequenceMask,
                ReceiveSequence = (control >> 5) & SequenceMask,
                Payload = payload,
            };
        }

        if ((control & SupervisoryControlMask) == SupervisoryControlValue)
        {
            HdlcFrameKind kind = (control & SupervisorySequenceMask) switch
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
                ReceiveSequence = (control >> 5) & SequenceMask,
                Payload = payload,
            };
        }

        if ((control & UnnumberedControlMask) == UnnumberedControlValue)
        {
            HdlcFrameKind kind = (byte)(control & ~PollFinalBit) switch
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
        byte pollFinal = PollFinal ? PollFinalBit : (byte)0;

        return Kind switch
        {
            HdlcFrameKind.Information => (byte)(((SendSequence & SequenceMask) << 1) | pollFinal | ((ReceiveSequence & SequenceMask) << 5)),
            HdlcFrameKind.ReceiveReady => (byte)(SupervisoryControlValue | ReceiveReadySequenceValue | pollFinal | ((ReceiveSequence & SequenceMask) << 5)),
            HdlcFrameKind.ReceiveNotReady => (byte)(SupervisoryControlValue | ReceiveNotReadySequenceValue | pollFinal | ((ReceiveSequence & SequenceMask) << 5)),
            HdlcFrameKind.Reject => (byte)(SupervisoryControlValue | RejectSequenceValue | pollFinal | ((ReceiveSequence & SequenceMask) << 5)),
            HdlcFrameKind.SetAsynchronousBalancedMode => (byte)(SetAsynchronousBalancedModeControl | pollFinal),
            HdlcFrameKind.Disconnect => (byte)(DisconnectControl | pollFinal),
            HdlcFrameKind.UnnumberedAcknowledge => (byte)(UnnumberedAcknowledgeControl | pollFinal),
            HdlcFrameKind.DisconnectedMode => (byte)(DisconnectedModeControl | pollFinal),
            HdlcFrameKind.FrameReject => (byte)(FrameRejectControl | pollFinal),
            _ => throw new ArgumentOutOfRangeException(nameof(Kind), Kind, "Unrecognized frame kind."),
        };
    }
}
