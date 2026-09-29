namespace BlueHeighliner.MicroGate;

/// <summary>
/// Saves and loads the monitor's frame log as JSON.
/// </summary>
internal static class FrameLogSerializer
{
    private static readonly int currentVersion = 1;
    private static readonly JsonSerializerOptions options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>
    /// Writes <paramref name="entries"/> to <paramref name="destination"/> as JSON.
    /// </summary>
    /// <param name="entries">The log rows to save, in order.</param>
    /// <param name="destination">The stream to write to.</param>
    public static async Task Save(IEnumerable<FrameLogEntry> entries, Stream destination)
    {
        SavedFrameLog saved = new() { Version = currentVersion, Entries = [.. entries.Select(ToSaved)] };
        await JsonSerializer.SerializeAsync(destination, saved, options).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads a previously saved frame log from <paramref name="source"/>.
    /// </summary>
    /// <param name="source">The stream to read from.</param>
    /// <returns>The saved log rows, in their original order.</returns>
    /// <exception cref="JsonException">The stream did not contain a valid frame log.</exception>
    /// <exception cref="NotSupportedException">The file's schema version is not one this version of the monitor understands.</exception>
    public static async Task<IReadOnlyList<FrameLogEntry>> Load(Stream source)
    {
        SavedFrameLog saved = await JsonSerializer.DeserializeAsync<SavedFrameLog>(source, options).ConfigureAwait(false)
            ?? throw new JsonException("The file is empty.");

        if (saved.Version != currentVersion)
        {
            throw new NotSupportedException($"This file uses log format version {saved.Version}, which this version of the monitor does not understand (expected version {currentVersion}).");
        }

        return [.. saved.Entries.Select(FromSaved)];
    }

    private static SavedFrameLogEntry ToSaved(FrameLogEntry entry) => new()
    {
        Text = entry.Text,
        Frame = entry.Frame is { } frame
            ? new SavedMicroGateFrame
            {
                Timestamp = frame.Timestamp,
                Address = frame.Address,
                Kind = frame.Kind,
                PollFinal = frame.PollFinal,
                SendSequence = frame.SendSequence,
                ReceiveSequence = frame.ReceiveSequence,
                Payload = frame.Payload.ToArray(),
                Raw = frame.Raw.ToArray(),
                ErrorMessage = frame.ErrorMessage,
            }
            : null,
    };

    private static FrameLogEntry FromSaved(SavedFrameLogEntry saved)
    {
        MicroGateFrame? frame = saved.Frame is { } savedFrame
            ? new MicroGateFrame
            {
                Timestamp = savedFrame.Timestamp,
                Address = savedFrame.Address,
                Kind = savedFrame.Kind,
                PollFinal = savedFrame.PollFinal,
                SendSequence = savedFrame.SendSequence,
                ReceiveSequence = savedFrame.ReceiveSequence,
                Payload = savedFrame.Payload,
                Raw = savedFrame.Raw,
                ErrorMessage = savedFrame.ErrorMessage,
            }
            : null;

        return new FrameLogEntry(saved.Text, frame);
    }
}
