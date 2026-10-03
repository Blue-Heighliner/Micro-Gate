namespace BlueHeighliner.MicroGate;

public sealed class HdlcPeerTests : IDisposable
{
    private readonly DeviceHarness harness = new(new HdlcPeerOptions { RetryInterval = TimeSpan.FromMinutes(1), RetransmitInterval = null });
    private readonly TimeSpan timeout = TimeSpan.FromSeconds(5);

    public void Dispose() => harness.Dispose();

    [Fact]
    public async Task Create_DoesNotOpenDeviceUntilStarted()
    {
        await using HdlcPeer peer = harness.CreatePeer();

        Assert.Equal(HdlcPeerState.Idle, peer.State);
        Assert.False(peer.IsConnected);
        harness.Opener.Verify(x => x.Open(It.IsAny<string>(), It.IsAny<HdlcPeerOptions>()), Times.Never);
    }

    [Fact]
    public async Task Start_OpensDeviceSendsSabmAndReportsStatesToEarlyObserver()
    {
        HdlcPeer peer = harness.CreatePeer();
        TestObserver<HdlcPeerState> states = new();
        peer.StateChanged.Subscribe(states);

        Task connecting = peer.StartAndConnect("port", harness.Address, harness.RemoteAddress, harness.Options).AsTask();
        HdlcWireFrame sabm = await harness.NextWritten(0);
        harness.Receive(harness.Peer(HdlcWireFrameKind.UnnumberedAcknowledge));
        await connecting.WaitAsync(timeout);

        Assert.Equal(HdlcWireFrameKind.SetAsynchronousBalancedMode, sabm.Kind);
        Assert.Equal([HdlcPeerState.Ready, HdlcPeerState.Connecting, HdlcPeerState.Connected], states.Seen);
        Assert.True(peer.IsConnected);
        harness.Opener.Verify(x => x.Open("port", harness.Options), Times.Once);
        await peer.DisposeAsync();
    }

    [Fact]
    public async Task Start_WithoutRetry_SendsNothingAndAcceptsRemoteRequest()
    {
        HdlcPeer peer = harness.CreatePeer();
        HdlcPeerOptions options = harness.Options with { RetryInterval = null };
        TestObserver<HdlcPeerState> states = new();
        peer.StateChanged.Subscribe(states);

        Task starting = peer.StartAndConnect("port", harness.Address, harness.RemoteAddress, options).AsTask();
        await states.Next();
        await Task.Delay(100);
        Assert.Empty(harness.Written);
        harness.Receive(harness.Peer(HdlcWireFrameKind.SetAsynchronousBalancedMode));
        await starting.WaitAsync(timeout);

        Assert.Equal(HdlcWireFrameKind.UnnumberedAcknowledge, (await harness.NextWritten(0)).Kind);
        Assert.Equal([HdlcPeerState.Ready, HdlcPeerState.Connecting, HdlcPeerState.Connected], states.Seen);
        await peer.DisposeAsync();
    }

    [Fact]
    public async Task Start_ResendsRequestAtRetryIntervalUntilAnswered()
    {
        HdlcPeer peer = harness.CreatePeer();
        HdlcPeerOptions options = harness.Options with { RetryInterval = TimeSpan.FromMilliseconds(30) };

        Task starting = peer.StartAndConnect("port", harness.Address, harness.RemoteAddress, options).AsTask();
        HdlcWireFrame third = await harness.NextWritten(2);
        int beforeAnswer = harness.Written.Count;
        harness.Receive(harness.Peer(HdlcWireFrameKind.UnnumberedAcknowledge));
        await starting.WaitAsync(timeout);
        int atConnected = harness.Written.Count;
        await Task.Delay(150);

        Assert.Equal(HdlcWireFrameKind.SetAsynchronousBalancedMode, third.Kind);
        Assert.All(harness.Written.Take(beforeAnswer), frame => Assert.Equal(HdlcWireFrameKind.SetAsynchronousBalancedMode, HdlcWireFrame.Parse(frame).Kind));
        Assert.True(atConnected >= 3);
        Assert.Equal(atConnected, harness.Written.Count);
        await peer.DisposeAsync();
    }

    [Fact]
    public async Task Start_WhenRemoteRequestArrivesFirst_ConnectsWithoutFurtherRequests()
    {
        HdlcPeer peer = harness.CreatePeer();
        HdlcPeerOptions options = harness.Options with { RetryInterval = TimeSpan.FromMilliseconds(40) };

        Task starting = peer.StartAndConnect("port", harness.Address, harness.RemoteAddress, options).AsTask();
        await harness.NextWritten(0);
        harness.Receive(harness.Peer(HdlcWireFrameKind.SetAsynchronousBalancedMode));
        await starting.WaitAsync(timeout);
        int atConnected = harness.Written.Count;
        await Task.Delay(150);

        Assert.True(peer.IsConnected);
        Assert.Equal(HdlcWireFrameKind.UnnumberedAcknowledge, (await harness.NextWritten(atConnected - 1)).Kind);
        Assert.Equal(atConnected, harness.Written.Count);
        await peer.DisposeAsync();
    }

    [Fact]
    public async Task Start_WhenCanceled_ThrowsAndDisposesPeer()
    {
        using CancellationTokenSource cancellation = new();
        HdlcPeer peer = harness.CreatePeer();
        TestObserver<HdlcPeerState> states = new();
        peer.StateChanged.Subscribe(states);

        Task connecting = peer.StartAndConnect("port", harness.Address, harness.RemoteAddress, harness.Options, cancellation.Token).AsTask();
        await harness.NextWritten(0);
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => connecting);
        Assert.Equal(HdlcPeerState.Disconnected, peer.State);
        await states.Completed.WaitAsync(timeout);
        harness.Device.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    public async Task Connect_WhenCanceledWhileTheRequestWriteBlocks_ThrowsAndDisconnects()
    {
        HdlcPeer peer = harness.CreatePeer(TimeSpan.FromMilliseconds(200));
        using CancellationTokenSource cancellation = new();
        ManualResetEventSlim release = new();
        ManualResetEventSlim writing = new();
        harness.Device.Setup(x => x.Write(It.IsAny<ReadOnlyMemory<byte>>())).Callback(() =>
        {
            writing.Set();
            release.Wait(timeout);
        });
        harness.Device.Setup(x => x.Dispose()).Callback(release.Set);

        Task connecting = peer.StartAndConnect("port", harness.Address, harness.RemoteAddress, harness.Options, cancellation.Token).AsTask();
        Assert.True(writing.Wait(timeout));
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => connecting.WaitAsync(timeout));
        Assert.Equal(HdlcPeerState.Disconnected, peer.State);
        harness.Device.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    public async Task Start_WhenCanceledBeforeDeviceOpens_Throws()
    {
        await using HdlcPeer peer = harness.CreatePeer();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await peer.StartAndConnect("port", harness.Address, harness.RemoteAddress, harness.Options, new CancellationToken(true)));

