namespace BlueHeighliner.MicroGate;

public sealed class MicroGatePeerTests : IDisposable
{
    private readonly DeviceHarness harness = new(new MicroGatePeerOptions { Address = 0x21, RetryInterval = TimeSpan.FromMinutes(1), RetransmitInterval = null });
    private readonly TimeSpan timeout = TimeSpan.FromSeconds(5);

    public void Dispose() => harness.Dispose();

    [Fact]
    public async Task Create_DoesNotOpenDeviceUntilStarted()
    {
        await using MicroGatePeer peer = harness.CreatePeer();

        Assert.Equal(MicroGatePeerState.Idle, peer.State);
        Assert.False(peer.IsConnected);
        harness.Opener.Verify(x => x.Open(It.IsAny<string>(), It.IsAny<MicroGatePeerOptions>()), Times.Never);
    }

    [Fact]
    public async Task Start_OpensDeviceSendsSabmAndReportsStatesToEarlyObserver()
    {
        MicroGatePeer peer = harness.CreatePeer();
        TestObserver<MicroGatePeerState> states = new();
        peer.StateChanged.Subscribe(states);

        Task connecting = peer.Start("port", harness.Options).AsTask();
        HdlcFrame sabm = await harness.NextWritten(0);
        harness.Receive(harness.Peer(HdlcFrameKind.UnnumberedAcknowledge));
        await connecting.WaitAsync(timeout);

        Assert.Equal(HdlcFrameKind.SetAsynchronousBalancedMode, sabm.Kind);
        Assert.Equal([MicroGatePeerState.Connecting, MicroGatePeerState.Connected], states.Seen);
        Assert.True(peer.IsConnected);
        harness.Opener.Verify(x => x.Open("port", harness.Options), Times.Once);
        await peer.DisposeAsync();
    }

    [Fact]
    public async Task Start_WithoutRetry_SendsNothingAndAcceptsRemoteRequest()
    {
        MicroGatePeer peer = harness.CreatePeer();
        MicroGatePeerOptions options = harness.Options with { RetryInterval = null };
        TestObserver<MicroGatePeerState> states = new();
        peer.StateChanged.Subscribe(states);

        Task starting = peer.Start("port", options).AsTask();
        await states.Next();
        await Task.Delay(100);
        Assert.Empty(harness.Written);
        harness.Receive(harness.Peer(HdlcFrameKind.SetAsynchronousBalancedMode));
        await starting.WaitAsync(timeout);

        Assert.Equal(HdlcFrameKind.UnnumberedAcknowledge, (await harness.NextWritten(0)).Kind);
        Assert.Equal([MicroGatePeerState.Connecting, MicroGatePeerState.Connected], states.Seen);
        await peer.DisposeAsync();
    }

    [Fact]
    public async Task Start_ResendsRequestAtRetryIntervalUntilAnswered()
    {
        MicroGatePeer peer = harness.CreatePeer();
        MicroGatePeerOptions options = harness.Options with { RetryInterval = TimeSpan.FromMilliseconds(30) };

        Task starting = peer.Start("port", options).AsTask();
        HdlcFrame third = await harness.NextWritten(2);
        int beforeAnswer = harness.Written.Count;
        harness.Receive(harness.Peer(HdlcFrameKind.UnnumberedAcknowledge));
        await starting.WaitAsync(timeout);
        int atConnected = harness.Written.Count;
        await Task.Delay(150);

        Assert.Equal(HdlcFrameKind.SetAsynchronousBalancedMode, third.Kind);
        Assert.All(harness.Written.Take(beforeAnswer), frame => Assert.Equal(HdlcFrameKind.SetAsynchronousBalancedMode, HdlcFrame.Parse(frame).Kind));
        Assert.True(atConnected >= 3);
        Assert.Equal(atConnected, harness.Written.Count);
        await peer.DisposeAsync();
    }

    [Fact]
    public async Task Start_WhenRemoteRequestArrivesFirst_ConnectsWithoutFurtherRequests()
    {
        MicroGatePeer peer = harness.CreatePeer();
        MicroGatePeerOptions options = harness.Options with { RetryInterval = TimeSpan.FromMilliseconds(40) };

        Task starting = peer.Start("port", options).AsTask();
        await harness.NextWritten(0);
        harness.Receive(harness.Peer(HdlcFrameKind.SetAsynchronousBalancedMode));
        await starting.WaitAsync(timeout);
        int atConnected = harness.Written.Count;
        await Task.Delay(150);

        Assert.True(peer.IsConnected);
        Assert.Equal(HdlcFrameKind.UnnumberedAcknowledge, (await harness.NextWritten(atConnected - 1)).Kind);
        Assert.Equal(atConnected, harness.Written.Count);
        await peer.DisposeAsync();
    }

    [Fact]
    public async Task Start_WhenCanceled_ThrowsAndDisposesPeer()
    {
        using CancellationTokenSource cancellation = new();
        MicroGatePeer peer = harness.CreatePeer();
        TestObserver<MicroGatePeerState> states = new();
        peer.StateChanged.Subscribe(states);

        Task connecting = peer.Start("port", harness.Options, cancellation.Token).AsTask();
        await harness.NextWritten(0);
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => connecting);
        Assert.Equal(MicroGatePeerState.Disconnected, peer.State);
        await states.Completed.WaitAsync(timeout);
        harness.Device.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    public async Task Start_WhenCanceledBeforeDeviceOpens_Throws()
    {
        await using MicroGatePeer peer = harness.CreatePeer();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await peer.Start("port", harness.Options, new CancellationToken(true)));

