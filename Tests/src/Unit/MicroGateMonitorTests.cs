namespace BlueHeighliner.MicroGate;

public sealed class MicroGateMonitorTests : IDisposable
{
    private readonly MonitorDeviceHarness harness = new();
    private readonly TimeSpan timeout = TimeSpan.FromSeconds(5);

    public void Dispose() => harness.Dispose();

    [Fact]
    public void Create_DoesNotOpenDeviceUntilStarted()
    {
        using MicroGateMonitor monitor = harness.CreateMonitor();

        Assert.Equal(MicroGateMonitorState.Idle, monitor.State);
        harness.Opener.Verify(x => x.Open(It.IsAny<string>(), It.IsAny<MicroGateMonitorOptions>()), Times.Never);
    }

    [Fact]
    public async Task Start_OpensDeviceAndReportsStatesToEarlyObserver()
    {
        MicroGateMonitor monitor = harness.CreateMonitor();
        TestObserver<MicroGateMonitorState> states = new();
        monitor.StateChanged.Subscribe(states);

        await monitor.Start("port", harness.Options);

        Assert.Equal([MicroGateMonitorState.Monitoring], states.Seen);
        Assert.Equal(MicroGateMonitorState.Monitoring, monitor.State);
        harness.Opener.Verify(x => x.Open("port", harness.Options), Times.Once);
        await monitor.DisposeAsync();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Start_WithNonPositiveClockSpeed_ThrowsAndLeavesMonitorIdle(int clockSpeed)
    {
        await using MicroGateMonitor monitor = harness.CreateMonitor();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await monitor.Start("port", new MicroGateMonitorOptions { ClockSpeed = clockSpeed }));

        Assert.Equal(MicroGateMonitorState.Idle, monitor.State);
        harness.Opener.Verify(x => x.Open(It.IsAny<string>(), It.IsAny<MicroGateMonitorOptions>()), Times.Never);
    }

    [Fact]
    public async Task Start_WithoutOptions_UsesDefaults()
    {
        MicroGateMonitorOptions? captured = null;
        harness.Opener.Setup(x => x.Open("port", It.IsAny<MicroGateMonitorOptions>())).Callback<string, MicroGateMonitorOptions>((_, passed) => captured = passed).Returns(harness.Device.Object);
        await using MicroGateMonitor monitor = harness.CreateMonitor();

        await monitor.Start("port");

        Assert.Equal(new MicroGateMonitorOptions(), captured);
        Assert.Equal(MicroGateEncoding.Nrz, captured!.Encoding);
        Assert.Equal(MicroGateCrc.Crc32Ccitt, captured.Crc);
        Assert.Null(captured.HardwareAddressFilter);
    }

    [Fact]
    public async Task Start_WhenAlreadyStartedOrDisposed_Throws()
    {
        await using MicroGateMonitor started = harness.CreateMonitor();
        await started.Start("port", harness.Options);

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await started.Start("port", harness.Options));

