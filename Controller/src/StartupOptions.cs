namespace BlueHeighliner.MicroGate;

/// <summary>
/// What the command line asks of the controller when it opens.
/// </summary>
internal sealed record StartupOptions
{
    /// <summary>
    /// Gets the mode the window opens in.
    /// </summary>
    public ControllerMode InitialMode { get; init; } = ControllerMode.Peer;

    /// <summary>
    /// Gets the local address to open with, or <see langword="null"/> to keep the default.
    /// </summary>
    public byte? LocalAddress { get; init; }

    /// <summary>
    /// Gets the remote address to open with, or <see langword="null"/> to keep the default.
    /// </summary>
    public byte? RemoteAddress { get; init; }

    /// <summary>
    /// Gets a description of what was wrong with the command line, or <see langword="null"/> if nothing was.
    /// </summary>
    public string? Problem { get; init; }
}
