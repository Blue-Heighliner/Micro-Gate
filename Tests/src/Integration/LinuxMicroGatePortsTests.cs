namespace BlueHeighliner.MicroGate;

public sealed class LinuxMicroGatePortsTests : IDisposable
{
    public LinuxMicroGatePortsTests()
    {
        devicePath = Path.Combine(root, "dev");
        serialByIdPath = Path.Combine(root, "dev", "serial", "by-id");
        Directory.CreateDirectory(devicePath);
    }

    private readonly string root = Path.Combine(Path.GetTempPath(), "microgate-" + Guid.NewGuid().ToString("N"));
    private readonly string devicePath;
    private readonly string serialByIdPath;

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
        AddDevice("ttyUSB0");
        AddDevice("ttyUSB1");
        AddDevice("ttyUSB2");
        AddAlias("usb-MicroGate_SyncLink_USB_1U3-12477-if00-port0", "ttyUSB1");
        AddAlias("usb-MicroGate_SyncLink_USB_1U3-99999-if00-port0", "ttyUSB0");
        AddAlias("usb-FTDI_FT232R_USB_UART_A1234-if00-port0", "ttyUSB2");

        IReadOnlyList<string> ports = await new LinuxMicroGatePorts(devicePath, serialByIdPath).GetPorts();

        Assert.Equal(["ttySLG0", "ttySLG1", "ttyUSB0", "ttyUSB1"], ports);
    }

    [Fact]
    public async Task GetPorts_IgnoresAliasWhoseDeviceIsGoneAndDuplicates()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        AddDevice("ttyUSB0");
        AddAlias("usb-MicroGate_SyncLink_USB_A-if00-port0", "ttyUSB0");
        AddAlias("usb-MicroGate_SyncLink_USB_B-if00-port0", "ttyUSB0");
        AddAlias("usb-MicroGate_SyncLink_USB_C-if00-port0", "ttyUSB7");

        IReadOnlyList<string> ports = await new LinuxMicroGatePorts(devicePath, serialByIdPath).GetPorts();

        Assert.Equal(["ttyUSB0"], ports);
    }

    [Fact]
    public async Task GetPorts_WithoutSerialAliasFolder_ReturnsOnlyPciPorts()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        AddDevice("ttySLG0");
        AddDevice("ttyUSB0");

        IReadOnlyList<string> ports = await new LinuxMicroGatePorts(devicePath, serialByIdPath).GetPorts();

        Assert.Equal(["ttySLG0"], ports);
    }

    [Fact]
    public async Task GetPorts_WhenDeviceFolderMissing_ReturnsEmpty()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        IReadOnlyList<string> ports = await new LinuxMicroGatePorts(Path.Combine(root, "missing"), Path.Combine(root, "missing", "by-id")).GetPorts();

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

    private void AddAlias(string alias, string device)
    {
        Directory.CreateDirectory(serialByIdPath);
        File.CreateSymbolicLink(Path.Combine(serialByIdPath, alias), Path.Combine(devicePath, device));
    }
}