        Assert.Equal(HdlcPeerState.Disconnected, peer.State);
    }

    [Fact]
    public async Task Start_WhenOpenFails_ThrowsAndDisconnects()
    {
        harness.Opener.Setup(x => x.Open(It.IsAny<string>(), It.IsAny<HdlcPeerOptions>())).Throws<IOException>();
        await using HdlcPeer peer = harness.CreatePeer();

        await Assert.ThrowsAsync<IOException>(async () => await peer.StartAndConnect("port", harness.Address, harness.RemoteAddress, harness.Options));

        Assert.Equal(HdlcPeerState.Disconnected, peer.State);
    }

    [Fact]
    public async Task Start_WhenWriteFails_Throws()
    {
        harness.Device.Setup(x => x.Write(It.IsAny<ReadOnlyMemory<byte>>())).Throws<IOException>();
        await using HdlcPeer peer = harness.CreatePeer();

        await Assert.ThrowsAsync<IOException>(async () => await peer.StartAndConnect("port", harness.Address, harness.RemoteAddress, harness.Options));

        Assert.Equal(HdlcPeerState.Disconnected, peer.State);
    }

    [Fact]
    public async Task Start_WhenDeviceClosesBeforeEstablished_ThrowsIoException()
    {
        await using HdlcPeer peer = harness.CreatePeer();
        Task connecting = peer.StartAndConnect("port", harness.Address, harness.RemoteAddress, harness.Options).AsTask();
        await harness.NextWritten(0);

        harness.EndOfInput();

        await Assert.ThrowsAsync<IOException>(() => connecting);
        Assert.Equal(HdlcPeerState.Disconnected, peer.State);
    }

    [Fact]
    public async Task Start_WithoutRetry_WhenCanceled_Throws()
    {
        using CancellationTokenSource cancellation = new(TimeSpan.FromMilliseconds(100));
        await using HdlcPeer peer = harness.CreatePeer();
        HdlcPeerOptions options = harness.Options with { RetryInterval = null };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await peer.StartAndConnect("port", harness.Address, harness.RemoteAddress, options, cancellation.Token));

        Assert.Equal(HdlcPeerState.Disconnected, peer.State);
    }

    [Fact]
    public async Task Start_WhenAlreadyStartedOrDisposed_Throws()
    {
        await using HdlcPeer connected = await harness.Connect();

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await connected.StartAndConnect("port", harness.Address, harness.RemoteAddress, harness.Options));
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await connected.StartAndConnect("port", harness.Address, harness.RemoteAddress, harness.Options));

        await using HdlcPeer disposed = harness.CreatePeer();
        await disposed.DisposeAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await disposed.StartAndConnect("port", harness.Address, harness.RemoteAddress, harness.Options));
    }

    [Fact]
    public async Task Receiver_IsGivenTheFrameDataAndTheFrameIsAcknowledged()
    {
        await using HdlcPeer peer = await harness.Connect();
        PayloadObserver observer = new();
        peer.Receiver = observer.Receive;

        harness.Receive(harness.Peer(HdlcWireFrameKind.Information, false, 0, new byte[] { 1, 2, 3 }));

        Assert.Equal(new byte[] { 1, 2, 3 }, await observer.Next());
        HdlcWireFrame acknowledgement = await harness.NextWritten(1);
        Assert.Equal(HdlcWireFrameKind.ReceiveReady, acknowledgement.Kind);
        Assert.Equal(1, acknowledgement.ReceiveSequence);
    }

    [Fact]
    public async Task Receiver_MemoryStaysValidWhileTheDelegateIsRunningAndLaterFramesWait()
    {
        await using HdlcPeer peer = await harness.Connect();
        TaskCompletionSource release = new();
        TaskCompletionSource firstStarted = new();
        List<byte[]> seen = [];
        peer.Receiver = data =>
        {
            if (seen.Count == 0)
            {
                firstStarted.SetResult();
                release.Task.Wait();
            }

            lock (seen)
            {
                seen.Add(data.Memory.ToArray());
            }

            data.Dispose();
        };

        harness.Receive(harness.Peer(HdlcWireFrameKind.Information, false, 0, new byte[] { 1, 1 }));
        await firstStarted.Task.WaitAsync(timeout);
        harness.Receive(harness.Peer(HdlcWireFrameKind.Information, false, 1, new byte[] { 2, 2 }));
        await Task.Delay(100);
        lock (seen)
        {
            Assert.Empty(seen);
        }

        release.SetResult();
        await Eventually(() =>
        {
            lock (seen)
            {
                return seen.Count == 2;
            }
        });

        Assert.Equal(new byte[] { 1, 1 }, seen[0]);
        Assert.Equal(new byte[] { 2, 2 }, seen[1]);
    }

    [Fact]
    public async Task Receiver_OwnerOutlivesTheCallUntilTheDelegateDisposesIt()
    {
        await using HdlcPeer peer = await harness.Connect();
        List<IMemoryOwner<byte>> kept = [];
        peer.Receiver = data =>
        {
            lock (kept)
            {
                kept.Add(data);
            }
        };

        harness.Receive(harness.Peer(HdlcWireFrameKind.Information, false, 0, new byte[] { 1, 1 }));
        harness.Receive(harness.Peer(HdlcWireFrameKind.Information, false, 1, new byte[] { 2, 2, 2 }));
        await Eventually(() =>
        {
            lock (kept)
            {
                return kept.Count == 2;
            }
        });

        Assert.Equal(new byte[] { 1, 1 }, kept[0].Memory.ToArray());
        Assert.Equal(new byte[] { 2, 2, 2 }, kept[1].Memory.ToArray());
        kept[0].Dispose();
        Assert.Throws<ObjectDisposedException>(() => kept[0].Memory);
        Assert.Equal(new byte[] { 2, 2, 2 }, kept[1].Memory.ToArray());
        kept[1].Dispose();
    }

    [Fact]
    public async Task Receiver_WhenTheDelegateThrows_ReportsItOnExceptionsAndKeepsTheConnection()
    {
        await using HdlcPeer peer = await harness.Connect();
        TestObserver<Exception> exceptions = new();
        peer.Exceptions.Subscribe(exceptions);
        InvalidOperationException failure = new("receiver failed");
        PayloadObserver healthy = new();
        int calls = 0;
        peer.Receiver = data =>
        {
            if (Interlocked.Increment(ref calls) == 1)
            {
                throw failure;
            }

            healthy.Receive(data);
        };

        harness.Receive(harness.Peer(HdlcWireFrameKind.Information, false, 0, new byte[] { 4 }));
        harness.Receive(harness.Peer(HdlcWireFrameKind.Information, false, 1, new byte[] { 5 }));
        harness.Receive(harness.Peer(HdlcWireFrameKind.Information, false, 2, new byte[] { 6 }));

        Assert.Same(failure, await exceptions.Next());
        Assert.Equal([new byte[] { 5 }, new byte[] { 6 }], await healthy.Next(2));
        Assert.True(peer.IsConnected);
        Assert.Equal(HdlcPeerState.Connected, peer.State);
    }

    [Fact]
    public async Task Exceptions_WhenAStateObserverThrows_ReportsItAndTheConnectionCarriesOn()
    {
        await using HdlcPeer peer = await harness.Connect();
        TestObserver<Exception> exceptions = new();
        peer.Exceptions.Subscribe(exceptions);
        InvalidOperationException failure = new("observer failed");
        peer.StateChanged.Subscribe(new CallbackObserver<HdlcPeerState>(_ => throw failure));

        harness.Receive(harness.Peer(HdlcWireFrameKind.Disconnect));

        Assert.Same(failure, await exceptions.Next());
    }

    [Fact]
    public async Task Exceptions_WhenAFrameIsTooShortToParse_ReportsItAndKeepsReceiving()
    {
        await using HdlcPeer peer = await harness.Connect();
        TestObserver<Exception> exceptions = new();
        peer.Exceptions.Subscribe(exceptions);
        PayloadObserver payloads = new();
        peer.Receiver = payloads.Receive;

        harness.Receive(new byte[] { 0x21 });
        harness.Receive(harness.Peer(HdlcWireFrameKind.Information, false, 0, new byte[] { 7 }));

        Assert.IsType<HdlcWireFrameException>(await exceptions.Next());
        Assert.Equal(new byte[] { 7 }, await payloads.Next());
        Assert.True(peer.IsConnected);
    }

    [Fact]
    public async Task Exceptions_CompletesTogetherWithTheStateStream()
    {
        HdlcPeer peer = await harness.Connect();
        TestObserver<Exception> exceptions = new();
        peer.Exceptions.Subscribe(exceptions);

        await peer.DisposeAsync();

        await exceptions.Completed.WaitAsync(timeout);
    }

    [Fact]
    public async Task Exceptions_ObserversRunOutsideThePeersLocks()
    {
        await using HdlcPeer peer = await harness.Connect();
        TaskCompletionSource sent = new();
        peer.Exceptions.Subscribe(new CallbackObserver<Exception>(_ =>
        {
            peer.Send(new byte[] { 1 }).AsTask().Wait();
            sent.SetResult();
        }));

        harness.Receive(new byte[] { 0x21 });

        await sent.Task.WaitAsync(timeout);
    }

    [Fact]
    public async Task Receiver_CanBeReplacedWhileConnected()
    {
        await using HdlcPeer peer = await harness.Connect();
        PayloadObserver first = new();
        PayloadObserver second = new();
        peer.Receiver = first.Receive;
        harness.Receive(harness.Peer(HdlcWireFrameKind.Information, false, 0, new byte[] { 1 }));
        Assert.Equal([new byte[] { 1 }], await first.Next(1));

        peer.Receiver = second.Receive;
        harness.Receive(harness.Peer(HdlcWireFrameKind.Information, false, 1, new byte[] { 2 }));

        Assert.Equal([new byte[] { 2 }], await second.Next(1));
    }

    [Fact]
    public async Task Receiver_DeliversDataReceivedBeforeTheLinkEnded()
    {
        await using HdlcPeer peer = await harness.Connect();
        TaskCompletionSource release = new();
        PayloadObserver observer = new();
        peer.Receiver = data =>
        {
            release.Task.Wait();
            observer.Receive(data);
        };
        harness.Receive(harness.Peer(HdlcWireFrameKind.Information, false, 0, new byte[] { 8 }));
        harness.Receive(harness.Peer(HdlcWireFrameKind.DisconnectedMode, false));
        await Eventually(() => peer.State == HdlcPeerState.Disconnected);

        release.SetResult();

        Assert.Equal([new byte[] { 8 }], await observer.Next(1));
    }

    [Fact]
    public async Task Receiver_WhenNotSet_DataIsAcknowledgedAndDiscarded()
    {
        await using HdlcPeer peer = await harness.Connect();

        harness.Receive(harness.Peer(HdlcWireFrameKind.Information, false, 0, new byte[] { 3 }));

        HdlcWireFrame acknowledgement = await harness.NextWritten(1);
        Assert.Equal(HdlcWireFrameKind.ReceiveReady, acknowledgement.Kind);
        Assert.Equal(1, acknowledgement.ReceiveSequence);
        Assert.Null(peer.Receiver);
    }

    [Fact]
    public async Task Received_MalformedFrame_IsDroppedAndLoopContinues()
    {
        await using HdlcPeer peer = await harness.Connect();
        PayloadObserver observer = new();
        peer.Receiver = observer.Receive;

        harness.Receive([0xFF]);
        harness.Receive(harness.Peer(HdlcWireFrameKind.Information, false, 0, new byte[] { 9 }));

        Assert.Equal(new byte[] { 9 }, await observer.Next());
    }

    [Fact]
    public async Task Received_WithNoObserver_StillAcknowledges()
    {
        await using HdlcPeer peer = await harness.Connect();

        harness.Receive(harness.Peer(HdlcWireFrameKind.Information, false, 0, new byte[] { 1 }));

        Assert.Equal(HdlcWireFrameKind.ReceiveReady, (await harness.NextWritten(1)).Kind);
    }

    [Fact]
    public async Task Send_Memory_WritesInformationFrameWithIncreasingSequence()
    {
        await using HdlcPeer peer = await harness.Connect();

        await peer.Send(new byte[] { 5, 6 });
        await peer.Send(new byte[] { 7 });

        HdlcWireFrame first = await harness.NextWritten(1);
        HdlcWireFrame second = await harness.NextWritten(2);
        Assert.Equal(HdlcWireFrameKind.Information, first.Kind);
        Assert.Equal(0, first.SendSequence);
        Assert.Equal(new byte[] { 5, 6 }, first.Payload.ToArray());
        Assert.Equal(1, second.SendSequence);
    }

    [Fact]
    public async Task Send_Owner_WritesFrameWithoutCopyingAndDisposesOwnerOnceAcknowledged()
    {
        await using HdlcPeer peer = await harness.Connect();
        bool disposed = false;
        Mock<IMemoryOwner<byte>> owner = new();
        owner.SetupGet(x => x.Memory).Returns(new byte[] { 4, 4 });
        owner.Setup(x => x.Dispose()).Callback(() => disposed = true);

        await peer.Send(owner.Object);

        Assert.Equal(new byte[] { 4, 4 }, (await harness.NextWritten(1)).Payload.ToArray());
        Assert.False(disposed);
        harness.Receive(harness.Peer(HdlcWireFrameKind.ReceiveReady, false, receiveSequence: 1));
        await Eventually(() => disposed);
        owner.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    public async Task Send_Owner_WhenTheWriteFails_DisposesOwnerAndThrows()
    {
        await using HdlcPeer peer = await harness.Connect();
        harness.Device.Setup(x => x.Write(It.IsAny<ReadOnlyMemory<byte>>())).Throws<IOException>();
        Mock<IMemoryOwner<byte>> owner = new();
        owner.SetupGet(x => x.Memory).Returns(new byte[] { 4, 4 });

        await Assert.ThrowsAsync<IOException>(async () => await peer.Send(owner.Object));

        owner.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    public async Task Send_Owner_WhenThePeerEndsBeforeItIsAcknowledged_DisposesOwner()
    {
        HdlcPeer peer = await harness.Connect();
        Mock<IMemoryOwner<byte>> owner = new();
        owner.SetupGet(x => x.Memory).Returns(new byte[] { 4, 4 });
        await peer.Send(owner.Object);
        owner.Verify(x => x.Dispose(), Times.Never);

        await peer.DisposeAsync();

        owner.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    public async Task Send_Owner_WhenNotConnected_DisposesOwnerAndThrows()
    {
        await using HdlcPeer peer = harness.CreatePeer();
        Mock<IMemoryOwner<byte>> owner = new();
        owner.SetupGet(x => x.Memory).Returns(new byte[] { 1 });

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await peer.Send(owner.Object));

        owner.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    public async Task Send_WhenWriteFails_Throws()
    {
        await using HdlcPeer peer = await harness.Connect();
        harness.Device.Setup(x => x.Write(It.IsAny<ReadOnlyMemory<byte>>())).Throws<IOException>();

        await Assert.ThrowsAsync<IOException>(async () => await peer.Send(new byte[] { 1 }));
    }

    [Fact]
    public async Task Receive_PeerDisconnect_EmitsDisconnectedOnceCompletesTheStateStreamAndAcknowledges()
    {
        HdlcPeer peer = await harness.Connect();
        TestObserver<HdlcPeerState> states = new();
        peer.StateChanged.Subscribe(states);

        harness.Receive(harness.Peer(HdlcWireFrameKind.Disconnect));

        await states.Completed.WaitAsync(timeout);
        Assert.Equal([HdlcPeerState.Disconnected], states.Seen);
        Assert.Equal(HdlcWireFrameKind.UnnumberedAcknowledge, (await harness.NextWritten(1)).Kind);
        Assert.False(peer.IsConnected);
        await peer.DisposeAsync();
        Assert.Equal([HdlcPeerState.Disconnected], states.Seen);
    }

    [Fact]
    public async Task Receive_EndOfInputWhileConnected_EmitsDisconnected()
    {
        await using HdlcPeer peer = await harness.Connect();
        TestObserver<HdlcPeerState> states = new();
        peer.StateChanged.Subscribe(states);

        harness.EndOfInput();

        await states.Completed.WaitAsync(timeout);
        Assert.Equal([HdlcPeerState.Disconnected], states.Seen);
    }

    [Fact]
    public async Task Subscribe_AfterDisconnect_CompletesTheStateStreamImmediately()
    {
        HdlcPeer peer = await harness.Connect();
        await peer.DisposeAsync();
        TestObserver<HdlcPeerState> states = new();

        peer.StateChanged.Subscribe(states);

        await states.Completed.WaitAsync(timeout);
    }

    [Fact]
    public async Task DisposeAsync_WhenConnected_SendsDisconnectDisablesReceiverAndDisposesDevice()
    {
        HdlcPeer peer = await harness.Connect();
        TestObserver<HdlcPeerState> states = new();
        peer.StateChanged.Subscribe(states);

        await peer.DisposeAsync();
        await peer.DisposeAsync();

        Assert.Equal(HdlcWireFrameKind.Disconnect, (await harness.NextWritten(1)).Kind);
        harness.Device.Verify(x => x.DisableReceiver(), Times.Once);
        harness.Device.Verify(x => x.Dispose(), Times.Once);
        Assert.Equal([HdlcPeerState.Disconnected], states.Seen);
    }

    [Fact]
    public async Task DisposeAsync_WhenDisconnectWriteFails_StillDisposesDevice()
    {
        HdlcPeer peer = await harness.Connect();
        harness.Device.Setup(x => x.Write(It.IsAny<ReadOnlyMemory<byte>>())).Throws<IOException>();

        await peer.DisposeAsync();

        harness.Device.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    public async Task DisposeAsync_WhenReceiveLoopFaulted_DoesNotThrow()
    {
        HdlcPeer peer = await harness.Connect();
        harness.Device.Setup(x => x.Write(It.IsAny<ReadOnlyMemory<byte>>())).Throws<IOException>();
        harness.Receive(harness.Peer(HdlcWireFrameKind.Information, false, 0, new byte[] { 1 }));
        await Task.Delay(200);

        await peer.DisposeAsync();

        harness.Device.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    public async Task DisposeAsync_BeforeStart_DisconnectsWithoutOpeningDevice()
    {
        HdlcPeer peer = harness.CreatePeer();
        TestObserver<HdlcPeerState> states = new();
        peer.StateChanged.Subscribe(states);

        await peer.DisposeAsync();

        Assert.Equal(HdlcPeerState.Disconnected, peer.State);
        await states.Completed.WaitAsync(timeout);
        harness.Opener.Verify(x => x.Open(It.IsAny<string>(), It.IsAny<HdlcPeerOptions>()), Times.Never);
        Assert.Empty(harness.Written);
    }

    [Fact]
    public async Task Dispose_BlocksUntilDisposed()
    {
        HdlcPeer peer = await harness.Connect();

        peer.Dispose();

        harness.Device.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    public async Task Start_WithoutOptions_UsesDefaults()
    {
        HdlcPeerOptions? captured = null;
        harness.Opener.Setup(x => x.Open("port", It.IsAny<HdlcPeerOptions>())).Callback<string, HdlcPeerOptions>((_, passed) => captured = passed).Returns(harness.Device.Object);
        await using HdlcPeer peer = harness.CreatePeer();
        Task starting = peer.StartAndConnect("port", harness.Address, harness.RemoteAddress).AsTask();
        await harness.NextWritten(0);
        harness.EndOfInput();

        await Assert.ThrowsAsync<IOException>(() => starting);

        Assert.Equal(new HdlcPeerOptions(), captured);
        Assert.Equal(TimeSpan.FromSeconds(1), captured!.RetryInterval);
        Assert.Equal(TimeSpan.FromSeconds(1), captured.RetransmitInterval);
        Assert.Equal(21, captured.MaxRetransmissions);
        Assert.False(captured.DisablePollFinalBit);
        Assert.Equal(7, captured.TransmitWindow);
        Assert.Equal(1500, captured.MaxInfoField);
        Assert.Equal(HdlcEncoding.Nrz, captured.Link.Encoding);
        Assert.Equal(HdlcCrc.Crc32Ccitt, captured.Link.Crc);
        Assert.Equal(HdlcIdlePattern.Flags, captured.IdlePattern);
        Assert.False(captured.Loopback);
        Assert.Equal(HdlcReceiveClockSource.OwnPin, captured.Link.ReceiveClockSource);
        Assert.Equal(HdlcTransmitClockSource.OwnPin, captured.Link.TransmitClockSource);
        Assert.Equal(HdlcPhaseLockedLoopDivisor.DivideBy32, captured.Link.PhaseLockedLoopDivisor);
        Assert.Equal(HdlcUnderrunAction.Abort7, captured.UnderrunAction);
        Assert.Equal(4800, captured.Link.ClockSpeed);
        Assert.Equal(HdlcPreambleLength.Bits8, captured.PreambleLength);
        Assert.Equal(HdlcPreamblePattern.None, captured.PreamblePattern);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task Start_WithNonPositiveRetryInterval_ThrowsAndStaysIdle(int milliseconds)
    {
        await using HdlcPeer peer = harness.CreatePeer();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await peer.StartAndConnect("port", harness.Address, harness.RemoteAddress, new HdlcPeerOptions { RetryInterval = TimeSpan.FromMilliseconds(milliseconds) }));

        Assert.Equal(HdlcPeerState.Idle, peer.State);
        harness.Opener.Verify(x => x.Open(It.IsAny<string>(), It.IsAny<HdlcPeerOptions>()), Times.Never);
    }

    [Fact]
    public async Task Start_OnLinux_UsesLinuxOpenerOnly()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        Mock<IMicroGateDeviceOpener> windowsOpener = new();
        await using HdlcPeer peer = new(harness.Opener.Object, windowsOpener.Object);
        Task starting = peer.StartAndConnect("ttySLG0", harness.Address, harness.RemoteAddress, harness.Options).AsTask();
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
        await using HdlcPeer peer = new(linuxOpener.Object, harness.Opener.Object);
        Task starting = peer.StartAndConnect("COM3", harness.Address, harness.RemoteAddress, harness.Options).AsTask();
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

        await using HdlcPeer peer = harness.CreatePeer();

        await Assert.ThrowsAsync<PlatformNotSupportedException>(async () => await peer.StartAndConnect("port", harness.Address, harness.RemoteAddress));

        Assert.Equal(HdlcPeerState.Idle, peer.State);
    }

    [Fact]
    public async Task Send_BeforeStart_Throws()
    {
        await using HdlcPeer peer = harness.CreatePeer();

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await peer.Send(new byte[] { 1 }));
    }

    [Fact]
    public async Task Start_WithNonPositiveRetransmitInterval_ThrowsAndStaysIdle()
    {
        await using HdlcPeer peer = harness.CreatePeer();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await peer.StartAndConnect("port", harness.Address, harness.RemoteAddress, new HdlcPeerOptions { RetransmitInterval = TimeSpan.Zero }));

        Assert.Equal(HdlcPeerState.Idle, peer.State);
    }

    [Fact]
    public async Task ReceiveLoop_WhenResponseWriteFails_DisconnectsAndCompletesTheStateStream()
    {
        HdlcPeer peer = await harness.Connect();
        TestObserver<HdlcPeerState> states = new();
        peer.StateChanged.Subscribe(states);
        harness.Device.Setup(x => x.Write(It.IsAny<ReadOnlyMemory<byte>>())).Throws<IOException>();

        harness.Receive(harness.Peer(HdlcWireFrameKind.Information, false, 0, new byte[] { 1 }));

        await states.Completed.WaitAsync(timeout);
        Assert.Equal([HdlcPeerState.Disconnected], states.Seen);
        Assert.Equal(HdlcPeerState.Disconnected, peer.State);
        await peer.DisposeAsync();
        harness.Device.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    public async Task ReceiveLoop_WhenReadThrowsBeforeConnected_StartFailsWithIoException()
    {
        harness.Device.Setup(x => x.Read(It.IsAny<byte[]>())).Throws<InvalidOperationException>();
        await using HdlcPeer peer = harness.CreatePeer();

        IOException exception = await Assert.ThrowsAsync<IOException>(async () => await peer.StartAndConnect("port", harness.Address, harness.RemoteAddress, harness.Options with { RetryInterval = null }));

        Assert.IsType<InvalidOperationException>(exception.InnerException);
        Assert.Equal(HdlcPeerState.Disconnected, peer.State);
    }

    [Fact]
    public async Task DisposeAsync_WhileDeviceIsOpening_DisposesTheDeviceAndFailsStart()
    {
        ManualResetEventSlim release = new();
        ManualResetEventSlim opening = new();
        harness.Opener.Setup(x => x.Open(It.IsAny<string>(), It.IsAny<HdlcPeerOptions>())).Returns(() =>
        {
            opening.Set();
            release.Wait(timeout);
            return harness.Device.Object;
        });
        HdlcPeer peer = harness.CreatePeer();
        Task starting = peer.StartAndConnect("port", harness.Address, harness.RemoteAddress, harness.Options).AsTask();
        await Task.Run(() => opening.Wait(timeout));

        await peer.DisposeAsync();
        release.Set();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => starting);
        harness.Device.Verify(x => x.Dispose(), Times.Once);
        Assert.Equal(HdlcPeerState.Disconnected, peer.State);
        Assert.Empty(harness.Written);
    }

    [Fact]
    public async Task Dispose_FromDisconnectedObserver_DoesNotDeadlockAndClosesDevice()
    {
        HdlcPeer peer = await harness.Connect();
        TaskCompletionSource disposedInCallback = new();
        peer.StateChanged.Subscribe(new CallbackObserver<HdlcPeerState>(state =>
        {
            if (state == HdlcPeerState.Disconnected)
            {
                peer.Dispose();
                disposedInCallback.TrySetResult();
            }
        }));

        harness.Receive(harness.Peer(HdlcWireFrameKind.Disconnect));

        await disposedInCallback.Task.WaitAsync(timeout);
        await Eventually(() => harness.Device.Invocations.Count(invocation => invocation.Method.Name == nameof(IMicroGateDevice.Dispose)) == 1);
    }

    [Fact]
    public async Task Dispose_FromTheReceiver_SendsDisconnectAndClosesDeviceAfterLoopEnds()
    {
        HdlcPeer peer = await harness.Connect();
        TaskCompletionSource disposedInCallback = new();
        peer.Receiver = _ =>
        {
            peer.Dispose();
            disposedInCallback.TrySetResult();
        };

        harness.Receive(harness.Peer(HdlcWireFrameKind.Information, false, 0, new byte[] { 1 }));

        await disposedInCallback.Task.WaitAsync(timeout);
        await Eventually(() => harness.Device.Invocations.Count(invocation => invocation.Method.Name == nameof(IMicroGateDevice.Dispose)) == 1);
        Assert.Equal(HdlcPeerState.Disconnected, peer.State);
        Assert.Contains(harness.Written, frame => HdlcWireFrame.Parse(frame).Kind == HdlcWireFrameKind.Disconnect);
    }

    [Fact]
    public async Task StateChanged_ObserversRunOutsideTheStateLock()
    {
        HdlcPeer peer = harness.CreatePeer();
        TaskCompletionSource<HdlcPeerState> otherThreadRead = new();
        peer.StateChanged.Subscribe(new CallbackObserver<HdlcPeerState>(_ =>
        {
            Task<HdlcPeerState> read = Task.Run(() => peer.State);
            if (read.Wait(timeout))
            {
                otherThreadRead.TrySetResult(read.Result);
            }
        }));
        Task starting = peer.StartAndConnect("port", harness.Address, harness.RemoteAddress, harness.Options).AsTask();

        Assert.Equal(HdlcPeerState.Ready, await otherThreadRead.Task.WaitAsync(timeout));

        await harness.NextWritten(0);
        harness.Receive(harness.Peer(HdlcWireFrameKind.UnnumberedAcknowledge));
        await starting.WaitAsync(timeout);
        await peer.DisposeAsync();
    }

    [Fact]
    public async Task Send_MoreThanTheWindow_WaitsForAcknowledgement()
    {
        await using HdlcPeer peer = await harness.Connect();
        for (int i = 0; i < 7; i++)
        {
            await peer.Send(new byte[] { (byte)i });
        }

        Task eighth = peer.Send(new byte[] { 7 }).AsTask();
        await Task.Delay(100);
        Assert.False(eighth.IsCompleted);

        harness.Receive(harness.Peer(HdlcWireFrameKind.ReceiveReady, false, receiveSequence: 3));
        await eighth.WaitAsync(timeout);

        HdlcWireFrame last = await harness.NextWritten(8);
        Assert.Equal(7, last.SendSequence);
        Assert.Equal(new byte[] { 7 }, last.Payload.ToArray());
    }

    [Fact]
    public async Task Retransmit_TimerStartsWhenTheFrameHasBeenWrittenNotWhenItWasCreated()
    {
        HdlcPeerOptions slow = harness.Options with { RetransmitInterval = TimeSpan.FromMilliseconds(400) };
        HdlcPeer peer = harness.CreatePeer();
        Task starting = peer.StartAndConnect("port", harness.Address, harness.RemoteAddress, slow).AsTask();
        await harness.NextWritten(0);
        harness.Receive(harness.Peer(HdlcWireFrameKind.UnnumberedAcknowledge));
        await starting.WaitAsync(timeout);
        int slowWrites = 0;
        harness.Device.Setup(x => x.Write(It.IsAny<ReadOnlyMemory<byte>>())).Callback<ReadOnlyMemory<byte>>(frame =>
        {
            if (HdlcWireFrame.Parse(frame).Kind == HdlcWireFrameKind.Information && Interlocked.Increment(ref slowWrites) == 1)
            {
                Thread.Sleep(600);
            }

            harness.Record(frame);
        });

        await peer.Send(new byte[] { 9 });
        int afterFirstWrite = harness.Written.Count;
        await Task.Delay(250);

        Assert.Equal(afterFirstWrite, harness.Written.Count);
        await Eventually(() => harness.Written.Count > afterFirstWrite);
        await peer.DisposeAsync();
    }

    [Fact]
    public async Task Send_Owner_AfterThePeerHasEnded_ThrowsAndDisposesOwner()
    {
        HdlcPeer peer = await harness.Connect();
        harness.Receive(harness.Peer(HdlcWireFrameKind.Disconnect));
        await Eventually(() => peer.State == HdlcPeerState.Disconnected);
        Mock<IMemoryOwner<byte>> owner = new();
        owner.SetupGet(x => x.Memory).Returns(new byte[] { 1 });

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await peer.Send(owner.Object));

        owner.Verify(x => x.Dispose(), Times.Once);
        await peer.DisposeAsync();
    }

    [Fact]
    public async Task Send_WhileTheRemotePeerIsNotReady_WaitsUntilItIsReadyAgain()
    {
        await using HdlcPeer peer = await harness.Connect();
        harness.Receive(harness.Peer(HdlcWireFrameKind.ReceiveNotReady, false));
        await Task.Delay(100);

        Task sending = peer.Send(new byte[] { 1 }).AsTask();
        await Task.Delay(150);
        Assert.False(sending.IsCompleted);

        harness.Receive(harness.Peer(HdlcWireFrameKind.ReceiveReady, false));
        await sending.WaitAsync(timeout);

        Assert.Equal(new byte[] { 1 }, (await harness.NextWritten(1)).Payload.ToArray());
    }

    [Fact]
    public async Task Send_WaitingForARemotePeerThatIsNotReady_CanBeCanceled()
    {
        await using HdlcPeer peer = await harness.Connect();
        harness.Receive(harness.Peer(HdlcWireFrameKind.ReceiveNotReady, false));
        await Task.Delay(100);
        using CancellationTokenSource cancellation = new(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await peer.Send(new byte[] { 1 }, cancellation.Token));
    }

    [Fact]
    public async Task Send_WaitingForWindow_CanBeCanceled()
    {
        await using HdlcPeer peer = await harness.Connect();
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
        HdlcPeer peer = await harness.Connect();
        for (int i = 0; i < 7; i++)
        {
            await peer.Send(new byte[] { (byte)i });
        }

        Task waiting = peer.Send(new byte[] { 7 }).AsTask();
        await Task.Delay(50);
        harness.Receive(harness.Peer(HdlcWireFrameKind.Disconnect));

        await Assert.ThrowsAsync<IOException>(() => waiting);
        await peer.DisposeAsync();
    }

    [Fact]
    public async Task Send_AfterAcknowledgements_KeepsDeliveringBeyondTheWindow()
    {
        await using HdlcPeer peer = await harness.Connect();

        for (int i = 0; i < 30; i++)
        {
            await peer.Send(new byte[] { (byte)i });
            harness.Receive(harness.Peer(HdlcWireFrameKind.ReceiveReady, false, receiveSequence: (i + 1) % 8));
        }

        HdlcWireFrame last = await harness.NextWritten(30);
        Assert.Equal(29 % 8, last.SendSequence);
        Assert.Equal(new byte[] { 29 }, last.Payload.ToArray());
    }

    [Fact]
    public async Task Receive_Reject_SendsRejectedFramesAgain()
    {
        await using HdlcPeer peer = await harness.Connect();
        for (int i = 0; i < 3; i++)
        {
            await peer.Send(new byte[] { (byte)(20 + i) });
        }

        harness.Receive(harness.Peer(HdlcWireFrameKind.Reject, false, receiveSequence: 1));

        HdlcWireFrame again = await harness.NextWritten(4);
        HdlcWireFrame andAgain = await harness.NextWritten(5);
        Assert.Equal(1, again.SendSequence);
        Assert.Equal(new byte[] { 21 }, again.Payload.ToArray());
        Assert.Equal(2, andAgain.SendSequence);
        Assert.Equal(new byte[] { 22 }, andAgain.Payload.ToArray());
    }

    [Fact]
    public async Task Retransmit_WhenNothingIsAcknowledged_SendsUnacknowledgedFramesAgainUntilAcknowledged()
    {
        HdlcPeerOptions timed = harness.Options with { RetransmitInterval = TimeSpan.FromMilliseconds(60), DisablePollFinalBit = true };
        HdlcPeer peer = harness.CreatePeer();
        Task starting = peer.StartAndConnect("port", harness.Address, harness.RemoteAddress, timed).AsTask();
        await harness.NextWritten(0);
        harness.Receive(harness.Peer(HdlcWireFrameKind.UnnumberedAcknowledge));
        await starting.WaitAsync(timeout);

        await peer.Send(new byte[] { 5 });
        HdlcWireFrame original = await harness.NextWritten(1);
        HdlcWireFrame repeat = await harness.NextWritten(2);
        harness.Receive(harness.Peer(HdlcWireFrameKind.ReceiveReady, false, receiveSequence: 1));
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
    public async Task Retransmit_WhenNothingIsAcknowledged_PollsTheRemotePeerAndSendsFramesAgainOnlyIfItsAnswerShowsTheyWereLost()
    {
        HdlcPeerOptions timed = harness.Options with { RetransmitInterval = TimeSpan.FromMilliseconds(400) };
        HdlcPeer peer = harness.CreatePeer();
        Task starting = peer.StartAndConnect("port", harness.Address, harness.RemoteAddress, timed).AsTask();
        await harness.NextWritten(0);
        harness.Receive(harness.Peer(HdlcWireFrameKind.UnnumberedAcknowledge));
        await starting.WaitAsync(timeout);

        await peer.Send(new byte[] { 5 });
        HdlcWireFrame original = await harness.NextWritten(1);
        HdlcWireFrame poll = await harness.NextWritten(2);
        harness.Receive(harness.Peer(HdlcWireFrameKind.ReceiveReady, true, receiveSequence: 0));
        HdlcWireFrame repeat = await harness.NextWritten(3);
        harness.Receive(harness.Peer(HdlcWireFrameKind.ReceiveReady, true, receiveSequence: 1));
        await Task.Delay(150);
        int afterAck = harness.Written.Count;
        await Task.Delay(900);

        Assert.Equal(HdlcWireFrameKind.Information, original.Kind);
        Assert.Equal(HdlcWireFrameKind.ReceiveReady, poll.Kind);
        Assert.Equal(harness.RemoteAddress, poll.Address);
        Assert.True(poll.PollFinal);
        Assert.Equal(HdlcWireFrameKind.Information, repeat.Kind);
        Assert.Equal(new byte[] { 5 }, repeat.Payload.ToArray());
        Assert.Equal(afterAck, harness.Written.Count);
        await peer.DisposeAsync();
    }

    [Fact]
    public async Task Retransmit_WhenNothingIsAcknowledged_PollsAgainUntilTheRemotePeerAnswers()
    {
        HdlcPeerOptions timed = harness.Options with { RetransmitInterval = TimeSpan.FromMilliseconds(60) };
        HdlcPeer peer = harness.CreatePeer();
        Task starting = peer.StartAndConnect("port", harness.Address, harness.RemoteAddress, timed).AsTask();
        await harness.NextWritten(0);
        harness.Receive(harness.Peer(HdlcWireFrameKind.UnnumberedAcknowledge));
        await starting.WaitAsync(timeout);

        await peer.Send(new byte[] { 5 });
        HdlcWireFrame firstPoll = await harness.NextWritten(2);
        HdlcWireFrame secondPoll = await harness.NextWritten(3);

        Assert.True(firstPoll.PollFinal);
        Assert.True(secondPoll.PollFinal);
        Assert.Equal(HdlcWireFrameKind.ReceiveReady, secondPoll.Kind);
        await peer.DisposeAsync();
    }

    [Fact]
    public async Task Retransmit_WithoutInterval_NeverSendsOnItsOwn()
    {
        await using HdlcPeer peer = await harness.Connect();

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
        await using HdlcPeer peer = await harness.Connect();
        Mock<IMemoryOwner<byte>> owner = new();
        owner.SetupGet(x => x.Memory).Returns(new byte[peer.MaxPayloadSize + 1]);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await peer.Send(new byte[peer.MaxPayloadSize + 1]));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await peer.Send(owner.Object));

        owner.Verify(x => x.Dispose(), Times.Once);
        Assert.Equal(1500, peer.MaxPayloadSize);
    }

    [Fact]
    public async Task Send_PayloadOfTheMaximum_IsSent()
    {
        await using HdlcPeer peer = await harness.Connect();

        await peer.Send(new byte[peer.MaxPayloadSize]);

        Assert.Equal(peer.MaxPayloadSize, (await harness.NextWritten(1)).Payload.Length);
    }

    [Fact]
    public async Task Received_WithNoObserver_DoesNotBreakLaterSubscribers()
    {
        await using HdlcPeer peer = await harness.Connect();
        harness.Receive(harness.Peer(HdlcWireFrameKind.Information, false, 0, new byte[] { 1 }));
        await harness.NextWritten(1);
        PayloadObserver late = new();
        peer.Receiver = late.Receive;

        harness.Receive(harness.Peer(HdlcWireFrameKind.Information, false, 1, new byte[] { 2 }));

        byte[] first = await late.Next();
        byte[] latest = first[0] == 2 ? first : await late.Next();
        Assert.Equal(new byte[] { 2 }, latest);
    }

    [Fact]
    public async Task DisposeAsync_WhenTheDisconnectWriteBlocks_CancelsItByDisablingTheTransmitter()
    {
        HdlcPeer peer = harness.CreatePeer(TimeSpan.FromMilliseconds(200));
        Task starting = peer.StartAndConnect("port", harness.Address, harness.RemoteAddress, harness.Options).AsTask();
        await harness.NextWritten(0);
        harness.Receive(harness.Peer(HdlcWireFrameKind.UnnumberedAcknowledge));
        await starting.WaitAsync(timeout);
        ManualResetEventSlim release = new();
        harness.Device.Setup(x => x.Write(It.IsAny<ReadOnlyMemory<byte>>())).Callback(() => release.Wait(timeout));
        harness.Device.Setup(x => x.DisableTransmitter()).Callback(release.Set);

        await peer.DisposeAsync().AsTask().WaitAsync(timeout);

        harness.Device.Verify(x => x.DisableTransmitter(), Times.Once);
        harness.Device.Verify(x => x.Dispose(), Times.Once);
        Assert.Equal(HdlcPeerState.Disconnected, peer.State);
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
        HdlcPeer peer = harness.CreatePeer(TimeSpan.FromMilliseconds(200));
        Task starting = peer.StartAndConnect("port", harness.Address, harness.RemoteAddress, harness.Options with { RetryInterval = null }).AsTask();
        await Task.Delay(100);

        await peer.DisposeAsync().AsTask().WaitAsync(timeout);
        stuck.Set();

        harness.Device.Verify(x => x.Dispose(), Times.Once);
        await Assert.ThrowsAnyAsync<Exception>(() => starting);
    }

    [Fact]
    public async Task Connect_WithTheSameAddressForBothStations_ThrowsAndStaysReady()
    {
        await using HdlcPeer peer = harness.CreatePeer();
        await peer.Start("port", harness.Options);

        await Assert.ThrowsAsync<ArgumentException>(async () => await peer.Connect(0x21, 0x21));

        Assert.Equal(HdlcPeerState.Ready, peer.State);
    }

    [Fact]
    public async Task Start_WithNonPositiveMaxRetransmissions_ThrowsAndStaysIdle()
    {
        await using HdlcPeer peer = harness.CreatePeer();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await peer.StartAndConnect("port", harness.Address, harness.RemoteAddress, new HdlcPeerOptions { MaxRetransmissions = 0 }));

        Assert.Equal(HdlcPeerState.Idle, peer.State);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    public async Task Start_WithTransmitWindowOutsideValidRange_ThrowsAndStaysIdle(int window)
    {
        await using HdlcPeer peer = harness.CreatePeer();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await peer.StartAndConnect("port", harness.Address, harness.RemoteAddress, new HdlcPeerOptions { TransmitWindow = window }));

        Assert.Equal(HdlcPeerState.Idle, peer.State);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4091)]
    public async Task Start_WithMaxInfoFieldOutsideValidRange_ThrowsAndStaysIdle(int maxInfoField)
    {
        await using HdlcPeer peer = harness.CreatePeer();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await peer.StartAndConnect("port", harness.Address, harness.RemoteAddress, new HdlcPeerOptions { MaxInfoField = maxInfoField }));

        Assert.Equal(HdlcPeerState.Idle, peer.State);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task Start_WithNonPositiveClockSpeed_ThrowsAndStaysIdle(int clockSpeed)
    {
        await using HdlcPeer peer = harness.CreatePeer();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await peer.StartAndConnect("port", harness.Address, harness.RemoteAddress, new HdlcPeerOptions { Link = new() { ClockSpeed = clockSpeed } }));

        Assert.Equal(HdlcPeerState.Idle, peer.State);
    }

    [Fact]
    public async Task Retransmit_WhenTheRemotePeerNeverAcknowledges_GivesUpAndDisconnects()
    {
        HdlcPeerOptions quick = harness.Options with { RetransmitInterval = TimeSpan.FromMilliseconds(40), MaxRetransmissions = 2 };
        HdlcPeer peer = harness.CreatePeer();
        TestObserver<HdlcPeerState> states = new();
        peer.StateChanged.Subscribe(states);
        Task starting = peer.StartAndConnect("port", harness.Address, harness.RemoteAddress, quick).AsTask();
        await harness.NextWritten(0);
        harness.Receive(harness.Peer(HdlcWireFrameKind.UnnumberedAcknowledge));
        await starting.WaitAsync(timeout);

        await peer.Send(new byte[] { 1 });
        await states.Completed.WaitAsync(timeout);

        Assert.Equal(HdlcPeerState.Disconnected, peer.State);
        Assert.Equal([HdlcPeerState.Ready, HdlcPeerState.Connecting, HdlcPeerState.Connected, HdlcPeerState.Disconnected], states.Seen);
        Assert.Equal(1 + 1 + 2, harness.Written.Count);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await peer.Send(new byte[] { 2 }));
        harness.Device.Verify(x => x.DisableReceiver(), Times.AtLeastOnce);
        await peer.DisposeAsync();
    }

    [Fact]
    public async Task Retransmit_WhenAcknowledgementsKeepArriving_NeverGivesUp()
    {
        HdlcPeerOptions quick = harness.Options with { RetransmitInterval = TimeSpan.FromMilliseconds(60), MaxRetransmissions = 1 };
        HdlcPeer peer = harness.CreatePeer();
        Task starting = peer.StartAndConnect("port", harness.Address, harness.RemoteAddress, quick).AsTask();
        await harness.NextWritten(0);
        harness.Receive(harness.Peer(HdlcWireFrameKind.UnnumberedAcknowledge));
        await starting.WaitAsync(timeout);

        for (int i = 0; i < 8; i++)
        {
            await peer.Send(new byte[] { (byte)i });
            await Task.Delay(50);
            harness.Receive(harness.Peer(HdlcWireFrameKind.ReceiveReady, false, receiveSequence: (i + 1) % 8));
        }

        Assert.True(peer.IsConnected);
        await peer.DisposeAsync();
    }

    [Fact]
    public async Task Retransmit_WhenResendingFails_DisconnectsInsteadOfStoppingSilently()
    {
        HdlcPeerOptions quick = harness.Options with { RetransmitInterval = TimeSpan.FromMilliseconds(40) };
        HdlcPeer peer = harness.CreatePeer();
        TestObserver<HdlcPeerState> states = new();
        peer.StateChanged.Subscribe(states);
        Task starting = peer.StartAndConnect("port", harness.Address, harness.RemoteAddress, quick).AsTask();
        await harness.NextWritten(0);
        harness.Receive(harness.Peer(HdlcWireFrameKind.UnnumberedAcknowledge));
        await starting.WaitAsync(timeout);
        await peer.Send(new byte[] { 1 });
        harness.Device.Setup(x => x.Write(It.IsAny<ReadOnlyMemory<byte>>())).Throws<IOException>();

        await states.Completed.WaitAsync(timeout);

        Assert.Equal(HdlcPeerState.Disconnected, peer.State);
        await peer.DisposeAsync();
    }

    [Fact]
    public async Task Send_WhenTheWriteFails_TakesTheFrameBackSoARetryIsNotDuplicated()
    {
        await using HdlcPeer peer = await harness.Connect();
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
        harness.Receive(harness.Peer(HdlcWireFrameKind.ReceiveReady, false, receiveSequence: 7));
        await eighth.WaitAsync(timeout);
    }

    [Fact]
    public async Task Receive_RepeatedRequestWhileConnected_ResetsTheLinkAndSendsUnacknowledgedDataAgain()
    {
        await using HdlcPeer peer = await harness.Connect();
        await peer.Send(new byte[] { 1 });
        await peer.Send(new byte[] { 2 });
        await harness.NextWritten(2);

        harness.Receive(harness.Peer(HdlcWireFrameKind.SetAsynchronousBalancedMode));

        Assert.Equal(HdlcWireFrameKind.UnnumberedAcknowledge, (await harness.NextWritten(3)).Kind);
        HdlcWireFrame first = await harness.NextWritten(4);
        HdlcWireFrame second = await harness.NextWritten(5);
        Assert.Equal(0, first.SendSequence);
        Assert.Equal(new byte[] { 1 }, first.Payload.ToArray());
        Assert.Equal(1, second.SendSequence);
        Assert.Equal(new byte[] { 2 }, second.Payload.ToArray());
        Assert.True(peer.IsConnected);
    }

    [Fact]
    public async Task Send_AfterTheRemotePeerDisconnected_Throws()
    {
        HdlcPeer peer = await harness.Connect();
        TestObserver<HdlcPeerState> states = new();
        peer.StateChanged.Subscribe(states);
        harness.Receive(harness.Peer(HdlcWireFrameKind.Disconnect));
        await states.Completed.WaitAsync(timeout);

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await peer.Send(new byte[] { 1 }));
        await peer.DisposeAsync();
    }

    [Fact]
    public async Task Dispose_FromObserverWhenRetransmissionGivesUp_ReturnsWithoutWaitingForTheShutdownTimeout()
    {
        HdlcPeerOptions quick = harness.Options with { RetransmitInterval = TimeSpan.FromMilliseconds(40), MaxRetransmissions = 1 };
        HdlcPeer peer = harness.CreatePeer(TimeSpan.FromSeconds(30));
        Task starting = peer.StartAndConnect("port", harness.Address, harness.RemoteAddress, quick).AsTask();
        await harness.NextWritten(0);
        harness.Receive(harness.Peer(HdlcWireFrameKind.UnnumberedAcknowledge));
        await starting.WaitAsync(timeout);
        TaskCompletionSource<TimeSpan> disposeTook = new();
        peer.StateChanged.Subscribe(new CallbackObserver<HdlcPeerState>(state =>
        {
            if (state == HdlcPeerState.Disconnected)
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
        HdlcPeer peer = harness.CreatePeer();
        Task starting = peer.StartAndConnect("port", harness.Address, harness.RemoteAddress, harness.Options).AsTask();
        await harness.NextWritten(0);

        await peer.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => starting);
        Assert.Equal(HdlcPeerState.Disconnected, peer.State);
        harness.Device.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    public async Task Start_WithSubMillisecondRetransmitInterval_StaysConnected()
    {
        HdlcPeer peer = harness.CreatePeer();
        Task starting = peer.StartAndConnect("port", harness.Address, harness.RemoteAddress, harness.Options with { RetransmitInterval = TimeSpan.FromTicks(5000) }).AsTask();
        await harness.NextWritten(0);
        harness.Receive(harness.Peer(HdlcWireFrameKind.UnnumberedAcknowledge));
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
        await using HdlcPeer peer = harness.CreatePeer();
        TimeSpan tooLong = TimeSpan.FromMilliseconds(int.MaxValue) + TimeSpan.FromMilliseconds(1);
        HdlcPeerOptions options = retry ? new() { RetryInterval = tooLong } : new() { RetransmitInterval = tooLong };

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await peer.StartAndConnect("port", harness.Address, harness.RemoteAddress, options));

        Assert.Equal(HdlcPeerState.Idle, peer.State);
    }

    [Fact]
    public async Task Receiver_BlockingOnASendThatNeedsAnAcknowledgement_DoesNotStallReceiving()
    {
        await using HdlcPeer peer = await harness.Connect();
        for (int i = 0; i < 7; i++)
        {
            await peer.Send(new byte[] { (byte)i });
        }

        TaskCompletionSource sent = new();
        peer.Receiver = _ =>
        {
            peer.Send(new byte[] { 99 }).AsTask().Wait();
            sent.SetResult();
        };
        harness.Receive(harness.Peer(HdlcWireFrameKind.Information, false, 0, new byte[] { 1 }));
        await Task.Delay(100);
        Assert.False(sent.Task.IsCompleted);

        harness.Receive(harness.Peer(HdlcWireFrameKind.ReceiveReady, false, receiveSequence: 7));

        await sent.Task.WaitAsync(timeout);
        Assert.Contains(harness.Written, frame => HdlcWireFrame.Parse(frame).Kind == HdlcWireFrameKind.Information && HdlcWireFrame.Parse(frame).Payload.ToArray().SequenceEqual(new byte[] { 99 }));
    }

    [Fact]
    public async Task Receiver_CallsAreNeverOverlappedAndKeepTheirOrder()
    {
        await using HdlcPeer peer = await harness.Connect();
        int inside = 0;
        bool overlapped = false;
        List<byte> order = [];
        peer.Receiver = data =>
        {
            if (Interlocked.Increment(ref inside) > 1)
            {
                overlapped = true;
            }

            Thread.Sleep(10);
            lock (order)
            {
                order.Add(data.Memory.Span[0]);
            }

            Interlocked.Decrement(ref inside);
            data.Dispose();
        };

        for (int i = 0; i < 10; i++)
        {
            harness.Receive(harness.Peer(HdlcWireFrameKind.Information, false, i % 8, new byte[] { (byte)i }));
        }

        await Eventually(() =>
        {
            lock (order)
            {
                return order.Count == 10;
            }
        });

        Assert.False(overlapped);
        Assert.Equal(Enumerable.Range(0, 10).Select(i => (byte)i), order);
    }

    [Fact]
    public async Task Receiver_AfterDisposal_IsNotCalledAgain()
    {
        HdlcPeer peer = await harness.Connect();
        TaskCompletionSource release = new();
        int calls = 0;
        peer.Receiver = _ =>
        {
            Interlocked.Increment(ref calls);
            release.Task.Wait();
        };
        harness.Receive(harness.Peer(HdlcWireFrameKind.Information, false, 0, new byte[] { 1 }));
        harness.Receive(harness.Peer(HdlcWireFrameKind.Information, false, 1, new byte[] { 2 }));
        await Eventually(() => Volatile.Read(ref calls) == 1);

        Task disposing = peer.DisposeAsync().AsTask();
        release.SetResult();
        await disposing.WaitAsync(timeout);
        await Task.Delay(100);

        Assert.Equal(1, calls);
    }
}
