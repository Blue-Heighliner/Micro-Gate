namespace BlueHeighliner.MicroGate;

/// <summary>
/// Describes the HDLC fields of a frame as log text, so the row's byte table can hold only the frame's data. Every value is a byte value from 0 to 255, never hexadecimal.
/// </summary>
internal interface IFrameDescriber
{
    /// <summary>
    /// Describes a frame on one line, naming each field.
    /// </summary>
    /// <param name="frame">The frame to describe.</param>
    /// <returns>The frame type and its address, send and receive sequence numbers, poll/final bit, and data size, each labeled with its field name.</returns>
    string Describe(HdlcFrame frame);

    /// <summary>
    /// Describes every HDLC field of a frame as a labeled list.
    /// </summary>
    /// <param name="frame">The frame to describe.</param>
    /// <returns>The address, control byte, frame type, poll/final bit, sequence numbers, and data size, or for a frame that could not be parsed its error and size.</returns>
    IReadOnlyList<LogField> Details(HdlcFrame frame);
}

/// <inheritdoc />
internal sealed class FrameDescriber : IFrameDescriber
{
    /// <inheritdoc />
    public string Describe(HdlcFrame frame)
    {
        if (frame.Kind == HdlcFrameKind.Malformed)
        {
            return $"MALFORMED  Error:{frame.ErrorMessage}  Size:{frame.Raw.Length}B";
        }

        List<string> fields = [Abbreviate(frame.Kind), $"Address:{frame.Address}"];
        if (frame.SendSequence is { } sendSequence)
        {
            fields.Add($"Send:{sendSequence}");
        }

        if (frame.ReceiveSequence is { } receiveSequence)
        {
            fields.Add($"Receive:{receiveSequence}");
        }

        fields.Add($"Poll/Final:{(frame.PollFinal ? 1 : 0)}");
        fields.Add($"Data:{frame.Payload.Length}B");
        return string.Join("  ", fields);
    }

    /// <inheritdoc />
    public IReadOnlyList<LogField> Details(HdlcFrame frame)
    {
        if (frame.Kind == HdlcFrameKind.Malformed)
        {
            return [Field("Frame Type", "Malformed"), Field("Error", frame.ErrorMessage ?? string.Empty), Field("Size", $"{frame.Raw.Length}B (shown below)")];
        }

        List<LogField> fields =
        [
            Field("Address", frame.Address.ToString(CultureInfo.InvariantCulture)),
            Field("Control", frame.Raw.Length >= 2 ? frame.Raw.Span[1].ToString(CultureInfo.InvariantCulture) : "none"),
            Field("Frame Type", Name(frame.Kind)),
            Field("Poll/Final", frame.PollFinal ? "1" : "0"),
        ];
        if (frame.SendSequence is { } sendSequence)
        {
            fields.Add(Field("Send", sendSequence.ToString(CultureInfo.InvariantCulture)));
        }

        if (frame.ReceiveSequence is { } receiveSequence)
        {
            fields.Add(Field("Receive", receiveSequence.ToString(CultureInfo.InvariantCulture)));
        }

        fields.Add(Field("Data", frame.Payload.IsEmpty ? "none" : $"{frame.Payload.Length}B"));
        return fields;
    }

    private LogField Field(string name, string value) => new() { Name = name, Value = value };

    private string Abbreviate(HdlcFrameKind kind) => kind switch
    {
        HdlcFrameKind.Information => "I",
        HdlcFrameKind.ReceiveReady => "RR",
        HdlcFrameKind.ReceiveNotReady => "RNR",
        HdlcFrameKind.Reject => "REJ",
        HdlcFrameKind.SetAsynchronousBalancedMode => "SABM",
        HdlcFrameKind.Disconnect => "DISC",
        HdlcFrameKind.UnnumberedAcknowledge => "UA",
        HdlcFrameKind.DisconnectedMode => "DM",
        HdlcFrameKind.FrameReject => "FRMR",
        _ => "?",
    };

    private string Name(HdlcFrameKind kind) => kind switch
    {
        HdlcFrameKind.Information => "Information (I)",
        HdlcFrameKind.ReceiveReady => "Receive Ready (RR)",
        HdlcFrameKind.ReceiveNotReady => "Receive Not Ready (RNR)",
        HdlcFrameKind.Reject => "Reject (REJ)",
        HdlcFrameKind.SetAsynchronousBalancedMode => "Set Asynchronous Balanced Mode (SABM)",
        HdlcFrameKind.Disconnect => "Disconnect (DISC)",
        HdlcFrameKind.UnnumberedAcknowledge => "Unnumbered Acknowledge (UA)",
        HdlcFrameKind.DisconnectedMode => "Disconnected Mode (DM)",
        HdlcFrameKind.FrameReject => "Frame Reject (FRMR)",
        _ => "Unknown",
    };
}
