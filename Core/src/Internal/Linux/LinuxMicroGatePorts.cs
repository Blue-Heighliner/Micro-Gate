namespace BlueHeighliner.MicroGate.Linux;

/// <summary>
/// Enumerates the MicroGate SyncLink tty devices present under <c>/dev</c> on Linux.
/// </summary>
internal interface ILinuxMicroGatePorts
{
    /// <summary>
    /// Gets the names of the MicroGate SyncLink devices present on the local machine.
    /// </summary>
    /// <returns>A <see cref="ValueTask{TResult}"/> that completes with the available port names.</returns>
    ValueTask<IReadOnlyList<string>> GetPorts();
}

/// <summary>
/// Enumerates PCI/PCIe adapter ports (<c>/dev/ttySLGx</c>) and USB adapter ports (<c>/dev/ttyUSBx</c>) whose USB vendor ID identifies them as MicroGate devices.
/// </summary>
internal sealed class LinuxMicroGatePorts : ILinuxMicroGatePorts
{
    private readonly string pciDeviceSearchPattern = "ttySLG*";
    private readonly string usbDeviceSearchPattern = "ttyUSB*";
    private readonly string microGateUsbVendorId = "2618";
    private readonly string sysClassTtyPath = "/sys/class/tty";

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<string>> GetPorts()
    {
        List<string> ports = [];

        if (Directory.Exists("/dev"))
        {
            ports.AddRange(Directory.EnumerateFiles("/dev", pciDeviceSearchPattern).Select(Path.GetFileName)!);
            ports.AddRange(Directory.EnumerateFiles("/dev", usbDeviceSearchPattern).Select(Path.GetFileName).Where(IsMicroGateUsbDevice)!);
        }

        ports.Sort(StringComparer.Ordinal);
        return ValueTask.FromResult<IReadOnlyList<string>>(ports);
    }

    private bool IsMicroGateUsbDevice(string? name) =>
        name is not null && string.Equals(FindAncestorFile(Path.Combine(sysClassTtyPath, name, "device"), "idVendor")?.Trim(), microGateUsbVendorId, StringComparison.OrdinalIgnoreCase);

    private string? FindAncestorFile(string startPath, string fileName)
    {
        string? current = ResolveRealPath(startPath);

        while (current is not null && current != "/" && current != "/sys")
        {
            string candidate = Path.Combine(current, fileName);
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            current = Path.GetDirectoryName(current);
        }

        return null;
    }

    private string? ResolveRealPath(string path)
    {
        if (!Directory.Exists(path) && !File.Exists(path))
        {
            return null;
        }

        return Directory.ResolveLinkTarget(path, returnFinalTarget: true)?.FullName ?? Path.GetFullPath(path);
    }
}
