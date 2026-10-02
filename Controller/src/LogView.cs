namespace BlueHeighliner.MicroGate;

/// <summary>
/// Which rows of the controller's log are shown in peer mode. Switching never clears the log: every row stays in it, and the view only decides which are shown.
/// </summary>
internal enum LogView
{
    /// <summary>
    /// The data received and sent, one row per message.
    /// </summary>
    Data,

    /// <summary>
    /// Every frame received and transmitted, with its HDLC fields.
    /// </summary>
    Frames,
}
