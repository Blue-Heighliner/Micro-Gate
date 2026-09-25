namespace BlueHeighliner.MicroGate;

/// <summary>
/// Enumerates the MicroGate SyncLink device ports available on the local machine.
/// </summary>
public interface IMicroGatePortSource
{
    /// <summary>
    /// Gets the names of the serial ports with MicroGate SyncLink devices attached.
    /// </summary>
    /// <param name="cancellation">A token that can be used to cancel the enumeration operation.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> that completes with the names of the available ports.</returns>
    /// <exception cref="PlatformNotSupportedException">The current operating system is neither Windows nor Linux.</exception>
    /// <exception cref="IOException">The driver failed to enumerate the ports.</exception>
    ValueTask<IReadOnlyList<string>> GetPorts(CancellationToken cancellation = default);
}

/// <summary>
/// <inheritdoc cref="IMicroGatePortSource" />
/// </summary>
public sealed class MicroGatePortSource : IMicroGatePortSource
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MicroGatePortSource"/> class.
    /// </summary>
    public MicroGatePortSource()
        : this(new LinuxMicroGatePorts(), new WindowsMicroGatePorts(new WindowsNative()))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MicroGatePortSource"/> class with the platform specific port sources to dispatch to.
    /// </summary>
    /// <param name="linuxPorts">The port source used on Linux.</param>
    /// <param name="windowsPorts">The port source used on Windows.</param>
    internal MicroGatePortSource(ILinuxMicroGatePorts linuxPorts, IWindowsMicroGatePorts windowsPorts)
    {
        this.linuxPorts = linuxPorts;
        this.windowsPorts = windowsPorts;
    }

    private readonly ILinuxMicroGatePorts linuxPorts;
    private readonly IWindowsMicroGatePorts windowsPorts;

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<string>> GetPorts(CancellationToken cancellation = default)
    {
        if (OperatingSystem.IsWindows())
        {
            return new ValueTask<IReadOnlyList<string>>(Task.Run(() => windowsPorts.GetPorts().AsTask(), cancellation));
        }

        if (OperatingSystem.IsLinux())
        {
            return new ValueTask<IReadOnlyList<string>>(Task.Run(() => linuxPorts.GetPorts().AsTask(), cancellation));
        }

        throw new PlatformNotSupportedException("MicroGate SyncLink devices are only supported on Windows and Linux.");
    }
}
