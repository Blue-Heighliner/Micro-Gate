namespace BlueHeighliner.MicroGate.Windows;

/// <summary>
/// Enumerates the MicroGate SyncLink devices installed on the local machine via <c>MgslEnumeratePorts</c>.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class WindowsMicroGatePorts
{
    /// <summary>
    /// Gets the names of the MicroGate SyncLink devices present on the local machine.
    /// </summary>
    /// <returns>The available port names.</returns>
    public static unsafe ValueTask<IReadOnlyList<string>> GetPorts()
    {
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
