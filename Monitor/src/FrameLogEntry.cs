namespace BlueHeighliner.MicroGate;

/// <summary>
/// One row in the monitor's frame log: either a received frame, or a plain status message with no frame behind it. A frame row can be expanded to show <see cref="Detail"/> underneath it.
/// </summary>
/// <param name="text">The row's display text.</param>
/// <param name="frame">The frame the row reports, or <see langword="null"/> for a status message.</param>
internal sealed class FrameLogEntry(string text, MicroGateFrame? frame) : INotifyPropertyChanged
{
    private bool isExpanded;

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Gets the row's display text.
    /// </summary>
    public string Text { get; } = text;

    /// <summary>
    /// Gets the frame the row reports, or <see langword="null"/> for a status message.
    /// </summary>
    public MicroGateFrame? Frame { get; } = frame;

    /// <summary>
    /// Gets the byte-level detail text shown when the row is expanded, empty for a status message.
    /// </summary>
    public string Detail => Frame is { } value ? FrameDetailFormatter.Format(value) : string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the row's <see cref="Detail"/> is shown.
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
