namespace BlueHeighliner.MicroGate;

public sealed class UartPeerTests : IDisposable
{
    private readonly UartDeviceHarness harness = new();
    private readonly TimeSpan timeout = TimeSpan.FromSeconds(5);

    public void Dispose() => harness.Dispose();

    [Fact]
    public async Task Create_DoesNotOpenTheDeviceUntilStarted()
    {
        await using UartPeer peer = harness.CreatePeer();

        Assert.Equal(UartPeerState.Idle, peer.State);
        harness.Opener.Verify(x => x.Open(It.IsAny<string>(), It.IsAny<UartPeerOptions>()), Times.Never);
    }

    [Fact]
    public async Task Start_OpensTheDeviceWithTheOptionsAndReportsTheStateToAnEarlyObserver()
    {
        UartPeer peer = harness.CreatePeer();
        TestObserver<UartPeerState> states = new();
        peer.StateChanged.Subscribe(states);
        UartPeerOptions options = new() { BaudRate = 19200, DataBits = 7, StopBits = UartStopBits.Two, Parity = UartParity.Even, Loopback = true };

        await peer.Start("port", options);

        Assert.Equal([UartPeerState.Open], states.Seen);
        Assert.Equal(UartPeerState.Open, peer.State);
        harness.Opener.Verify(x => x.Open("port", options), Times.Once);
        harness.Device.Verify(x => x.EnableTransmitter(), Times.Never);
        await peer.DisposeAsync();
    }

    [Fact]
    public async Task Start_WithoutOptions_UsesTheDefaults()
    {
        UartPeerOptions? captured = null;
        harness.Opener.Setup(x => x.Open("port", It.IsAny<UartPeerOptions>())).Callback<string, UartPeerOptions>((_, passed) => captured = passed).Returns(harness.Device.Object);
        await using UartPeer peer = harness.CreatePeer();

        await peer.Start("port");

        Assert.Equal(new UartPeerOptions(), captured);
        Assert.Equal(9600, captured!.BaudRate);
        Assert.Equal(8, captured.DataBits);
        Assert.Equal(UartStopBits.One, captured.StopBits);
        Assert.Equal(UartParity.None, captured.Parity);
        Assert.False(captured.Loopback);
    }

    [Theory]
    [InlineData(0, 8, 0, 0)]
    [InlineData(-9600, 8, 0, 0)]
    [InlineData(9600, 4, 0, 0)]
    [InlineData(9600, 9, 0, 0)]
    [InlineData(9600, 8, 7, 0)]
    [InlineData(9600, 8, 0, 7)]
    public async Task Start_WithAnInvalidSetting_ThrowsAndStaysIdle(int baudRate, int dataBits, int stopBits, int parity)
    {
        await using UartPeer peer = harness.CreatePeer();
        UartPeerOptions options = new() { BaudRate = baudRate, DataBits = dataBits, StopBits = (UartStopBits)stopBits, Parity = (UartParity)parity };

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await peer.Start("port", options));