        await using MicroGateMonitor disposed = harness.CreateMonitor();
        await disposed.DisposeAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await disposed.Start("port", harness.Options));
    }

    [Fact]
    public async Task Start_WhenOpenFails_ThrowsAndStops()
    {
        harness.Opener.Setup(x => x.Open(It.IsAny<string>(), It.IsAny<MicroGateMonitorOptions>())).Throws<IOException>();
        await using MicroGateMonitor monitor = harness.CreateMonitor();

        await Assert.ThrowsAsync<IOException>(async () => await monitor.Start("port", harness.Options));

        Assert.Equal(MicroGateMonitorState.Stopped, monitor.State);
    }

    [Fact]
    public async Task Start_WhenCanceledAfterDeviceOpens_ThrowsAndDisposesMonitor()
    {
        await using MicroGateMonitor monitor = harness.CreateMonitor();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await monitor.Start("port", harness.Options, new CancellationToken(true)));

        Assert.Equal(MicroGateMonitorState.Stopped, monitor.State);
        harness.Device.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    public async Task Start_WhenDisposedWhileDeviceIsOpening_ThrowsObjectDisposedExceptionAndClosesDevice()
    {
        ManualResetEventSlim opening = new();
        ManualResetEventSlim release = new();
        harness.Opener.Setup(x => x.Open(It.IsAny<string>(), It.IsAny<MicroGateMonitorOptions>())).Returns(() =>
        {
            opening.Set();
            release.Wait(timeout);
            return harness.Device.Object;
        });
        MicroGateMonitor monitor = harness.CreateMonitor();
        Task starting = monitor.Start("port", harness.Options).AsTask();
        await Task.Run(() => opening.Wait(timeout));

        await monitor.DisposeAsync();
        release.Set();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => starting);
        harness.Device.Verify(x => x.Dispose(), Times.Once);
        Assert.Equal(MicroGateMonitorState.Stopped, monitor.State);
    }

    [Fact]
    public async Task Start_OnLinux_UsesLinuxOpenerOnly()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        Mock<IMicroGateMonitorDeviceOpener> windowsOpener = new();
        await using MicroGateMonitor monitor = new(harness.Opener.Object, windowsOpener.Object);

        await monitor.Start("ttySLG0", harness.Options);

        harness.Opener.Verify(x => x.Open("ttySLG0", harness.Options), Times.Once);
        windowsOpener.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Start_OnWindows_UsesWindowsOpenerOnly()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        Mock<IMicroGateMonitorDeviceOpener> linuxOpener = new();
        await using MicroGateMonitor monitor = new(linuxOpener.Object, harness.Opener.Object);

        await monitor.Start("COM3", harness.Options);

        harness.Opener.Verify(x => x.Open("COM3", harness.Options), Times.Once);
        linuxOpener.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Start_OnUnsupportedPlatform_ThrowsAndStaysIdle()
    {
        if (OperatingSystem.IsLinux() || OperatingSystem.IsWindows())
        {
            return;
        }

        await using MicroGateMonitor monitor = harness.CreateMonitor();

        await Assert.ThrowsAsync<PlatformNotSupportedException>(async () => await monitor.Start("port"));

        Assert.Equal(MicroGateMonitorState.Idle, monitor.State);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public async Task Received_ReportsEverySystemAndInformationFrameKind(int kindIndex)
    {
        (HdlcFrameKind Hdlc, MicroGateFrameKind MicroGate)[] kinds =
        [
            (HdlcFrameKind.Information, MicroGateFrameKind.Information),
            (HdlcFrameKind.ReceiveReady, MicroGateFrameKind.ReceiveReady),
            (HdlcFrameKind.ReceiveNotReady, MicroGateFrameKind.ReceiveNotReady),
            (HdlcFrameKind.Reject, MicroGateFrameKind.Reject),
            (HdlcFrameKind.SetAsynchronousBalancedMode, MicroGateFrameKind.SetAsynchronousBalancedMode),
            (HdlcFrameKind.Disconnect, MicroGateFrameKind.Disconnect),
            (HdlcFrameKind.UnnumberedAcknowledge, MicroGateFrameKind.UnnumberedAcknowledge),
            (HdlcFrameKind.DisconnectedMode, MicroGateFrameKind.DisconnectedMode),
            (HdlcFrameKind.FrameReject, MicroGateFrameKind.FrameReject),
        ];
        (HdlcFrameKind hdlcKind, MicroGateFrameKind expectedKind) = kinds[kindIndex];
        await using MicroGateMonitor monitor = await harness.Started();
        TestObserver<MicroGateFrame> frames = new();
        monitor.Received.Subscribe(frames);
        ReadOnlyMemory<byte> payload = hdlcKind == HdlcFrameKind.Information ? new byte[] { 9, 8 } : default;

        harness.Receive(harness.Frame(hdlcKind, address: 0x11, payload: payload));

        MicroGateFrame frame = await frames.Next();
        Assert.Equal(expectedKind, frame.Kind);
        Assert.Equal(0x11, frame.Address);
        Assert.Equal(payload.ToArray(), frame.Payload.ToArray());
        Assert.Null(frame.ErrorMessage);
        harness.Device.Verify(x => x.Write(It.IsAny<ReadOnlyMemory<byte>>()), Times.Never);
    }

    [Fact]
    public async Task Received_InformationFrame_ReportsSendAndReceiveSequence()
    {
        await using MicroGateMonitor monitor = await harness.Started();
        TestObserver<MicroGateFrame> frames = new();
        monitor.Received.Subscribe(frames);

        harness.Receive(harness.Frame(HdlcFrameKind.Information, sendSequence: 3, receiveSequence: 5, payload: new byte[] { 1 }));

        MicroGateFrame frame = await frames.Next();
        Assert.Equal(3, frame.SendSequence);
        Assert.Equal(5, frame.ReceiveSequence);
    }

    [Fact]
    public async Task Received_UnnumberedFrame_HasNoSequenceNumbers()
    {
        await using MicroGateMonitor monitor = await harness.Started();
        TestObserver<MicroGateFrame> frames = new();
        monitor.Received.Subscribe(frames);

        harness.Receive(harness.Frame(HdlcFrameKind.SetAsynchronousBalancedMode));

        MicroGateFrame frame = await frames.Next();
        Assert.Null(frame.SendSequence);
        Assert.Null(frame.ReceiveSequence);
    }

    [Fact]
    public async Task Received_SupervisoryFrame_ReportsReceiveSequenceButNoSendSequence()
    {
        await using MicroGateMonitor monitor = await harness.Started();
        TestObserver<MicroGateFrame> frames = new();
        monitor.Received.Subscribe(frames);

        harness.Receive(harness.Frame(HdlcFrameKind.ReceiveReady, receiveSequence: 4));

        MicroGateFrame frame = await frames.Next();
        Assert.Null(frame.SendSequence);
        Assert.Equal(4, frame.ReceiveSequence);
    }

    [Fact]
    public async Task Received_TooShortFrame_IsReportedAsMalformedWithRawBytes()
    {
        await using MicroGateMonitor monitor = await harness.Started();
        TestObserver<MicroGateFrame> frames = new();
        monitor.Received.Subscribe(frames);

        harness.Receive([0xAA]);

        MicroGateFrame frame = await frames.Next();
        Assert.Equal(MicroGateFrameKind.Malformed, frame.Kind);
        Assert.Equal(new byte[] { 0xAA }, frame.Raw.ToArray());
        Assert.NotNull(frame.ErrorMessage);
        Assert.Empty(frame.Payload.ToArray());
    }

    [Fact]
    public async Task Received_UnrecognizedControlByte_IsReportedAsMalformed()
    {
        await using MicroGateMonitor monitor = await harness.Started();
        TestObserver<MicroGateFrame> frames = new();
        monitor.Received.Subscribe(frames);

        harness.Receive([0xFF, 0xEF]);

        MicroGateFrame frame = await frames.Next();
        Assert.Equal(MicroGateFrameKind.Malformed, frame.Kind);
        Assert.NotNull(frame.ErrorMessage);
    }

    [Fact]
    public async Task Received_MalformedFrame_DoesNotStopTheMonitorFromReportingLaterFrames()
    {
        await using MicroGateMonitor monitor = await harness.Started();
        TestObserver<MicroGateFrame> frames = new();
        monitor.Received.Subscribe(frames);

        harness.Receive([0xFF]);
        harness.Receive(harness.Frame(HdlcFrameKind.SetAsynchronousBalancedMode));

        List<MicroGateFrame> seen = await frames.Next(2);
        Assert.Equal(MicroGateFrameKind.Malformed, seen[0].Kind);
        Assert.Equal(MicroGateFrameKind.SetAsynchronousBalancedMode, seen[1].Kind);
    }

    [Fact]
    public async Task Received_WithNoObserver_DoesNotBreakLaterSubscribers()
    {
        await using MicroGateMonitor monitor = await harness.Started();
        harness.Receive(harness.Frame(HdlcFrameKind.SetAsynchronousBalancedMode));
        await Task.Delay(100);
        TestObserver<MicroGateFrame> late = new();
        monitor.Received.Subscribe(late);

        harness.Receive(harness.Frame(HdlcFrameKind.Disconnect));

        MicroGateFrame frame = await late.Next();
        Assert.Equal(MicroGateFrameKind.Disconnect, frame.Kind);
    }

    [Fact]
    public async Task Received_WhenObserverThrows_IsUnsubscribedAndOthersKeepReceiving()
    {
        await using MicroGateMonitor monitor = await harness.Started();
        int calls = 0;
        monitor.Received.Subscribe(new CallbackObserver<MicroGateFrame>(_ =>
        {
            Interlocked.Increment(ref calls);
            throw new InvalidOperationException();
        }));
        TestObserver<MicroGateFrame> healthy = new();
        monitor.Received.Subscribe(healthy);

        harness.Receive(harness.Frame(HdlcFrameKind.SetAsynchronousBalancedMode));
        harness.Receive(harness.Frame(HdlcFrameKind.Disconnect));

        List<MicroGateFrame> seen = await healthy.Next(2);
        Assert.Equal(2, seen.Count);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task EndOfInput_TransitionsToStoppedAndCompletesBothStreams()
    {
        await using MicroGateMonitor monitor = await harness.Started();
        TestObserver<MicroGateMonitorState> states = new();
        TestObserver<MicroGateFrame> frames = new();
        monitor.StateChanged.Subscribe(states);
        monitor.Received.Subscribe(frames);

        harness.EndOfInput();

        await states.Completed.WaitAsync(timeout);
        await frames.Completed.WaitAsync(timeout);
        Assert.Equal([MicroGateMonitorState.Stopped], states.Seen);
        Assert.Equal(MicroGateMonitorState.Stopped, monitor.State);
    }

    [Fact]
    public async Task DisposeAsync_DisablesReceiverAndTransmitterAndClosesDevice()
    {
        MicroGateMonitor monitor = await harness.Started();

        await monitor.DisposeAsync();
        await monitor.DisposeAsync();

        harness.Device.Verify(x => x.DisableReceiver(), Times.Once);
        harness.Device.Verify(x => x.DisableTransmitter(), Times.Once);
        harness.Device.Verify(x => x.Dispose(), Times.Once);
        harness.Device.Verify(x => x.Write(It.IsAny<ReadOnlyMemory<byte>>()), Times.Never);
        Assert.Equal(MicroGateMonitorState.Stopped, monitor.State);
    }

    [Fact]
    public async Task DisposeAsync_BeforeStart_DisconnectsWithoutOpeningDevice()
    {
        MicroGateMonitor monitor = harness.CreateMonitor();

        await monitor.DisposeAsync();

        Assert.Equal(MicroGateMonitorState.Stopped, monitor.State);
        harness.Opener.Verify(x => x.Open(It.IsAny<string>(), It.IsAny<MicroGateMonitorOptions>()), Times.Never);
    }

    [Fact]
    public async Task Dispose_BlocksUntilDisposed()
    {
        MicroGateMonitor monitor = await harness.Started();

        monitor.Dispose();

        harness.Device.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    public async Task Dispose_FromObserverCallback_DoesNotDeadlockAndClosesDevice()
    {
        MicroGateMonitor monitor = await harness.Started();
        TaskCompletionSource disposedInCallback = new();
        monitor.StateChanged.Subscribe(new CallbackObserver<MicroGateMonitorState>(state =>
        {
            if (state == MicroGateMonitorState.Stopped)
            {
                monitor.Dispose();
                disposedInCallback.TrySetResult();
            }
        }));

        harness.EndOfInput();

        await disposedInCallback.Task.WaitAsync(timeout);
        await Eventually(() => harness.Device.Invocations.Count(invocation => invocation.Method.Name == nameof(IMicroGateDevice.Dispose)) == 1);
    }

    [Fact]
    public async Task DisposeAsync_WhenTheReceiverDoesNotWakeTheRead_ClosesTheDeviceAfterTheTimeout()
    {
        ManualResetEventSlim stuck = new();
        harness.Device.Setup(x => x.Read(It.IsAny<byte[]>())).Returns((byte[] buffer) =>
        {
            stuck.Wait(timeout);
            return 0;
        });
        harness.Device.Setup(x => x.DisableReceiver());
        MicroGateMonitor monitor = harness.CreateMonitor(TimeSpan.FromMilliseconds(200));
        await monitor.Start("port", harness.Options);

        await monitor.DisposeAsync().AsTask().WaitAsync(timeout);
        stuck.Set();

        harness.Device.Verify(x => x.Dispose(), Times.Once);
    }

    private async Task Eventually(Func<bool> condition)
    {
        using CancellationTokenSource cancellation = new(timeout);
        while (!condition())
        {
            await Task.Delay(10, cancellation.Token);
        }
    }
}
