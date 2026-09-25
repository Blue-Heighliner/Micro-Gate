namespace BlueHeighliner.MicroGate;

public sealed class MicroGateConnectorTests
{
    private readonly MicroGateConnectionOptions options = new() { Address = 0x11, DisablePollFinalBit = true, Encoding = MicroGateEncoding.NrziSpace, Crc = MicroGateCrc.Crc32Ccitt };
    private readonly Mock<ILinuxMicroGateConnector> linuxConnector = new();
    private readonly Mock<IWindowsMicroGateConnector> windowsConnector = new();
    private readonly Mock<IMicroGateConnection> connection = new();

    [Fact]
    public async Task Connect_OnLinux_DelegatesToLinuxConnector()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using CancellationTokenSource cancellation = new();
        linuxConnector.Setup(x => x.Connect("ttySLG0", options, cancellation.Token)).Returns(new ValueTask<IMicroGateConnection>(connection.Object));
        MicroGateConnector connector = new(linuxConnector.Object, windowsConnector.Object);

        IMicroGateConnection result = await connector.Connect("ttySLG0", options, cancellation.Token);

        Assert.Same(connection.Object, result);
        windowsConnector.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Connect_OnWindows_DelegatesToWindowsConnector()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        windowsConnector.Setup(x => x.Connect("COM3", options, CancellationToken.None)).Returns(new ValueTask<IMicroGateConnection>(connection.Object));
        MicroGateConnector connector = new(linuxConnector.Object, windowsConnector.Object);

        IMicroGateConnection result = await connector.Connect("COM3", options);

        Assert.Same(connection.Object, result);
        linuxConnector.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Connect_WithoutOptions_UsesDefaults()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        MicroGateConnectionOptions? captured = null;
        linuxConnector
            .Setup(x => x.Connect("ttySLG0", It.IsAny<MicroGateConnectionOptions>(), CancellationToken.None))
            .Callback<string, MicroGateConnectionOptions, CancellationToken>((_, passed, _) => captured = passed)
            .Returns(new ValueTask<IMicroGateConnection>(connection.Object));
        MicroGateConnector connector = new(linuxConnector.Object, windowsConnector.Object);

        await connector.Connect("ttySLG0");

        Assert.Equal(new MicroGateConnectionOptions(), captured);
        Assert.False(captured!.DisablePollFinalBit);
        Assert.Equal(MicroGateEncoding.Nrz, captured.Encoding);
        Assert.Equal(MicroGateCrc.Crc16Ccitt, captured.Crc);
        Assert.Equal(MicroGateIdlePattern.Flags, captured.IdlePattern);
        Assert.Null(captured.HardwareAddressFilter);
    }

    [Fact]
    public void Connect_OnUnsupportedPlatform_Throws()
    {
        if (OperatingSystem.IsLinux() || OperatingSystem.IsWindows())
        {
            return;
        }

        MicroGateConnector connector = new(linuxConnector.Object, windowsConnector.Object);

        Assert.Throws<PlatformNotSupportedException>(() => connector.Connect("port"));
    }
}
