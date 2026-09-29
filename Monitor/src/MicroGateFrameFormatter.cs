namespace BlueHeighliner.MicroGate;

/// <summary>
/// Formats a <see cref="MicroGateFrame"/> as a single human-readable log line.
/// </summary>
internal static class MicroGateFrameFormatter
{
    private static readonly int maxDisplayedBytes = 32;

    /// <summary>
    /// Formats <paramref name="frame"/> for display in the monitor's frame log.
    /// </summary>
    /// <param name="frame">The frame to format.</param>
    /// <returns>A single line describing the frame.</returns>
    public static string Format(MicroGateFrame frame)
    {
        string timestamp = frame.Timestamp.ToString("HH:mm:ss.fff");

        if (frame.Kind == MicroGateFrameKind.Malformed)
        {
            return $"{timestamp}  MALFORMED  raw=[{ToHex(frame.Raw)}]  {frame.ErrorMessage}";
        }

        string sequence = (frame.SendSequence, frame.ReceiveSequence) switch
        {
            (int sendSequence, int receiveSequence) => $"  N(S)={sendSequence} N(R)={receiveSequence}",
            (null, int receiveSequence) => $"  N(R)={receiveSequence}",
            _ => string.Empty,
        };
        string pollFinal = frame.PollFinal ? "  P/F" : string.Empty;
        string payload = frame.Payload.IsEmpty ? string.Empty : $"  payload=[{ToHex(frame.Payload)}] ({frame.Payload.Length} bytes)";

        return $"{timestamp}  {Abbreviate(frame.Kind),-4}  addr=0x{frame.Address:X2}{sequence}{pollFinal}{payload}";
    }

    private static string Abbreviate(MicroGateFrameKind kind) => kind switch
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

    private static string ToHex(ReadOnlyMemory<byte> data)
    {
        ReadOnlySpan<byte> shown = data.Span[..Math.Min(data.Length, maxDisplayedBytes)];
        string hex = string.Join(' ', shown.ToArray().Select(value => value.ToString("X2")));
        return data.Length > maxDisplayedBytes ? $"{hex} ..." : hex;
    }
}
