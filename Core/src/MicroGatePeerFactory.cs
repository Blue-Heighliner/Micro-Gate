namespace BlueHeighliner.MicroGate;

/// <summary>
/// Creates <see cref="IMicroGatePeer"/>s. A peer can be started only once, so anything that needs a new link, such as a service resolved from a dependency injection container, takes this factory and creates a peer for each one instead of holding a single peer.
/// </summary>
public interface IMicroGatePeerFactory
{
    /// <summary>
    /// Creates a new, idle peer.
    /// </summary>
    /// <returns>The new peer, which the caller owns and must dispose.</returns>
    IMicroGatePeer Create();
}

/// <summary>
/// <inheritdoc cref="IMicroGatePeerFactory" />
/// </summary>
public sealed class MicroGatePeerFactory : IMicroGatePeerFactory
{
    /// <inheritdoc />
    public IMicroGatePeer Create() => new MicroGatePeer();
}
