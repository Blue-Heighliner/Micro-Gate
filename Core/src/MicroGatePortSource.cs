namespace BlueHeighliner.MicroGate;

/// <summary>
/// Enumerates the MicroGate SyncLink device ports available on the local machine.
/// </summary>
public interface IMicroGatePortSource
{
    /// <summary>
    /// Gets the names of the serial ports with MicroGate SyncLink devices attached.
    /// </summary>
    /// <param name="cancellationToken">A token that can be used to cancel the enumeration operation.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> that completes with the names of the available ports.</returns>
    ValueTask<IReadOnlyList<string>> GetPorts(CancellationToken cancellationToken = default);
}
