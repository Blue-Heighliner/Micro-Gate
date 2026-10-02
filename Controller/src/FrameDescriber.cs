namespace BlueHeighliner.MicroGate;

/// <summary>
/// Describes a frame as the text of a log row; its bytes are shown in the row's table.
/// </summary>
internal interface IFrameDescriber
{
    /// <summary>
    /// Describes a frame.
    /// </summary>
    /// <param name="frame">The frame to describe.</param>
    /// <returns>The frame's kind, address, sequence numbers, poll/final bit, and size on one line.</returns>
    string Describe(MicroGateFrame frame);
}

/// <inheritdoc />
internal sealed class FrameDescriber : IFrameDescriber
{
    /// <inheritdoc />
    public string Describe(MicroGateFrame frame)
    {
        string size = frame.Raw.Length == 1 ? "1 byte" : $"{frame.Raw.Length} bytes";
        if (frame.Kind == MicroGateFrameKind.Malformed)
        {
            return $"MALFORMED  {frame.ErrorMessage}  {size}";
        }

        string sequence = (frame.SendSequence, frame.ReceiveSequence) switch
        {
            (int sendSequence, int receiveSequence) => $"  N(S)={sendSequence} N(R)={receiveSequence}",
            (null, int receiveSequence) => $"  N(R)={receiveSequence}",
            _ => string.Empty,
        };
        string pollFinal = frame.PollFinal ? "  P/F" : string.Empty;
        return $"{Abbreviate(frame.Kind),-4}  addr=0x{frame.Address:X2}{sequence}{pollFinal}  {size}";
    }

    private string Abbreviate(MicroGateFrameKind kind) => kind switch
    {
        MicroGateFrameKind.Information => "I",
        MicroGateFrameKind.ReceiveReady => "RR",
        MicroGateFrameKind.ReceiveNotReady => "RNR",
        MicroGateFrameKind.Reject => "REJ",
        MicroGateFrameKind.SetAsynchronousBalancedMode => "SABM",
        MicroGateFrameKind.Disconnect => "DISC",
        MicroGateFrameKind.UnnumberedAcknowledge => "UA",
        MicroGateFrameKind.DisconnectedMode => "DM",
        MicroGateFrameKind.FrameReject => "FRMR",
        _ => "?",
    };
}
