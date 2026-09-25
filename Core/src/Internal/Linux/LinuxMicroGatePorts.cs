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
/// Enumerates PCI/PCIe adapter ports (<c>/dev/ttySLGx</c>) and USB adapter ports. USB adapters appear as <c>/dev/ttyUSBx</c>, which is shared with unrelated USB serial devices, so they are recognized by the udev alias the driver documentation describes: an entry in <c>/dev/serial/by-id</c> named <c>usb-MicroGate_...</c> that links to the device.
/// </summary>
internal sealed class LinuxMicroGatePorts : ILinuxMicroGatePorts
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LinuxMicroGatePorts"/> class that enumerates the real <c>/dev</c> and <c>/dev/serial/by-id</c> folders.
    /// </summary>
    public LinuxMicroGatePorts()
        : this("/dev", "/dev/serial/by-id")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LinuxMicroGatePorts"/> class that enumerates the specified folders.
    /// </summary>
    /// <param name="devicePath">The folder containing the tty device nodes.</param>
    /// <param name="serialByIdPath">The folder containing the udev serial number aliases, in the style of <c>/dev/serial/by-id</c>.</param>
    public LinuxMicroGatePorts(string devicePath, string serialByIdPath)
    {
        this.devicePath = devicePath;
        this.serialByIdPath = serialByIdPath;
    }

    private readonly string devicePath;
    private readonly string serialByIdPath;
    private readonly string pciDeviceSearchPattern = "ttySLG*";
    private readonly string usbAliasPrefix = "usb-MicroGate_";

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<string>> GetPorts()
    {
        SortedSet<string> ports = new(StringComparer.Ordinal);

        if (Directory.Exists(devicePath))
        {
            foreach (string path in Directory.EnumerateFiles(devicePath, pciDeviceSearchPattern))
            {
                ports.Add(Path.GetFileName(path));
            }
        }

        if (Directory.Exists(serialByIdPath))
        {
            foreach (string alias in Directory.EnumerateFileSystemEntries(serialByIdPath, usbAliasPrefix + "*"))
            {
                string? name = ResolveDeviceName(alias);
                if (name is not null)
                {
                    ports.Add(name);
                }
            }
        }

        return ValueTask.FromResult<IReadOnlyList<string>>([.. ports]);
    }

    private string? ResolveDeviceName(string alias)
    {
        FileSystemInfo? target = File.ResolveLinkTarget(alias, returnFinalTarget: true);
        if (target is null || !File.Exists(target.FullName))
        {
            return null;
        }

        return Path.GetFileName(target.FullName);
    }
}
