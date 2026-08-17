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
    /// <exception cref="PlatformNotSupportedException">The current operating system is neither Windows nor Linux.</exception>
    ValueTask<IReadOnlyList<string>> GetPorts(CancellationToken cancellationToken = default);
}

/// <summary>
/// <inheritdoc cref="IMicroGatePortSource" />
/// </summary>
public sealed class MicroGatePortSource : IMicroGatePortSource
{
    /// <inheritdoc />
    public ValueTask<IReadOnlyList<string>> GetPorts(CancellationToken cancellationToken = default)
    {
        if (OperatingSystem.IsWindows())
        {
            return WindowsMicroGatePorts.GetPorts();
        }

        if (OperatingSystem.IsLinux())
        {
            return LinuxMicroGatePorts.GetPorts();
        }

        throw new PlatformNotSupportedException("MicroGate SyncLink devices are only supported on Windows and Linux.");
    }
}
