namespace BlueHeighliner.MicroGate;

/// <summary>
/// Saves and loads the controller's log as JSON.
/// </summary>
internal interface ILogSerializer
{
    /// <summary>
    /// Writes log rows to a stream.
    /// </summary>
    /// <param name="entries">The rows to save, in order.</param>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="cancellation">A token that can be used to cancel the operation.</param>
    /// <returns>A task that completes once the rows are written.</returns>
    Task Save(IEnumerable<LogEntry> entries, Stream stream, CancellationToken cancellation = default);

    /// <summary>
    /// Reads log rows from a stream written by <see cref="Save"/>.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="cancellation">A token that can be used to cancel the operation.</param>
    /// <returns>The rows, with their data and per-cell display choices restored.</returns>
    /// <exception cref="InvalidDataException">The stream is not a valid saved log.</exception>
    Task<IReadOnlyList<LogEntry>> Load(Stream stream, CancellationToken cancellation = default);
}

/// <inheritdoc />
internal sealed class LogSerializer : ILogSerializer
{
    private readonly JsonSerializerOptions options = new() { WriteIndented = true };

    /// <inheritdoc />
    public Task Save(IEnumerable<LogEntry> entries, Stream stream, CancellationToken cancellation = default)
    {
        SavedLog log = new()
        {
            Entries = [.. entries.Select(entry => new SavedLogEntry
            {
                Text = entry.Text,
                Fields = entry.Fields,
                Data = entry.HasData ? Convert.ToBase64String([.. entry.Cells.Select(cell => cell.Value)]) : null,
                RawCells = [.. entry.Cells.Index().Where(item => item.Item.ShowRaw).Select(item => item.Index)],
            })],
        };

        return JsonSerializer.SerializeAsync(stream, log, options, cancellation);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LogEntry>> Load(Stream stream, CancellationToken cancellation = default)
    {
        try
        {
            SavedLog log = await JsonSerializer.DeserializeAsync<SavedLog>(stream, options, cancellation).ConfigureAwait(false) ?? throw new InvalidDataException("The file is empty.");
            if (log.Version != 1)
            {
                throw new InvalidDataException($"Unsupported log version {log.Version}.");
            }

            return [.. log.Entries.Select(Restore)];
        }
        catch (Exception exception) when (exception is JsonException or FormatException)
        {
            throw new InvalidDataException("The file is not a valid saved log.", exception);
        }
    }

    private LogEntry Restore(SavedLogEntry saved)
    {
        LogEntry entry = new(saved.Text, saved.Data is null ? null : Convert.FromBase64String(saved.Data), saved.Fields);
        foreach (int index in saved.RawCells.Where(index => index >= 0 && index < entry.Cells.Count))
        {
            entry.Cells[index].ShowRaw = true;
        }

        return entry;
    }
}
