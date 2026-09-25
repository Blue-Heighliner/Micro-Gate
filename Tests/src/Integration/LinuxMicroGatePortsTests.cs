namespace BlueHeighliner.MicroGate;

public sealed class LinuxMicroGatePortsTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "microgate-" + Guid.NewGuid().ToString("N"));
    private readonly string devicePath;
    private readonly string sysClassTtyPath;

    public LinuxMicroGatePortsTests()
    {
        devicePath = Path.Combine(root, "dev");
        sysClassTtyPath = Path.Combine(root, "sys", "class", "tty");
        Directory.CreateDirectory(devicePath);
        Directory.CreateDirectory(sysClassTtyPath);
    }

    public void Dispose() => Directory.Delete(root, true);

    [Fact]
    public async Task GetPorts_ReturnsPciPortsAndMicroGateUsbPortsSorted()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        AddDevice("ttySLG1");
        AddDevice("ttySLG0");
        AddDevice("ttyS0");
        AddUsbDevice("ttyUSB1", "2618\n");
        AddUsbDevice("ttyUSB0", "2618");
        AddUsbDevice("ttyUSB2", "1234");
        AddDevice("ttyUSB3");

        IReadOnlyList<string> ports = await new LinuxMicroGatePorts(devicePath, sysClassTtyPath).GetPorts();

        Assert.Equal(["ttySLG0", "ttySLG1", "ttyUSB0", "ttyUSB1"], ports);
    }

    [Fact]
    public async Task GetPorts_FindsVendorFileInAncestorOfDeviceLink()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        AddUsbDevice("ttyUSB0", "2618", "usb1/1-1/1-1:1.0/ttyUSB0");

        IReadOnlyList<string> ports = await new LinuxMicroGatePorts(devicePath, sysClassTtyPath).GetPorts();

        Assert.Equal(["ttyUSB0"], ports);
    }

    [Fact]
    public async Task GetPorts_WhenDeviceFolderMissing_ReturnsEmpty()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        IReadOnlyList<string> ports = await new LinuxMicroGatePorts(Path.Combine(root, "missing"), sysClassTtyPath).GetPorts();

        Assert.Empty(ports);
    }

    [Fact]
    public async Task GetPorts_WithDefaultConstructor_DoesNotThrow()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        IReadOnlyList<string> ports = await new LinuxMicroGatePorts().GetPorts();

        Assert.NotNull(ports);
    }

    private void AddDevice(string name) => File.WriteAllText(Path.Combine(devicePath, name), string.Empty);

    private void AddUsbDevice(string name, string vendor, string hierarchy = "usb1/1-1")
    {
        AddDevice(name);

        string vendorFolder = Path.Combine(root, "sys", "devices", name, hierarchy.Split('/')[0], "1-1");
        string target = Path.Combine(root, "sys", "devices", name, hierarchy);
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(vendorFolder, "idVendor"), vendor);

        string ttyFolder = Path.Combine(sysClassTtyPath, name);
        Directory.CreateDirectory(ttyFolder);
        Directory.CreateSymbolicLink(Path.Combine(ttyFolder, "device"), Path.GetRelativePath(ttyFolder, target));
    }
}
