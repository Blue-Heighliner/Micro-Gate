namespace BlueHeighliner.MicroGate;

public sealed class WindowsMicroGatePortsTests
{
    private readonly Mock<IWindowsNative> native = new();

    [Fact]
    public async Task GetPorts_ReturnsDecodedNamesSortedOrdinally()
    {
        native.Setup(x => x.EnumeratePorts()).Returns([MakePort("MGHDLC2"), MakePort("MGHDLC10"), MakePort("MGHDLC1")]);
        WindowsMicroGatePorts ports = new(native.Object);

        IReadOnlyList<string> names = await ports.GetPorts();

        Assert.Equal(["MGHDLC1", "MGHDLC10", "MGHDLC2"], names);
    }

    [Fact]
    public async Task GetPorts_WithNoPorts_ReturnsEmpty()
    {
        native.Setup(x => x.EnumeratePorts()).Returns([]);
        WindowsMicroGatePorts ports = new(native.Object);

        Assert.Empty(await ports.GetPorts());
    }

    [Fact]
    public void GetDeviceName_StopsAtNullTerminator()
    {
        MghdlcPort port = MakePort("AB");

        Assert.Equal("AB", port.GetDeviceName());
    }

    [Fact]
    public void GetDeviceName_WithFullBuffer_ReturnsAllCharacters()
    {
        string name = new('X', 25);

        Assert.Equal(name, MakePort(name).GetDeviceName());
    }

    private unsafe MghdlcPort MakePort(string name)
    {
        MghdlcPort port = new();
        for (int i = 0; i < name.Length; i++)
        {
            port.DeviceName[i] = (byte)name[i];
        }

        return port;
    }
}