        Assert.Equal(MicroGatePeerState.Disconnected, peer.State);
    }

    [Fact]
    public async Task Start_WhenOpenFails_ThrowsAndDisconnects()
    {
        harness.Opener.Setup(x => x.Open(It.IsAny<string>(), It.IsAny<MicroGatePeerOptions>())).Throws<IOException>();
        await using MicroGatePeer peer = harness.CreatePeer();

        await Assert.ThrowsAsync<IOException>(async () => await peer.Start("port", harness.Options));

        Assert.Equal(MicroGatePeerState.Disconnected, peer.State);
    }

    [Fact]
    public async Task Start_WhenWriteFails_Throws()
    {
        harness.Device.Setup(x => x.Write(It.IsAny<ReadOnlyMemory<byte>>())).Throws<IOException>();
        await using MicroGatePeer peer = harness.CreatePeer();

        await Assert.ThrowsAsync<IOException>(async () => await peer.Start("port", harness.Options));

        Assert.Equal(MicroGatePeerState.Disconnected, peer.State);
    }

    [Fact]
    public async Task Start_WhenDeviceClosesBeforeEstablished_ThrowsIoException()
    {
        await using MicroGatePeer peer = harness.CreatePeer();
        Task connecting = peer.Start("port", harness.Options).AsTask();
        await harness.NextWritten(0);

        harness.EndOfInput();

        await Assert.ThrowsAsync<IOException>(() => connecting);
        Assert.Equal(MicroGatePeerState.Disconnected, peer.State);
    }

    [Fact]
    public async Task Start_WithoutRetry_WhenCanceled_Throws()
    {
        using CancellationTokenSource cancellation = new(TimeSpan.FromMilliseconds(100));
        await using MicroGatePeer peer = harness.CreatePeer();
        MicroGatePeerOptions options = harness.Options with { RetryInterval = null };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await peer.Start("port", options, cancellation.Token));

        Assert.Equal(MicroGatePeerState.Disconnected, peer.State);
    }

    [Fact]
    public async Task Start_WhenAlreadyStartedOrDisposed_Throws()
    {
        await using MicroGatePeer connected = await harness.Connect();

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await connected.Start("port", harness.Options));
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await connected.Start("port", harness.Options));

        await using MicroGatePeer disposed = harness.CreatePeer();
        await disposed.DisposeAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await disposed.Start("port", harness.Options));
    }

    [Fact]
    public async Task Received_EveryObserverGetsTheFrameData()
    {
        await using MicroGatePeer peer = await harness.Connect();
        PayloadObserver first = new();
        PayloadObserver second = new();
        peer.Received.Subscribe(first);
        peer.Received.Subscribe(second);

        harness.Receive(harness.Peer(HdlcFrameKind.Information, false, 0, new byte[] { 1, 2, 3 }));

        Assert.Equal(new byte[] { 1, 2, 3 }, await first.Next());
        Assert.Equal(new byte[] { 1, 2, 3 }, await second.Next());
        HdlcFrame acknowledgement = await harness.NextWritten(1);
        Assert.Equal(HdlcFrameKind.ReceiveReady, acknowledgement.Kind);
        Assert.Equal(1, acknowledgement.ReceiveSequence);
    }

    [Fact]
    public async Task Received_DataStaysValidAfterLaterFramesArrive()
    {
        await using MicroGatePeer peer = await harness.Connect();
        List<ReadOnlyMemory<byte>> kept = [];
        TaskCompletionSource both = new();
        peer.Received.Subscribe(new CallbackObserver<ReadOnlyMemory<byte>>(data =>
        {
            kept.Add(data);
            if (kept.Count == 2)
            {
                both.SetResult();
            }
        }));

        harness.Receive(harness.Peer(HdlcFrameKind.Information, false, 0, new byte[] { 1, 1 }));
        harness.Receive(harness.Peer(HdlcFrameKind.Information, false, 1, new byte[] { 2, 2 }));
        await both.Task.WaitAsync(timeout);

        Assert.Equal(new byte[] { 1, 1 }, kept[0].ToArray());
        Assert.Equal(new byte[] { 2, 2 }, kept[1].ToArray());
    }

    [Fact]
    public async Task Received_WhenObserverThrows_IsUnsubscribedAndOthersKeepReceiving()
    {
        await using MicroGatePeer peer = await harness.Connect();
        int calls = 0;
        peer.Received.Subscribe(new CallbackObserver<ReadOnlyMemory<byte>>(_ =>
        {
            Interlocked.Increment(ref calls);
            throw new InvalidOperationException();
        }));
        PayloadObserver healthy = new();
        peer.Received.Subscribe(healthy);

        harness.Receive(harness.Peer(HdlcFrameKind.Information, false, 0, new byte[] { 4 }));
        harness.Receive(harness.Peer(HdlcFrameKind.Information, false, 1, new byte[] { 5 }));

        Assert.Equal([new byte[] { 4 }, new byte[] { 5 }], await healthy.Next(2));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Received_MalformedFrame_IsDroppedAndLoopContinues()
    {
        await using MicroGatePeer peer = await harness.Connect();
        PayloadObserver observer = new();
        peer.Received.Subscribe(observer);

        harness.Receive([0xFF]);
        harness.Receive(harness.Peer(HdlcFrameKind.Information, false, 0, new byte[] { 9 }));

        Assert.Equal(new byte[] { 9 }, await observer.Next());
    }

    [Fact]
    public async Task Received_WithNoObserver_StillAcknowledges()
    {
        await using MicroGatePeer peer = await harness.Connect();

        harness.Receive(harness.Peer(HdlcFrameKind.Information, false, 0, new byte[] { 1 }));

        Assert.Equal(HdlcFrameKind.ReceiveReady, (await harness.NextWritten(1)).Kind);
    }

    [Fact]
    public async Task Send_Memory_WritesInformationFrameWithIncreasingSequence()
    {
        await using MicroGatePeer peer = await harness.Connect();

        await peer.Send(new byte[] { 5, 6 });
        await peer.Send(new byte[] { 7 });

        HdlcFrame first = await harness.NextWritten(1);
        HdlcFrame second = await harness.NextWritten(2);
        Assert.Equal(HdlcFrameKind.Information, first.Kind);
        Assert.Equal(0, first.SendSequence);
        Assert.Equal(new byte[] { 5, 6 }, first.Payload.ToArray());
        Assert.Equal(1, second.SendSequence);
    }

    [Fact]
    public async Task Send_Owner_WritesFrameAndDisposesOwner()
    {
        await using MicroGatePeer peer = await harness.Connect();
        Mock<IMemoryOwner<byte>> owner = new();
        owner.SetupGet(x => x.Memory).Returns(new byte[] { 4, 4 });

        await peer.Send(owner.Object);

        Assert.Equal(new byte[] { 4, 4 }, (await harness.NextWritten(1)).Payload.ToArray());
        owner.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    public async Task Send_Owner_WhenNotConnected_DisposesOwnerAndThrows()
    {
        await using MicroGatePeer peer = harness.CreatePeer();
        Mock<IMemoryOwner<byte>> owner = new();
        owner.SetupGet(x => x.Memory).Returns(new byte[] { 1 });

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await peer.Send(owner.Object));

        owner.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    public async Task Send_WhenWriteFails_Throws()
    {
        await using MicroGatePeer peer = await harness.Connect();
        harness.Device.Setup(x => x.Write(It.IsAny<ReadOnlyMemory<byte>>())).Throws<IOException>();

        await Assert.ThrowsAsync<IOException>(async () => await peer.Send(new byte[] { 1 }));
    }

    [Fact]
    public async Task Receive_PeerDisconnect_EmitsDisconnectedOnceCompletesStreamsAndAcknowledges()
    {
        MicroGatePeer peer = await harness.Connect();
        TestObserver<MicroGatePeerState> states = new();
        PayloadObserver payloads = new();
        peer.StateChanged.Subscribe(states);
        peer.Received.Subscribe(payloads);

        harness.Receive(harness.Peer(HdlcFrameKind.Disconnect));

        await states.Completed.WaitAsync(timeout);
        await payloads.Completed.WaitAsync(timeout);
        Assert.Equal([MicroGatePeerState.Disconnected], states.Seen);
        Assert.Equal(HdlcFrameKind.UnnumberedAcknowledge, (await harness.NextWritten(1)).Kind);
        Assert.False(peer.IsConnected);
        await peer.DisposeAsync();
        Assert.Equal([MicroGatePeerState.Disconnected], states.Seen);
    }

    [Fact]
    public async Task Receive_EndOfInputWhileConnected_EmitsDisconnected()
    {
        await using MicroGatePeer peer = await harness.Connect();
        TestObserver<MicroGatePeerState> states = new();
        peer.StateChanged.Subscribe(states);

        harness.EndOfInput();

        await states.Completed.WaitAsync(timeout);
        Assert.Equal([MicroGatePeerState.Disconnected], states.Seen);
    }

    [Fact]
    public async Task Subscribe_AfterDisconnect_CompletesImmediately()
    {
        MicroGatePeer peer = await harness.Connect();
        await peer.DisposeAsync();
        TestObserver<MicroGatePeerState> states = new();
        PayloadObserver payloads = new();

        peer.StateChanged.Subscribe(states);
        peer.Received.Subscribe(payloads);

        await states.Completed.WaitAsync(timeout);
        await payloads.Completed.WaitAsync(timeout);
    }

    [Fact]
    public async Task DisposeAsync_WhenConnected_SendsDisconnectDisablesReceiverAndDisposesDevice()
    {
        MicroGatePeer peer = await harness.Connect();
        TestObserver<MicroGatePeerState> states = new();
        peer.StateChanged.Subscribe(states);

        await peer.DisposeAsync();
        await peer.DisposeAsync();

        Assert.Equal(HdlcFrameKind.Disconnect, (await harness.NextWritten(1)).Kind);
        harness.Device.Verify(x => x.DisableReceiver(), Times.Once);
        harness.Device.Verify(x => x.Dispose(), Times.Once);
        Assert.Equal([MicroGatePeerState.Disconnected], states.Seen);
    }

    [Fact]
    public async Task DisposeAsync_WhenDisconnectWriteFails_StillDisposesDevice()
    {
        MicroGatePeer peer = await harness.Connect();
        harness.Device.Setup(x => x.Write(It.IsAny<ReadOnlyMemory<byte>>())).Throws<IOException>();

        await peer.DisposeAsync();

        harness.Device.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    public async Task DisposeAsync_WhenReceiveLoopFaulted_DoesNotThrow()
    {
        MicroGatePeer peer = await harness.Connect();
        harness.Device.Setup(x => x.Write(It.IsAny<ReadOnlyMemory<byte>>())).Throws<IOException>();
        harness.Receive(harness.Peer(HdlcFrameKind.Information, false, 0, new byte[] { 1 }));
        await Task.Delay(200);

        await peer.DisposeAsync();

        harness.Device.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    public async Task DisposeAsync_BeforeStart_DisconnectsWithoutOpeningDevice()
    {
        MicroGatePeer peer = harness.CreatePeer();
        TestObserver<MicroGatePeerState> states = new();
        peer.StateChanged.Subscribe(states);

        await peer.DisposeAsync();

        Assert.Equal(MicroGatePeerState.Disconnected, peer.State);
        await states.Completed.WaitAsync(timeout);
        harness.Opener.Verify(x => x.Open(It.IsAny<string>(), It.IsAny<MicroGatePeerOptions>()), Times.Never);
        Assert.Empty(harness.Written);
    }

    [Fact]
    public async Task Dispose_BlocksUntilDisposed()
    {
        MicroGatePeer peer = await harness.Connect();

        peer.Dispose();

        harness.Device.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    public async Task Start_WithoutOptions_UsesDefaults()
    {
        MicroGatePeerOptions? captured = null;
        harness.Opener.Setup(x => x.Open("port", It.IsAny<MicroGatePeerOptions>())).Callback<string, MicroGatePeerOptions>((_, passed) => captured = passed).Returns(harness.Device.Object);
        await using MicroGatePeer peer = harness.CreatePeer();
        Task starting = peer.Start("port").AsTask();
        await harness.NextWritten(0);
        harness.EndOfInput();

        await Assert.ThrowsAsync<IOException>(() => starting);

        Assert.Equal(new MicroGatePeerOptions(), captured);
        Assert.Equal(TimeSpan.FromSeconds(1), captured!.RetryInterval);
        Assert.False(captured.DisablePollFinalBit);
        Assert.Equal(MicroGateEncoding.Nrz, captured.Encoding);
        Assert.Equal(MicroGateCrc.Crc16Ccitt, captured.Crc);
        Assert.Equal(MicroGateIdlePattern.Flags, captured.IdlePattern);
        Assert.Null(captured.HardwareAddressFilter);
        Assert.Equal(0xFF, captured.Address);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task Start_WithNonPositiveRetryInterval_ThrowsAndStaysIdle(int milliseconds)
    {
        await using MicroGatePeer peer = harness.CreatePeer();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await peer.Start("port", new MicroGatePeerOptions { RetryInterval = TimeSpan.FromMilliseconds(milliseconds) }));

        Assert.Equal(MicroGatePeerState.Idle, peer.State);
        harness.Opener.Verify(x => x.Open(It.IsAny<string>(), It.IsAny<MicroGatePeerOptions>()), Times.Never);
    }

    [Fact]
    public async Task Start_OnLinux_UsesLinuxOpenerOnly()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        Mock<IMicroGateDeviceOpener> windowsOpener = new();
        await using MicroGatePeer peer = new(harness.Opener.Object, windowsOpener.Object);
        Task starting = peer.Start("ttySLG0", harness.Options).AsTask();
        await harness.NextWritten(0);
        harness.EndOfInput();

        await Assert.ThrowsAsync<IOException>(() => starting);

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

        Mock<IMicroGateDeviceOpener> linuxOpener = new();
        await using MicroGatePeer peer = new(linuxOpener.Object, harness.Opener.Object);
        Task starting = peer.Start("COM3", harness.Options).AsTask();
        await harness.NextWritten(0);
        harness.EndOfInput();

        await Assert.ThrowsAsync<IOException>(() => starting);

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

        await using MicroGatePeer peer = harness.CreatePeer();

        await Assert.ThrowsAsync<PlatformNotSupportedException>(async () => await peer.Start("port"));

        Assert.Equal(MicroGatePeerState.Idle, peer.State);
    }

    [Fact]
    public async Task Send_BeforeStart_Throws()
    {
        await using MicroGatePeer peer = harness.CreatePeer();

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await peer.Send(new byte[] { 1 }));
    }

    [Fact]
    public async Task Start_WithNonPositiveRetransmitInterval_ThrowsAndStaysIdle()
    {
        await using MicroGatePeer peer = harness.CreatePeer();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await peer.Start("port", new MicroGatePeerOptions { RetransmitInterval = TimeSpan.Zero }));

        Assert.Equal(MicroGatePeerState.Idle, peer.State);
    }

    [Fact]
    public async Task ReceiveLoop_WhenResponseWriteFails_DisconnectsAndCompletesStreams()
    {
        MicroGatePeer peer = await harness.Connect();
        TestObserver<MicroGatePeerState> states = new();
        PayloadObserver payloads = new();
        peer.StateChanged.Subscribe(states);
        peer.Received.Subscribe(payloads);
        harness.Device.Setup(x => x.Write(It.IsAny<ReadOnlyMemory<byte>>())).Throws<IOException>();

        harness.Receive(harness.Peer(HdlcFrameKind.Information, false, 0, new byte[] { 1 }));

        await states.Completed.WaitAsync(timeout);
        await payloads.Completed.WaitAsync(timeout);
        Assert.Equal([MicroGatePeerState.Disconnected], states.Seen);
        Assert.Equal(MicroGatePeerState.Disconnected, peer.State);
        await peer.DisposeAsync();
        harness.Device.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    public async Task ReceiveLoop_WhenReadThrowsBeforeConnected_StartFailsWithIoException()
    {
        harness.Device.Setup(x => x.Read(It.IsAny<byte[]>())).Throws<InvalidOperationException>();
        await using MicroGatePeer peer = harness.CreatePeer();

        IOException exception = await Assert.ThrowsAsync<IOException>(async () => await peer.Start("port", harness.Options with { RetryInterval = null }));

        Assert.IsType<InvalidOperationException>(exception.InnerException);
        Assert.Equal(MicroGatePeerState.Disconnected, peer.State);
    }

    [Fact]
    public async Task DisposeAsync_WhileDeviceIsOpening_DisposesTheDeviceAndFailsStart()
    {
        ManualResetEventSlim release = new();
        ManualResetEventSlim opening = new();
        harness.Opener.Setup(x => x.Open(It.IsAny<string>(), It.IsAny<MicroGatePeerOptions>())).Returns(() =>
        {
            opening.Set();
            release.Wait(timeout);
            return harness.Device.Object;
        });
        MicroGatePeer peer = harness.CreatePeer();
        Task starting = peer.Start("port", harness.Options).AsTask();
        await Task.Run(() => opening.Wait(timeout));

        await peer.DisposeAsync();
        release.Set();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => starting);
        harness.Device.Verify(x => x.Dispose(), Times.Once);
        Assert.Equal(MicroGatePeerState.Disconnected, peer.State);
        Assert.Empty(harness.Written);
    }

    [Fact]
    public async Task Dispose_FromDisconnectedObserver_DoesNotDeadlockAndClosesDevice()
    {
        MicroGatePeer peer = await harness.Connect();
        TaskCompletionSource disposedInCallback = new();
        peer.StateChanged.Subscribe(new CallbackObserver<MicroGatePeerState>(state =>
        {
            if (state == MicroGatePeerState.Disconnected)
            {
                peer.Dispose();
                disposedInCallback.TrySetResult();
            }
        }));

        harness.Receive(harness.Peer(HdlcFrameKind.Disconnect));

        await disposedInCallback.Task.WaitAsync(timeout);
        await Eventually(() => harness.Device.Invocations.Count(invocation => invocation.Method.Name == nameof(IMicroGateDevice.Dispose)) == 1);
    }

    [Fact]
    public async Task Dispose_FromReceivedObserver_SendsDisconnectAndClosesDeviceAfterLoopEnds()
    {
        MicroGatePeer peer = await harness.Connect();
        TaskCompletionSource disposedInCallback = new();
        peer.Received.Subscribe(new CallbackObserver<ReadOnlyMemory<byte>>(_ =>
        {
            peer.Dispose();
            disposedInCallback.TrySetResult();
        }));

        harness.Receive(harness.Peer(HdlcFrameKind.Information, false, 0, new byte[] { 1 }));

        await disposedInCallback.Task.WaitAsync(timeout);
        await Eventually(() => harness.Device.Invocations.Count(invocation => invocation.Method.Name == nameof(IMicroGateDevice.Dispose)) == 1);
        Assert.Equal(MicroGatePeerState.Disconnected, peer.State);
        Assert.Contains(harness.Written, frame => HdlcFrame.Parse(frame).Kind == HdlcFrameKind.Disconnect);
    }

    [Fact]
    public async Task StateChanged_ObserversRunOutsideTheStateLock()
    {
        MicroGatePeer peer = harness.CreatePeer();
        TaskCompletionSource<MicroGatePeerState> otherThreadRead = new();
        peer.StateChanged.Subscribe(new CallbackObserver<MicroGatePeerState>(_ =>
        {
            Task<MicroGatePeerState> read = Task.Run(() => peer.State);
            if (read.Wait(timeout))
            {
                otherThreadRead.TrySetResult(read.Result);
            }
        }));
        Task starting = peer.Start("port", harness.Options).AsTask();

        Assert.Equal(MicroGatePeerState.Connecting, await otherThreadRead.Task.WaitAsync(timeout));

        await harness.NextWritten(0);
        harness.Receive(harness.Peer(HdlcFrameKind.UnnumberedAcknowledge));
        await starting.WaitAsync(timeout);
        await peer.DisposeAsync();
    }

    [Fact]
    public async Task Send_MoreThanTheWindow_WaitsForAcknowledgement()
    {
        await using MicroGatePeer peer = await harness.Connect();
        for (int i = 0; i < 7; i++)
        {
            await peer.Send(new byte[] { (byte)i });
        }

        Task eighth = peer.Send(new byte[] { 7 }).AsTask();
        await Task.Delay(100);
        Assert.False(eighth.IsCompleted);

        harness.Receive(harness.Peer(HdlcFrameKind.ReceiveReady, false, receiveSequence: 3));
        await eighth.WaitAsync(timeout);

        HdlcFrame last = await harness.NextWritten(8);
        Assert.Equal(7, last.SendSequence);
        Assert.Equal(new byte[] { 7 }, last.Payload.ToArray());
    }

    [Fact]
    public async Task Send_WaitingForWindow_CanBeCanceled()
    {
        await using MicroGatePeer peer = await harness.Connect();
        for (int i = 0; i < 7; i++)
        {
            await peer.Send(new byte[] { (byte)i });
        }

        using CancellationTokenSource cancellation = new(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await peer.Send(new byte[] { 7 }, cancellation.Token));
    }

    [Fact]
    public async Task Send_WaitingForWindow_FailsWhenPeerDisconnects()
    {
        MicroGatePeer peer = await harness.Connect();
        for (int i = 0; i < 7; i++)
        {
            await peer.Send(new byte[] { (byte)i });
        }

        Task waiting = peer.Send(new byte[] { 7 }).AsTask();
        await Task.Delay(50);
        harness.Receive(harness.Peer(HdlcFrameKind.Disconnect));

        await Assert.ThrowsAsync<IOException>(() => waiting);
        await peer.DisposeAsync();
    }

    [Fact]
    public async Task Send_AfterAcknowledgements_KeepsDeliveringBeyondTheWindow()
    {
        await using MicroGatePeer peer = await harness.Connect();

        for (int i = 0; i < 30; i++)
        {
            await peer.Send(new byte[] { (byte)i });
            harness.Receive(harness.Peer(HdlcFrameKind.ReceiveReady, false, receiveSequence: (i + 1) % 8));
        }

        HdlcFrame last = await harness.NextWritten(30);
        Assert.Equal(29 % 8, last.SendSequence);
        Assert.Equal(new byte[] { 29 }, last.Payload.ToArray());
    }

    [Fact]
    public async Task Receive_Reject_SendsRejectedFramesAgain()
    {
        await using MicroGatePeer peer = await harness.Connect();
        for (int i = 0; i < 3; i++)
        {
            await peer.Send(new byte[] { (byte)(20 + i) });
        }

        harness.Receive(harness.Peer(HdlcFrameKind.Reject, false, receiveSequence: 1));

        HdlcFrame again = await harness.NextWritten(4);
        HdlcFrame andAgain = await harness.NextWritten(5);
        Assert.Equal(1, again.SendSequence);
        Assert.Equal(new byte[] { 21 }, again.Payload.ToArray());
        Assert.Equal(2, andAgain.SendSequence);
        Assert.Equal(new byte[] { 22 }, andAgain.Payload.ToArray());
    }

    [Fact]
    public async Task Retransmit_WhenNothingIsAcknowledged_SendsUnacknowledgedFramesAgainUntilAcknowledged()
    {
        MicroGatePeerOptions timed = harness.Options with { RetransmitInterval = TimeSpan.FromMilliseconds(60) };
        MicroGatePeer peer = harness.CreatePeer();
        Task starting = peer.Start("port", timed).AsTask();
        await harness.NextWritten(0);
        harness.Receive(harness.Peer(HdlcFrameKind.UnnumberedAcknowledge));
        await starting.WaitAsync(timeout);

        await peer.Send(new byte[] { 5 });
        HdlcFrame original = await harness.NextWritten(1);
        HdlcFrame repeat = await harness.NextWritten(2);
        harness.Receive(harness.Peer(HdlcFrameKind.ReceiveReady, false, receiveSequence: 1));
        await Task.Delay(100);
        int afterAck = harness.Written.Count;
        await Task.Delay(250);

        Assert.Equal(0, original.SendSequence);
        Assert.Equal(0, repeat.SendSequence);
        Assert.Equal(new byte[] { 5 }, repeat.Payload.ToArray());
        Assert.Equal(afterAck, harness.Written.Count);
        await peer.DisposeAsync();
    }

    [Fact]
    public async Task Retransmit_WithoutInterval_NeverSendsOnItsOwn()
    {
        await using MicroGatePeer peer = await harness.Connect();

        await peer.Send(new byte[] { 5 });
        await Task.Delay(300);

        Assert.Equal(2, harness.Written.Count);
    }

    private async Task Eventually(Func<bool> condition)
    {
        using CancellationTokenSource cancellation = new(timeout);
        while (!condition())
        {
            await Task.Delay(10, cancellation.Token);
        }
    }

    [Fact]
    public async Task Send_PayloadLargerThanTheMaximum_ThrowsAndDisposesOwner()
    {
        await using MicroGatePeer peer = await harness.Connect();
        Mock<IMemoryOwner<byte>> owner = new();
        owner.SetupGet(x => x.Memory).Returns(new byte[peer.MaxPayloadSize + 1]);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await peer.Send(new byte[peer.MaxPayloadSize + 1]));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await peer.Send(owner.Object));

        owner.Verify(x => x.Dispose(), Times.Once);
        Assert.Equal(4090, peer.MaxPayloadSize);
    }

    [Fact]
    public async Task Send_PayloadOfTheMaximum_IsSent()
    {
        await using MicroGatePeer peer = await harness.Connect();

        await peer.Send(new byte[peer.MaxPayloadSize]);

        Assert.Equal(peer.MaxPayloadSize, (await harness.NextWritten(1)).Payload.Length);
    }

    [Fact]
    public async Task Received_WithNoObserver_DoesNotBreakLaterSubscribers()
    {
        await using MicroGatePeer peer = await harness.Connect();
        harness.Receive(harness.Peer(HdlcFrameKind.Information, false, 0, new byte[] { 1 }));
        await harness.NextWritten(1);
        PayloadObserver late = new();
        peer.Received.Subscribe(late);

        harness.Receive(harness.Peer(HdlcFrameKind.Information, false, 1, new byte[] { 2 }));

        byte[] first = await late.Next();
        byte[] latest = first[0] == 2 ? first : await late.Next();
        Assert.Equal(new byte[] { 2 }, latest);
    }

    [Fact]
    public async Task DisposeAsync_WhenTheDisconnectWriteBlocks_CancelsItByDisablingTheTransmitter()
    {
        MicroGatePeer peer = harness.CreatePeer(TimeSpan.FromMilliseconds(200));
        Task starting = peer.Start("port", harness.Options).AsTask();
        await harness.NextWritten(0);
        harness.Receive(harness.Peer(HdlcFrameKind.UnnumberedAcknowledge));
        await starting.WaitAsync(timeout);
        ManualResetEventSlim release = new();
        harness.Device.Setup(x => x.Write(It.IsAny<ReadOnlyMemory<byte>>())).Callback(() => release.Wait(timeout));
        harness.Device.Setup(x => x.DisableTransmitter()).Callback(release.Set);

        await peer.DisposeAsync().AsTask().WaitAsync(timeout);

        harness.Device.Verify(x => x.DisableTransmitter(), Times.Once);
        harness.Device.Verify(x => x.Dispose(), Times.Once);
        Assert.Equal(MicroGatePeerState.Disconnected, peer.State);
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
        MicroGatePeer peer = harness.CreatePeer(TimeSpan.FromMilliseconds(200));
        Task starting = peer.Start("port", harness.Options with { RetryInterval = null }).AsTask();
        await Task.Delay(100);

        await peer.DisposeAsync().AsTask().WaitAsync(timeout);
        stuck.Set();

        harness.Device.Verify(x => x.Dispose(), Times.Once);
        await Assert.ThrowsAnyAsync<Exception>(() => starting);
    }

    [Fact]
    public async Task Start_WithNonPositiveMaxRetransmissions_ThrowsAndStaysIdle()
    {
        await using MicroGatePeer peer = harness.CreatePeer();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await peer.Start("port", new MicroGatePeerOptions { MaxRetransmissions = 0 }));

        Assert.Equal(MicroGatePeerState.Idle, peer.State);
    }

    [Fact]
    public async Task Retransmit_WhenTheRemotePeerNeverAcknowledges_GivesUpAndDisconnects()
    {
        MicroGatePeerOptions quick = harness.Options with { RetransmitInterval = TimeSpan.FromMilliseconds(40), MaxRetransmissions = 2 };
        MicroGatePeer peer = harness.CreatePeer();
        TestObserver<MicroGatePeerState> states = new();
        PayloadObserver payloads = new();
        peer.StateChanged.Subscribe(states);
        peer.Received.Subscribe(payloads);
        Task starting = peer.Start("port", quick).AsTask();
        await harness.NextWritten(0);
        harness.Receive(harness.Peer(HdlcFrameKind.UnnumberedAcknowledge));
        await starting.WaitAsync(timeout);

        await peer.Send(new byte[] { 1 });
        await states.Completed.WaitAsync(timeout);
        await payloads.Completed.WaitAsync(timeout);

        Assert.Equal(MicroGatePeerState.Disconnected, peer.State);
        Assert.Equal([MicroGatePeerState.Connecting, MicroGatePeerState.Connected, MicroGatePeerState.Disconnected], states.Seen);
        Assert.Equal(1 + 1 + 2, harness.Written.Count);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await peer.Send(new byte[] { 2 }));
        harness.Device.Verify(x => x.DisableReceiver(), Times.AtLeastOnce);
        await peer.DisposeAsync();
    }

    [Fact]
    public async Task Retransmit_WhenAcknowledgementsKeepArriving_NeverGivesUp()
    {
        MicroGatePeerOptions quick = harness.Options with { RetransmitInterval = TimeSpan.FromMilliseconds(60), MaxRetransmissions = 1 };
        MicroGatePeer peer = harness.CreatePeer();
        Task starting = peer.Start("port", quick).AsTask();
        await harness.NextWritten(0);
        harness.Receive(harness.Peer(HdlcFrameKind.UnnumberedAcknowledge));
        await starting.WaitAsync(timeout);

        for (int i = 0; i < 8; i++)
        {
            await peer.Send(new byte[] { (byte)i });
            await Task.Delay(50);
            harness.Receive(harness.Peer(HdlcFrameKind.ReceiveReady, false, receiveSequence: (i + 1) % 8));
        }

        Assert.True(peer.IsConnected);
        await peer.DisposeAsync();
    }

    [Fact]
    public async Task Retransmit_WhenResendingFails_DisconnectsInsteadOfStoppingSilently()
    {
        MicroGatePeerOptions quick = harness.Options with { RetransmitInterval = TimeSpan.FromMilliseconds(40) };
        MicroGatePeer peer = harness.CreatePeer();
        TestObserver<MicroGatePeerState> states = new();
        peer.StateChanged.Subscribe(states);
        Task starting = peer.Start("port", quick).AsTask();
        await harness.NextWritten(0);
        harness.Receive(harness.Peer(HdlcFrameKind.UnnumberedAcknowledge));
        await starting.WaitAsync(timeout);
        await peer.Send(new byte[] { 1 });
        harness.Device.Setup(x => x.Write(It.IsAny<ReadOnlyMemory<byte>>())).Throws<IOException>();

        await states.Completed.WaitAsync(timeout);

        Assert.Equal(MicroGatePeerState.Disconnected, peer.State);
        await peer.DisposeAsync();
    }

    [Fact]
    public async Task Send_WhenTheWriteFails_TakesTheFrameBackSoARetryIsNotDuplicated()
    {
        await using MicroGatePeer peer = await harness.Connect();
        harness.Device.Setup(x => x.Write(It.IsAny<ReadOnlyMemory<byte>>())).Throws<IOException>();

        await Assert.ThrowsAsync<IOException>(async () => await peer.Send(new byte[] { 1 }));
        harness.Device.Setup(x => x.Write(It.IsAny<ReadOnlyMemory<byte>>()));
        for (int i = 0; i < 7; i++)
        {
            await peer.Send(new byte[] { (byte)(10 + i) }).AsTask().WaitAsync(timeout);
        }

        Task eighth = peer.Send(new byte[] { 99 }).AsTask();
        await Task.Delay(100);
        Assert.False(eighth.IsCompleted);
        harness.Receive(harness.Peer(HdlcFrameKind.ReceiveReady, false, receiveSequence: 7));
        await eighth.WaitAsync(timeout);
    }

    [Fact]
    public async Task Receive_RepeatedRequestWhileConnected_ResetsTheLinkAndSendsUnacknowledgedDataAgain()
    {
        await using MicroGatePeer peer = await harness.Connect();
        await peer.Send(new byte[] { 1 });
        await peer.Send(new byte[] { 2 });
        await harness.NextWritten(2);

        harness.Receive(harness.Peer(HdlcFrameKind.SetAsynchronousBalancedMode));

        Assert.Equal(HdlcFrameKind.UnnumberedAcknowledge, (await harness.NextWritten(3)).Kind);
        HdlcFrame first = await harness.NextWritten(4);
        HdlcFrame second = await harness.NextWritten(5);
        Assert.Equal(0, first.SendSequence);
        Assert.Equal(new byte[] { 1 }, first.Payload.ToArray());
        Assert.Equal(1, second.SendSequence);
        Assert.Equal(new byte[] { 2 }, second.Payload.ToArray());
        Assert.True(peer.IsConnected);
    }

    [Fact]
    public async Task Send_AfterTheRemotePeerDisconnected_Throws()
    {
        MicroGatePeer peer = await harness.Connect();
        TestObserver<MicroGatePeerState> states = new();
        peer.StateChanged.Subscribe(states);
        harness.Receive(harness.Peer(HdlcFrameKind.Disconnect));
        await states.Completed.WaitAsync(timeout);

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await peer.Send(new byte[] { 1 }));
        await peer.DisposeAsync();
    }

    [Fact]
    public async Task Dispose_FromObserverWhenRetransmissionGivesUp_ReturnsWithoutWaitingForTheShutdownTimeout()
    {
        MicroGatePeerOptions quick = harness.Options with { RetransmitInterval = TimeSpan.FromMilliseconds(40), MaxRetransmissions = 1 };
        MicroGatePeer peer = harness.CreatePeer(TimeSpan.FromSeconds(30));
        Task starting = peer.Start("port", quick).AsTask();
        await harness.NextWritten(0);
        harness.Receive(harness.Peer(HdlcFrameKind.UnnumberedAcknowledge));
        await starting.WaitAsync(timeout);
        TaskCompletionSource<TimeSpan> disposeTook = new();
        peer.StateChanged.Subscribe(new CallbackObserver<MicroGatePeerState>(state =>
        {
            if (state == MicroGatePeerState.Disconnected)
            {
                Stopwatch watch = Stopwatch.StartNew();
                peer.Dispose();
                disposeTook.TrySetResult(watch.Elapsed);
            }
        }));

        await peer.Send(new byte[] { 1 });

        Assert.True(await disposeTook.Task.WaitAsync(timeout) < TimeSpan.FromSeconds(5));
        await Eventually(() => harness.Device.Invocations.Count(invocation => invocation.Method.Name == nameof(IMicroGateDevice.Dispose)) == 1);
    }

    [Fact]
    public async Task Start_WhenDisposedWhileWaitingForTheRemotePeer_ThrowsObjectDisposedException()
    {
        MicroGatePeer peer = harness.CreatePeer();
        Task starting = peer.Start("port", harness.Options).AsTask();
        await harness.NextWritten(0);

        await peer.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => starting);
        Assert.Equal(MicroGatePeerState.Disconnected, peer.State);
        harness.Device.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    public async Task Start_WithSubMillisecondRetransmitInterval_StaysConnected()
    {
        MicroGatePeer peer = harness.CreatePeer();
        Task starting = peer.Start("port", harness.Options with { RetransmitInterval = TimeSpan.FromTicks(5000) }).AsTask();
        await harness.NextWritten(0);
        harness.Receive(harness.Peer(HdlcFrameKind.UnnumberedAcknowledge));
        await starting.WaitAsync(timeout);

        await Task.Delay(150);

        Assert.True(peer.IsConnected);
        await peer.DisposeAsync();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Start_WithIntervalLongerThanSupported_ThrowsAndStaysIdle(bool retry)
    {
        await using MicroGatePeer peer = harness.CreatePeer();
        TimeSpan tooLong = TimeSpan.FromMilliseconds(int.MaxValue) + TimeSpan.FromMilliseconds(1);
        MicroGatePeerOptions options = retry ? new() { RetryInterval = tooLong } : new() { RetransmitInterval = tooLong };

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await peer.Start("port", options));

        Assert.Equal(MicroGatePeerState.Idle, peer.State);
    }

    [Fact]
    public async Task Received_ItemsAndCompletionAreNeverDeliveredConcurrently()
    {
        MicroGatePeerOptions quick = harness.Options with { RetransmitInterval = TimeSpan.FromMilliseconds(30), MaxRetransmissions = 1 };
        MicroGatePeer peer = harness.CreatePeer();
        Task starting = peer.Start("port", quick).AsTask();
        await harness.NextWritten(0);
        harness.Receive(harness.Peer(HdlcFrameKind.UnnumberedAcknowledge));
        await starting.WaitAsync(timeout);
        int inside = 0;
        bool overlapped = false;
        TaskCompletionSource completed = new();
        peer.Received.Subscribe(
            _ =>
            {
                if (Interlocked.Increment(ref inside) > 1)
                {
                    overlapped = true;
                }

                Thread.Sleep(20);
                Interlocked.Decrement(ref inside);
            },
            () =>
            {
                if (Volatile.Read(ref inside) > 0)
                {
                    overlapped = true;
                }

                completed.TrySetResult();
            });

        await peer.Send(new byte[] { 1 });
        for (int i = 0; i < 10; i++)
        {
            harness.Receive(harness.Peer(HdlcFrameKind.Information, false, i % 8, new byte[] { (byte)i }));
        }

        await completed.Task.WaitAsync(timeout);
        Assert.False(overlapped);
        await peer.DisposeAsync();
    }
}
