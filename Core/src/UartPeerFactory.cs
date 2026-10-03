namespace BlueHeighliner.MicroGate;

/// <summary>
/// Creates idle UART peers. A peer is single use, so code that needs a new link on demand, such as a service built by a container, depends on this factory instead of holding a peer.
/// </summary>
public interface IUartPeerFactory
{
    /// <summary>
    /// Creates a new, idle UART peer.
    /// </summary>
    /// <returns>A peer that has not been started.</returns>
    IUartPeer Create();
}

/// <inheritdoc />
public sealed class UartPeerFactory : IUartPeerFactory
{
    /// <inheritdoc />
    public IUartPeer Create() => new UartPeer();
}
