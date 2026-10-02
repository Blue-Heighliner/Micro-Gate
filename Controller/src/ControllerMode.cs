namespace BlueHeighliner.MicroGate;

/// <summary>
/// How the controller uses the MicroGate ports it opens.
/// </summary>
internal enum ControllerMode
{
    /// <summary>
    /// Opens one port and forms an HDLC connection with a remote peer, so data can be sent and received.
    /// </summary>
    Peer,

    /// <summary>
    /// Opens one port and only observes the frames received on it, forming no connection and sending nothing.
    /// </summary>
    Monitor,

    /// <summary>
    /// Opens two ports and relays every frame received on each to the other, as if the controller were not between them, logging the frames in both directions.
    /// </summary>
    Passthrough,
}