        Assert.Equal(UartPeerState.Idle, peer.State);
        harness.Opener.Verify(x => x.Open(It.IsAny<string>(), It.IsAny<UartPeerOptions>()), Times.Never);
    }

    [Fact]
    public async Task Start_Twice_ThrowsInvalidOperation()
    {
        await using UartPeer peer = harness.CreatePeer();
        await peer.Start("port");

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await peer.Start("port"));
    }

    [Fact]
    public async Task Start_AfterDispose_ThrowsInvalidOperation()
    {
        UartPeer peer = harness.CreatePeer();
        await peer.DisposeAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await peer.Start("port"));
    }

    [Fact]
    public async Task Start_WhenTheDeviceCannotBeOpened_ThrowsAndClosesThePeer()
    {
        harness.Opener.Setup(x => x.Open("port", It.IsAny<UartPeerOptions>())).Throws(new IOException("no device"));
        UartPeer peer = harness.CreatePeer();
        TestObserver<UartPeerState> states = new();
        peer.StateChanged.Subscribe(states);

        IOException exception = await Assert.ThrowsAsync<IOException>(async () => await peer.Start("port"));

        Assert.Equal("no device", exception.Message);
        Assert.Equal(UartPeerState.Closed, peer.State);
        await states.Completed.WaitAsync(timeout);
        Assert.Equal([UartPeerState.Closed], states.Seen);
    }

    [Fact]
    public async Task Start_WithACanceledToken_ThrowsAndClosesThePeer()
    {
        UartPeer peer = harness.CreatePeer();
        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await peer.Start("port", null, cancellation.Token));

        Assert.Equal(UartPeerState.Closed, peer.State);
        harness.Device.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    public async Task Received_BytesAreDeliveredInOrderAsTheyArrive()
    {
        await using UartPeer peer = harness.CreatePeer();
        PayloadObserver received = new();
        peer.Receiver = received.Receive;
        await peer.Start("port");

        harness.Receive(1, 2, 3);
        harness.Receive(4);
        harness.Receive(5, 6);

        Assert.Equal([new byte[] { 1, 2, 3 }, [4], [5, 6]], await received.Next(3));
    }

    [Fact]
    public async Task Received_WithNoReceiver_IsDiscardedAndLaterBytesAreDeliveredOnceSet()
    {
        await using UartPeer peer = harness.CreatePeer();
        await peer.Start("port");
        harness.Receive(1);
        await Task.Delay(100);
        PayloadObserver received = new();

        peer.Receiver = received.Receive;
        harness.Receive(2);

        Assert.Equal(new byte[] { 2 }, await received.Next());
    }

    [Fact]
    public async Task Received_WhenTheReceiverThrows_ReportsItAndDeliversTheNextChunk()
    {
        await using UartPeer peer = harness.CreatePeer();
        TestObserver<Exception> errors = new();
        peer.Exceptions.Subscribe(errors);
        PayloadObserver received = new();
        int calls = 0;
        peer.Receiver = owner =>
        {
            if (Interlocked.Increment(ref calls) == 1)
            {
                owner.Dispose();
                throw new InvalidOperationException("receiver failed");
            }

            received.Receive(owner);
        };
        await peer.Start("port");

        harness.Receive(1);
        harness.Receive(2);

        Assert.Equal("receiver failed", (await errors.Next()).Message);
        Assert.Equal(new byte[] { 2 }, await received.Next());
    }

    [Fact]
    public async Task Received_WhenAStateObserverThrows_ReportsItOnExceptions()
    {
        UartPeer peer = harness.CreatePeer();
        TestObserver<Exception> errors = new();
        peer.Exceptions.Subscribe(errors);
        peer.StateChanged.Subscribe(new CallbackObserver<UartPeerState>(_ => throw new InvalidOperationException("observer failed")));

        await peer.Start("port");

        Assert.Equal("observer failed", (await errors.Next()).Message);
        await peer.DisposeAsync();
    }

    [Fact]
    public async Task Send_EnablesTheTransmitterOnceAndWritesTheBytes()
    {
        await using UartPeer peer = harness.CreatePeer();
        await peer.Start("port");

        await peer.Send(new byte[] { 1, 2, 3 });
        await peer.Send(new byte[] { 4 });

        Assert.Equal([new byte[] { 1, 2, 3 }, [4]], harness.Written);
        harness.Device.Verify(x => x.EnableTransmitter(), Times.Once);
    }

    [Fact]
    public async Task Send_SplitsLargeDataIntoPiecesOfAtMost4096Bytes()
    {
        await using UartPeer peer = harness.CreatePeer();
        await peer.Start("port");
        byte[] data = [.. Enumerable.Range(0, 10000).Select(i => (byte)i)];

        await peer.Send(data);

        Assert.Equal([4096, 4096, 1808], harness.Written.Select(piece => piece.Length));
        Assert.Equal(data, harness.Written.SelectMany(piece => piece));
    }

    [Fact]
    public async Task Send_EmptyData_WritesNothing()
    {
        await using UartPeer peer = harness.CreatePeer();
        await peer.Start("port");

        await peer.Send(ReadOnlyMemory<byte>.Empty);

        Assert.Empty(harness.Written);
    }

    [Fact]
    public async Task Send_ConcurrentSends_AreNeverInterleaved()
    {
        await using UartPeer peer = harness.CreatePeer();
        await peer.Start("port");
        byte[] first = new byte[9000];
        byte[] second = new byte[9000];
        Array.Fill(first, (byte)1);
        Array.Fill(second, (byte)2);

        await Task.WhenAll(peer.Send(first).AsTask(), peer.Send(second).AsTask());

        List<int> markers = [.. harness.Written.Select(piece => (int)piece[0])];
        Assert.True(markers.SequenceEqual([1, 1, 1, 2, 2, 2]) || markers.SequenceEqual([2, 2, 2, 1, 1, 1]), string.Join(",", markers));
    }

    [Fact]
    public async Task Send_BeforeStartOrAfterDispose_ThrowsInvalidOperation()
    {
        UartPeer peer = harness.CreatePeer();
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await peer.Send(new byte[] { 1 }));

        await peer.Start("port");
        await peer.DisposeAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await peer.Send(new byte[] { 1 }));
    }

    [Fact]
    public async Task Send_WhenTheWriteFails_ThrowsIoExceptionAndThePeerStaysOpen()
    {
        await using UartPeer peer = harness.CreatePeer();
        await peer.Start("port");
        harness.Device.Setup(x => x.Write(It.IsAny<ReadOnlyMemory<byte>>())).Throws(new IOException("write failed"));

        await Assert.ThrowsAsync<IOException>(async () => await peer.Send(new byte[] { 1 }));

        Assert.Equal(UartPeerState.Open, peer.State);
    }

    [Fact]
    public async Task Send_WithACanceledToken_StopsBeforeTheNextPiece()
    {
        await using UartPeer peer = harness.CreatePeer();
        await peer.Start("port");
        using CancellationTokenSource cancellation = new();
        harness.Device.Setup(x => x.Write(It.IsAny<ReadOnlyMemory<byte>>())).Callback(cancellation.Cancel);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await peer.Send(new byte[10000], cancellation.Token));

        harness.Device.Verify(x => x.Write(It.IsAny<ReadOnlyMemory<byte>>()), Times.Once);
    }

    [Fact]
    public async Task StateChanged_WhenTheDeviceEnds_ClosesThePeerAndCompletesTheStreamsAfterDeliveringWhatArrived()
    {
        await using UartPeer peer = harness.CreatePeer();
        TestObserver<UartPeerState> states = new();
        TestObserver<Exception> errors = new();
        PayloadObserver received = new();
        peer.StateChanged.Subscribe(states);
        peer.Exceptions.Subscribe(errors);
        peer.Receiver = received.Receive;
        await peer.Start("port");
        harness.Receive(7, 8);

        harness.EndOfInput();

        await states.Completed.WaitAsync(timeout);
        await errors.Completed.WaitAsync(timeout);
        Assert.Equal([UartPeerState.Open, UartPeerState.Closed], states.Seen);
        Assert.Equal(new byte[] { 7, 8 }, await received.Next());
    }

    [Fact]
    public async Task Dispose_DisablesTheReceiverClosesTheDeviceAndIsIdempotent()
    {
        UartPeer peer = harness.CreatePeer();
        await peer.Start("port");

        await peer.DisposeAsync();
        await peer.DisposeAsync();
        peer.Dispose();

        Assert.Equal(UartPeerState.Closed, peer.State);
        harness.Device.Verify(x => x.DisableReceiver(), Times.Once);
        harness.Device.Verify(x => x.Dispose(), Times.Once);
        harness.Device.Verify(x => x.DisableTransmitter(), Times.Never);
    }

    [Fact]
    public async Task Dispose_AfterSending_AlsoDisablesTheTransmitter()
    {
        UartPeer peer = harness.CreatePeer();
        await peer.Start("port");
        await peer.Send(new byte[] { 1 });

        await peer.DisposeAsync();

        harness.Device.Verify(x => x.DisableTransmitter(), Times.Once);
    }

    [Fact]
    public async Task Dispose_BeforeStart_IsSafe()
    {
        UartPeer peer = harness.CreatePeer();
        TestObserver<UartPeerState> states = new();
        peer.StateChanged.Subscribe(states);

        await peer.DisposeAsync();

        await states.Completed.WaitAsync(timeout);
        Assert.Equal([UartPeerState.Closed], states.Seen);
    }

    [Fact]
    public async Task Dispose_FromTheReceiver_DoesNotDeadlockAndClosesTheDevice()
    {
        UartPeer peer = harness.CreatePeer();
        TaskCompletionSource disposed = new();
        peer.Receiver = owner =>
        {
            owner.Dispose();
            peer.Dispose();
            disposed.TrySetResult();
        };
        await peer.Start("port");

        harness.Receive(1);

        await disposed.Task.WaitAsync(timeout);
        for (int attempt = 0; attempt < 100 && !harness.Device.Invocations.Any(invocation => invocation.Method.Name == nameof(IDisposable.Dispose)); attempt++)
        {
            await Task.Delay(50);
        }

        harness.Device.Verify(x => x.Dispose(), Times.Once);
        Assert.Equal(UartPeerState.Closed, peer.State);
    }

    [Fact]
    public void Factory_CreatesIdlePeers()
    {
        IUartPeer first = new UartPeerFactory().Create();
        IUartPeer second = new UartPeerFactory().Create();

        Assert.NotSame(first, second);
        Assert.Equal(UartPeerState.Idle, first.State);
        Assert.Equal(UartPeerState.Idle, second.State);
    }
}
