namespace BlueHeighliner.MicroGate;

/// <summary>
/// Formats a <see cref="MicroGateFrame"/> as a detailed byte dump: every raw byte in decimal (0-255) alongside its ASCII rendering, shown inline under the frame's row in the monitor's log when that row is expanded.
/// </summary>
internal static class FrameDetailFormatter
{
    private static readonly int bytesPerRow = 16;

    /// <summary>
    /// Formats <paramref name="frame"/> for display underneath its expanded row.
    /// </summary>
    /// <param name="frame">The frame to format.</param>
    /// <returns>A decimal and ASCII dump of every raw byte.</returns>
    public static string Format(MicroGateFrame frame)
    {
        StringBuilder builder = new();

        ReadOnlySpan<byte> raw = frame.Raw.Span;
        if (raw.IsEmpty)
        {
            builder.Append("No bytes.");
            return builder.ToString();
        }

        builder.Append("OFFSET  ");
        builder.Append("DECIMAL BYTES (0-255)".PadRight((bytesPerRow * 4) + 1));
        builder.AppendLine("ASCII");

        for (int offset = 0; offset < raw.Length; offset += bytesPerRow)
        {
            int count = Math.Min(bytesPerRow, raw.Length - offset);
            builder.Append(offset.ToString("D6")).Append("  ");

            for (int column = 0; column < bytesPerRow; column++)
            {
                builder.Append(column < count ? raw[offset + column].ToString("D3") : "   ");
                builder.Append(' ');
            }

            for (int column = 0; column < count; column++)
            {
                builder.Append(ToAsciiChar(raw[offset + column]));
            }

            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static char ToAsciiChar(byte value) => value is >= 0x20 and <= 0x7E ? (char)value : '.';
}
