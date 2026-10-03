namespace BlueHeighliner.MicroGate;

/// <summary>
/// The lifecycle state of an <see cref="IHdlcPeer"/>. A peer only moves forward: it is never returned to an earlier state.
/// </summary>
public enum HdlcPeerState
{
    /// <summary>
    /// The peer has been created but <see cref="IHdlcPeer.Start"/> has not completed.
    /// </summary>
    Idle,

    /// <summary>
    /// <see cref="IHdlcPeer.Start"/> has completed: the device is open and being read, but no connection is being formed.
    /// </summary>
    Ready,

    /// <summary>
    /// <see cref="IHdlcPeer.Connect"/> has been called and the link with the remote peer is not yet established.
    /// </summary>
    Connecting,

    /// <summary>
    /// The connection is established and data can be exchanged.
    /// </summary>
    Connected,

    /// <summary>
    /// The peer is finished: it was disconnected by the remote peer, lost its device, failed to connect, or was disposed.
    /// </summary>
    Disconnected,
}
