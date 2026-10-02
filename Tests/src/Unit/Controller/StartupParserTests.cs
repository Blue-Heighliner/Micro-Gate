namespace BlueHeighliner.MicroGate;

public sealed class StartupParserTests
{
    private readonly StartupParser parser = new();

    [Fact]
    public void Parse_WithoutArguments_OpensInPeerMode()
    {
        StartupOptions options = parser.Parse([]);

        Assert.Equal(ControllerMode.Peer, options.InitialMode);
        Assert.Null(options.Problem);
    }

    [Theory]
    [InlineData("Monitor", "Monitor")]
    [InlineData("monitor", "Monitor")]
    [InlineData("PASSTHROUGH", "Passthrough")]
    [InlineData("Peer", "Peer")]
    public void Parse_ModeAsASeparateArgument_SetsTheInitialMode(string value, string expected)
    {
        StartupOptions options = parser.Parse(["--mode", value]);

        Assert.Equal(Enum.Parse<ControllerMode>(expected), options.InitialMode);
        Assert.Null(options.Problem);
    }

    [Fact]
    public void Parse_ModeWithAnEqualsSign_SetsTheInitialMode() =>
        Assert.Equal(ControllerMode.Passthrough, parser.Parse(["--mode=Passthrough"]).InitialMode);

    [Fact]
    public void Parse_IgnoresUnrelatedArguments() =>
        Assert.Equal(ControllerMode.Monitor, parser.Parse(["--other", "--mode", "Monitor", "extra"]).InitialMode);

    [Theory]
    [InlineData("Nonsense")]
    [InlineData("7")]
    [InlineData("")]
    public void Parse_WithAnUnknownMode_FallsBackToPeerAndReportsAProblem(string value)
    {
        StartupOptions options = parser.Parse(["--mode", value]);

        Assert.Equal(ControllerMode.Peer, options.InitialMode);
        Assert.Contains("Unknown mode", options.Problem);
    }

    [Fact]
    public void Parse_WithAModeOptionButNoValue_ReportsAProblem() =>
        Assert.NotNull(parser.Parse(["--mode"]).Problem);

    [Fact]
    public void Parse_Addresses_SetsLocalAndRemote()
    {
        StartupOptions options = parser.Parse(["--local", "45", "--remote=19", "--mode", "Peer"]);

        Assert.Equal((byte)45, options.LocalAddress);
        Assert.Equal((byte)19, options.RemoteAddress);
        Assert.Null(options.Problem);
    }

    [Fact]
    public void Parse_WithoutAddresses_LeavesThemUnset()
    {
        StartupOptions options = parser.Parse(["--mode", "Monitor"]);

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
        StartupOptions options = parser.Parse(["--local", value, "--remote", "3", "--mode", "Monitor"]);

        Assert.Null(options.LocalAddress);
        Assert.Equal((byte)3, options.RemoteAddress);
        Assert.Equal(ControllerMode.Monitor, options.InitialMode);
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
