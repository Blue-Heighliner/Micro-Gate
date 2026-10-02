namespace BlueHeighliner.MicroGate;

/// <summary>
/// One row in the controller's log: data sent or received, or a plain status message with no data behind it. A data row can be expanded to show its bytes as a <see cref="ByteGrid"/> underneath it.
/// </summary>
/// <param name="text">The row's display text.</param>
/// <param name="data">The row's data, or <see langword="null"/> for a status message.</param>
internal sealed class LogEntry(string text, byte[]? data) : INotifyPropertyChanged
{
    private bool isExpanded;

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Gets the row's display text.
    /// </summary>
    public string Text { get; } = text;

    /// <summary>
    /// Gets a value indicating whether the row carries data, as opposed to being a status message.
    /// </summary>
    public bool HasData => data is not null;

    /// <summary>
    /// Gets the cells of the row's data, which keep their per-cell display choice while the row exists. Empty for a status message.
    /// </summary>
    public List<ByteCell> Cells { get; } = [.. (data ?? []).Select(value => new ByteCell { Value = value })];

    /// <summary>
    /// Gets or sets a value indicating whether the row's data is shown.
    /// </summary>
    public bool IsExpanded
    {
        get => isExpanded;
        set
        {
            if (isExpanded == value)
            {
                return;
            }

            isExpanded = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExpanded)));
        }
    }
}
