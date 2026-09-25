namespace BlueHeighliner.MicroGate.Windows;

/// <summary>
/// Enumerates the MicroGate SyncLink devices installed on a Windows system.
/// </summary>
internal interface IWindowsMicroGatePorts
{
    /// <summary>
    /// Gets the names of the MicroGate SyncLink devices present on the local machine.
    /// </summary>
    /// <returns>A <see cref="ValueTask{TResult}"/> that completes with the available port names.</returns>
    ValueTask<IReadOnlyList<string>> GetPorts();
}

/// <summary>
/// Enumerates the installed devices via <c>MgslEnumeratePorts</c>.
/// </summary>
/// <param name="native">The native device operations.</param>
internal sealed class WindowsMicroGatePorts(IWindowsNative native) : IWindowsMicroGatePorts
{
    /// <inheritdoc />
    public ValueTask<IReadOnlyList<string>> GetPorts()
    {
        List<string> names = [.. native.EnumeratePorts().Select(port => port.GetDeviceName())];
        names.Sort(StringComparer.Ordinal);
        return ValueTask.FromResult<IReadOnlyList<string>>(names);
    }
}
