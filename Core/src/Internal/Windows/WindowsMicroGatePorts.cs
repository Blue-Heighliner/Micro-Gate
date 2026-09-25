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
    /// <exception cref="PlatformNotSupportedException">The current operating system is not Windows.</exception>
    ValueTask<IReadOnlyList<string>> GetPorts();
}

/// <summary>
/// Enumerates the installed devices via <c>MgslEnumeratePorts</c>.
/// </summary>
internal sealed class WindowsMicroGatePorts : IWindowsMicroGatePorts
{
    /// <inheritdoc />
    public unsafe ValueTask<IReadOnlyList<string>> GetPorts()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("The Windows MicroGate transport is only supported on Windows.");
        }

        MghdlcPort[] buffer = new MghdlcPort[MghdlcConstants.MaxPorts];

        fixed (MghdlcPort* ports = buffer)
        {
            uint bufferSize = (uint)(buffer.Length * sizeof(MghdlcPort));
            Mghdlc.MgslEnumeratePorts(ports, bufferSize, out uint portCount);

            List<string> names = new((int)portCount);
            for (int i = 0; i < portCount; i++)
            {
                names.Add(buffer[i].GetDeviceName());
            }

            names.Sort(StringComparer.Ordinal);
            return ValueTask.FromResult<IReadOnlyList<string>>(names);
        }
    }
}
