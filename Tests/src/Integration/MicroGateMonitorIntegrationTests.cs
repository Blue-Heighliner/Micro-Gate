namespace BlueHeighliner.MicroGate;

public sealed class MicroGateMonitorIntegrationTests
{
    private readonly TimeSpan timeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task Start_OnRealPseudoTerminal_ReportsFramesWrittenByThePeerAndNeverWritesBack()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using PseudoTerminal? terminal = PseudoTerminal.Create();
        if (terminal is null)
        {
            return;
        }

        await using MicroGateMonitor monitor = new(new LinuxMicroGateMonitorDeviceOpener(new TolerantLinuxNative(new LinuxNative())), new Mock<IMicroGateMonitorDeviceOpener>().Object);
        TestObserver<MicroGateFrame> frames = new();
        monitor.Received.Subscribe(frames);

        await monitor.Start(terminal.SlavePath).AsTask().WaitAsync(timeout);

        HdlcFrame sabm = new() { Address = 0x33, Kind = HdlcFrameKind.SetAsynchronousBalancedMode, PollFinal = true };
        terminal.Write(sabm.ToArray());
        MicroGateFrame observedSabm = await frames.Next();

        HdlcFrame information = new() { Address = 0x33, Kind = HdlcFrameKind.Information, PollFinal = false, SendSequence = 2, ReceiveSequence = 4, Payload = new byte[] { 1, 2, 3 } };
        terminal.Write(information.ToArray());
        MicroGateFrame observedInformation = await frames.Next();

        HdlcFrame disconnect = new() { Address = 0x33, Kind = HdlcFrameKind.Disconnect, PollFinal = true };
        terminal.Write(disconnect.ToArray());
        MicroGateFrame observedDisconnect = await frames.Next();

        byte[] neverWritten = new byte[16];
        int nothingArrivedAtMaster = terminal.Read(neverWritten, 200);

        Assert.Equal(MicroGateFrameKind.SetAsynchronousBalancedMode, observedSabm.Kind);
        Assert.Equal(0x33, observedSabm.Address);
        Assert.Null(observedSabm.ErrorMessage);
        Assert.Equal(MicroGateFrameKind.Information, observedInformation.Kind);
        Assert.Equal(new byte[] { 1, 2, 3 }, observedInformation.Payload.ToArray());
        Assert.Equal(2, observedInformation.SendSequence);
        Assert.Equal(4, observedInformation.ReceiveSequence);
        Assert.Equal(MicroGateFrameKind.Disconnect, observedDisconnect.Kind);
        Assert.Equal(-2, nothingArrivedAtMaster);
    }
}
