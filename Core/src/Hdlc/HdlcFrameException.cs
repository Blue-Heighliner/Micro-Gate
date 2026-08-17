namespace BlueHeighliner.MicroGate.Hdlc;

/// <summary>
/// The exception thrown when raw bytes cannot be parsed as a well-formed HDLC frame.
/// </summary>
internal sealed class HdlcFrameException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HdlcFrameException"/> class.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public HdlcFrameException(string message)
        : base(message)
    {
    }
}
