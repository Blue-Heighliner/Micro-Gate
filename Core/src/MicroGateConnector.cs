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
    /// <param name="options">The device and HDLC settings to apply, or <see langword="null"/> to use the defaults.</param>
    /// <param name="cancellation">A token that can be used to cancel the connect operation.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> that completes with the opened <see cref="IMicroGateConnection"/>.</returns>
    /// <exception cref="IOException">The device could not be opened, or an asynchronous balanced mode connection could not be established.</exception>
    /// <exception cref="PlatformNotSupportedException">The current operating system is neither Windows nor Linux.</exception>
    ValueTask<IMicroGateConnection> Connect(string portName, MicroGateConnectionOptions? options = null, CancellationToken cancellation = default);
}

/// <summary>
/// <inheritdoc cref="IMicroGateConnector" />
/// </summary>
public sealed class MicroGateConnector : IMicroGateConnector
{
    private readonly ILinuxMicroGateConnector linuxConnector;
    private readonly IWindowsMicroGateConnector windowsConnector;

    /// <summary>
    /// Initializes a new instance of the <see cref="MicroGateConnector"/> class.
    /// </summary>
    public MicroGateConnector()
        : this(new LinuxMicroGateConnector(new LinuxNative()), new WindowsMicroGateConnector(new WindowsNative()))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MicroGateConnector"/> class with the platform specific connectors to dispatch to.
    /// </summary>
    /// <param name="linuxConnector">The connector used on Linux.</param>
    /// <param name="windowsConnector">The connector used on Windows.</param>
    internal MicroGateConnector(ILinuxMicroGateConnector linuxConnector, IWindowsMicroGateConnector windowsConnector)
    {
        this.linuxConnector = linuxConnector;
        this.windowsConnector = windowsConnector;
    }

    /// <inheritdoc />
    public ValueTask<IMicroGateConnection> Connect(string portName, MicroGateConnectionOptions? options = null, CancellationToken cancellation = default)
    {
        options ??= new();

        if (OperatingSystem.IsWindows())
        {
            return windowsConnector.Connect(portName, options, cancellation);
        }

        if (OperatingSystem.IsLinux())
        {
            return linuxConnector.Connect(portName, options, cancellation);
        }

        throw new PlatformNotSupportedException("MicroGate SyncLink devices are only supported on Windows and Linux.");
    }
}
