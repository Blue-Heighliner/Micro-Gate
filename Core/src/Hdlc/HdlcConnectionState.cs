namespace BlueHeighliner.MicroGate.Hdlc;

/// <summary>
/// Identifies the asynchronous balanced mode connection state of an <see cref="IHdlcStateMachine"/>.
/// </summary>
internal enum HdlcConnectionState
{
    /// <summary>
    /// No connection is established.
    /// </summary>
    Disconnected,

    /// <summary>
    /// A <see cref="HdlcFrameKind.SetAsynchronousBalancedMode"/> frame has been sent and a matching acknowledgement is awaited.
    /// </summary>
    Connecting,

    /// <summary>
    /// The connection is established and information frames may be exchanged.
    /// </summary>
    Connected,

    /// <summary>
    /// A <see cref="HdlcFrameKind.Disconnect"/> frame has been sent and a matching acknowledgement is awaited.
    /// </summary>
    Disconnecting,
}
