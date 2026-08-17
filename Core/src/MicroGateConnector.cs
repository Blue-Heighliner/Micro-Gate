namespace BlueHeighliner.MicroGate;

/// <summary>
/// Forms connections to MicroGate SyncLink devices.
/// </summary>
public interface IMicroGateConnector
{
    /// <summary>
    /// Opens a connection to the MicroGate SyncLink device attached to the specified serial port.
    /// </summary>
    /// <param name="portName">The name of the serial port the device is attached to.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the connect operation.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> that completes with the opened <see cref="IMicroGateConnection"/>.</returns>
    ValueTask<IMicroGateConnection> Connect(string portName, CancellationToken cancellationToken = default);
}
