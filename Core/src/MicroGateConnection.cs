namespace BlueHeighliner.MicroGate;

/// <summary>
/// Represents an open connection to a MicroGate SyncLink device over which HDLC frames are exchanged in asynchronous balanced mode.
/// </summary>
public interface IMicroGateConnection : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Occurs when the connection has been disconnected.
    /// </summary>
    event EventHandler? Disconnected;

    /// <summary>
    /// Occurs when a span of data is received across the connection as an HDLC frame.
    /// </summary>
    /// <remarks>
    /// The <see cref="IMemoryOwner{T}"/> is owned by the subscriber upon receipt of the event and must be disposed once the data is no longer needed, to support pooled memory.
    /// </remarks>
    event EventHandler<IMemoryOwner<byte>>? Received;

    /// <summary>
    /// Gets a value indicating whether the connection is currently connected.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Sends a span of data across the connection as an HDLC frame.
    /// </summary>
    /// <param name="data">The data to send.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the send operation.</param>
    /// <returns>A <see cref="ValueTask"/> that completes once the data has been sent.</returns>
    ValueTask Send(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a span of pooled data across the connection as an HDLC frame.
    /// </summary>
    /// <param name="data">The pooled data to send. Ownership is transferred to the connection, which disposes it once the data has been sent.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the send operation.</param>
    /// <returns>A <see cref="ValueTask"/> that completes once the data has been sent.</returns>
    ValueTask Send(IMemoryOwner<byte> data, CancellationToken cancellationToken = default);
}
