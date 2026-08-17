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
    /// <exception cref="IOException">The device could not be opened, or an asynchronous balanced mode connection could not be established.</exception>
    /// <exception cref="PlatformNotSupportedException">The current operating system is neither Windows nor Linux.</exception>
    ValueTask<IMicroGateConnection> Connect(string portName, CancellationToken cancellationToken = default);
}

/// <summary>
/// <inheritdoc cref="IMicroGateConnector" />
/// </summary>
/// <param name="options">The HDLC station configuration applied to every connection this connector opens.</param>
public sealed class MicroGateConnector(HdlcStationOptions options) : IMicroGateConnector
{
    /// <inheritdoc />
    public ValueTask<IMicroGateConnection> Connect(string portName, CancellationToken cancellationToken = default)
    {
        if (OperatingSystem.IsWindows())
        {
            return WindowsMicroGateConnection.Connect(portName, options, cancellationToken);
        }

        if (OperatingSystem.IsLinux())
        {
            return LinuxMicroGateConnection.Connect(portName, options, cancellationToken);
        }

        throw new PlatformNotSupportedException("MicroGate SyncLink devices are only supported on Windows and Linux.");
    }
}
