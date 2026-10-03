namespace BlueHeighliner.MicroGate;

public sealed class StartupParserTests
{
    private readonly StartupParser parser = new();

    [Fact]
    public void Parse_WithoutArguments_OpensInHdlcPeerMode()
    {
        StartupOptions options = parser.Parse([]);

        Assert.Equal(ControllerMode.HdlcPeer, options.InitialMode);
        Assert.Null(options.Problem);
    }

    [Theory]
    [InlineData("HdlcMonitor", "HdlcMonitor")]
    [InlineData("hdlcmonitor", "HdlcMonitor")]
    [InlineData("HDLC Passthrough", "HdlcPassthrough")]
    [InlineData("hdlc-peer", "HdlcPeer")]
    [InlineData("UartPeer", "UartPeer")]
    [InlineData("uart_monitor", "UartMonitor")]
    [InlineData("UART PASSTHROUGH", "UartPassthrough")]
    public void Parse_ModeAsASeparateArgument_SetsTheInitialMode(string value, string expected)
    {
        StartupOptions options = parser.Parse(["--mode", value]);

        Assert.Equal(Enum.Parse<ControllerMode>(expected), options.InitialMode);
        Assert.Null(options.Problem);
    }

    [Fact]
    public void Parse_ModeWithAnEqualsSign_SetsTheInitialMode() =>
        Assert.Equal(ControllerMode.HdlcPassthrough, parser.Parse(["--mode=HdlcPassthrough"]).InitialMode);

    [Fact]
    public void Parse_IgnoresUnrelatedArguments() =>
        Assert.Equal(ControllerMode.HdlcMonitor, parser.Parse(["--other", "--mode", "HdlcMonitor", "extra"]).InitialMode);

    [Theory]
    [InlineData("Nonsense")]
    [InlineData("Peer")]
    [InlineData("Monitor")]
    [InlineData("1")]
    [InlineData("7")]
    [InlineData("")]
    public void Parse_WithAnUnknownMode_FallsBackToHdlcPeerAndReportsAProblem(string value)
    {
        StartupOptions options = parser.Parse(["--mode", value]);

        Assert.Equal(ControllerMode.HdlcPeer, options.InitialMode);
        Assert.Contains("Unknown mode", options.Problem);
    }

    [Fact]
    public void Parse_WithAModeOptionButNoValue_ReportsAProblem() =>
        Assert.NotNull(parser.Parse(["--mode"]).Problem);

    [Fact]
    public void Parse_Addresses_SetsLocalAndRemote()
    {
        StartupOptions options = parser.Parse(["--local", "45", "--remote=19", "--mode", "HdlcPeer"]);

        Assert.Equal((byte)45, options.LocalAddress);
        Assert.Equal((byte)19, options.RemoteAddress);
        Assert.Null(options.Problem);
    }

    [Fact]
    public void Parse_WithoutAddresses_LeavesThemUnset()
    {
        StartupOptions options = parser.Parse(["--mode", "HdlcMonitor"]);

        Assert.Null(options.LocalAddress);
        Assert.Null(options.RemoteAddress);
    }

    [Theory]
    [InlineData("256")]
    [InlineData("-1")]
    [InlineData("0x10")]
    [InlineData("abc")]
    [InlineData("")]
    public void Parse_WithAnInvalidAddress_ReportsAProblemAndKeepsTheRest(string value)
    {
        StartupOptions options = parser.Parse(["--local", value, "--remote", "3", "--mode", "HdlcMonitor"]);

        Assert.Null(options.LocalAddress);
        Assert.Equal((byte)3, options.RemoteAddress);
        Assert.Equal(ControllerMode.HdlcMonitor, options.InitialMode);
        Assert.Contains("local address", options.Problem);
    }

    [Fact]
    public void Parse_WithSeveralProblems_ReportsAll()
    {
        StartupOptions options = parser.Parse(["--mode", "x", "--remote", "300"]);

        Assert.Contains("Unknown mode", options.Problem);
        Assert.Contains("remote address", options.Problem);
    }
}
