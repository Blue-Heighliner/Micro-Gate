namespace BlueHeighliner.MicroGate;

public sealed class MicroGatePortSourceTests
{
    private readonly Mock<ILinuxMicroGatePorts> linuxPorts = new();
    private readonly Mock<IWindowsMicroGatePorts> windowsPorts = new();

    [Fact]
    public async Task GetPorts_OnLinux_DelegatesToLinuxPorts()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        IReadOnlyList<string> expected = ["ttySLG0"];
        linuxPorts.Setup(x => x.GetPorts()).Returns(new ValueTask<IReadOnlyList<string>>(expected));
        MicroGatePortSource source = new(linuxPorts.Object, windowsPorts.Object);

        IReadOnlyList<string> result = await source.GetPorts();

        Assert.Same(expected, result);
        windowsPorts.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetPorts_OnWindows_DelegatesToWindowsPorts()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        IReadOnlyList<string> expected = ["COM3"];
        windowsPorts.Setup(x => x.GetPorts()).Returns(new ValueTask<IReadOnlyList<string>>(expected));
        MicroGatePortSource source = new(linuxPorts.Object, windowsPorts.Object);

        IReadOnlyList<string> result = await source.GetPorts();

        Assert.Same(expected, result);
        linuxPorts.VerifyNoOtherCalls();
    }

    [Fact]
    public void GetPorts_OnUnsupportedPlatform_Throws()
    {
        if (OperatingSystem.IsLinux() || OperatingSystem.IsWindows())
        {
            return;
        }

        MicroGatePortSource source = new(linuxPorts.Object, windowsPorts.Object);

        Assert.Throws<PlatformNotSupportedException>(() => source.GetPorts());
    }
}
