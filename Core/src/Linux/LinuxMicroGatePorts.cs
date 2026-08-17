namespace BlueHeighliner.MicroGate.Linux;

/// <summary>
/// Enumerates the MicroGate SyncLink tty devices present under <c>/dev</c> on Linux: PCI/PCIe adapter ports (<c>/dev/ttySLGx</c>) and USB adapter ports (<c>/dev/ttyUSBx</c>) whose USB vendor ID identifies them as MicroGate devices.
/// </summary>
internal static class LinuxMicroGatePorts
{
    private const string PciDeviceSearchPattern = "ttySLG*";
    private const string UsbDeviceSearchPattern = "ttyUSB*";
    private const string MicroGateUsbVendorId = "2618";
    private const string SysClassTtyPath = "/sys/class/tty";

    /// <summary>
    /// Gets the names of the MicroGate SyncLink devices present on the local machine.
    /// </summary>
    /// <returns>The available port names.</returns>
    public static ValueTask<IReadOnlyList<string>> GetPorts()
    {
        List<string> ports = [];

        if (Directory.Exists("/dev"))
        {
            ports.AddRange(Directory.EnumerateFiles("/dev", PciDeviceSearchPattern).Select(Path.GetFileName)!);
            ports.AddRange(Directory.EnumerateFiles("/dev", UsbDeviceSearchPattern).Select(Path.GetFileName).Where(IsMicroGateUsbDevice)!);
        }

        ports.Sort(StringComparer.Ordinal);
        return ValueTask.FromResult<IReadOnlyList<string>>(ports);
    }

    private static bool IsMicroGateUsbDevice(string? name) =>
        name is not null && string.Equals(FindAncestorFile(Path.Combine(SysClassTtyPath, name, "device"), "idVendor")?.Trim(), MicroGateUsbVendorId, StringComparison.OrdinalIgnoreCase);

    private static string? FindAncestorFile(string startPath, string fileName)
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

    private static string? ResolveRealPath(string path)
    {
        if (!Directory.Exists(path) && !File.Exists(path))
        {
            return null;
        }

        return Directory.ResolveLinkTarget(path, returnFinalTarget: true)?.FullName ?? Path.GetFullPath(path);
    }
}
