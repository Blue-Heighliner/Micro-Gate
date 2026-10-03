namespace BlueHeighliner.MicroGate;

/// <summary>
/// The lifecycle state of an <see cref="IUartPeer"/>. An asynchronous link has no connection to form, so a peer is simply not yet open, open, or closed, and only moves forward.
/// </summary>
public enum UartPeerState
{
    /// <summary>
    /// The peer has been created but <see cref="IUartPeer.Start"/> has not completed.
    /// </summary>
    Idle,

    /// <summary>
    /// The device is open and configured: bytes can be sent and are received.
    /// </summary>
    Open,

    /// <summary>
    /// The peer is finished: it was disposed, lost its device, or failed to open it.
    /// </summary>
    Closed,
}
