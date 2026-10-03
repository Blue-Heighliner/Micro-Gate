namespace BlueHeighliner.MicroGate;

/// <summary>
/// Creates <see cref="IHdlcPeer"/>s. A peer can be started only once, so anything that needs a new link, such as a service resolved from a dependency injection container, takes this factory and creates a peer for each one instead of holding a single peer.
/// </summary>
public interface IHdlcPeerFactory
{
    /// <summary>
    /// Creates a new, idle peer.
    /// </summary>
    /// <returns>The new peer, which the caller owns and must dispose.</returns>
    IHdlcPeer Create();
}

/// <summary>
/// <inheritdoc cref="IHdlcPeerFactory" />
/// </summary>
public sealed class HdlcPeerFactory : IHdlcPeerFactory
{
    /// <inheritdoc />
    public IHdlcPeer Create() => new HdlcPeer();
}
