namespace BlueHeighliner.MicroGate;

public sealed class LoopbackPeerTests : IAsyncLifetime
{
    private readonly MicroGatePeerOptions requesting = new() { RetryInterval = TimeSpan.FromMilliseconds(200) };
    private readonly MicroGatePeerOptions passive = new() { RetryInterval = null };
    private readonly TimeSpan timeout = TimeSpan.FromSeconds(10);
    private MicroGatePeer first = null!;
    private MicroGatePeer second = null!;

    public async Task InitializeAsync()
    {
        (SocketMicroGateDevice firstDevice, SocketMicroGateDevice secondDevice) = await SocketMicroGateDevice.CreatePair();
        first = Create(firstDevice);
        second = Create(secondDevice);
    }

    public async Task DisposeAsync()
    {
        await first.DisposeAsync();
        await second.DisposeAsync();
    }

    [Fact]
    public async Task Start_OneSideSendsRequests_BothSidesConnect()
    {
        await ConnectBoth();

        Assert.True(first.IsConnected);
        Assert.True(second.IsConnected);
    }

    [Fact]
    public async Task Start_BothSidesSendRequests_BothSidesConnect()
    {
        (SocketMicroGateDevice firstDevice, SocketMicroGateDevice secondDevice) = await SocketMicroGateDevice.CreatePair();
        await using MicroGatePeer left = Create(firstDevice);
        await using MicroGatePeer right = Create(secondDevice);
        MicroGatePeerOptions fast = requesting with { RetryInterval = TimeSpan.FromMilliseconds(20) };

        await Task.WhenAll(left.Start("left", 0x01, 0x03, fast).AsTask(), right.Start("right", 0x03, 0x01, fast).AsTask()).WaitAsync(timeout);

        Assert.True(left.IsConnected);
        Assert.True(right.IsConnected);
    }

    [Fact]
    public async Task Start_WhenRemoteStartsLate_Connects()
    {
        Task starting = first.Start("first", 0x01, 0x03, requesting).AsTask();
        await Task.Delay(500);

        await second.Start("second", 0x03, 0x01, passive).AsTask().WaitAsync(timeout);
        await starting.WaitAsync(timeout);

        Assert.True(first.IsConnected);
        Assert.True(second.IsConnected);
    }

    [Fact]
    public async Task Send_ManyMessagesBothWays_ArrivesInOrder()
    {
        PayloadObserver atSecond = new();
        PayloadObserver atFirst = new();
        second.Receiver = atSecond.Receive;
        first.Receiver = atFirst.Receive;
        await ConnectBoth();

        for (int i = 0; i < 40; i++)
        {
            await first.Send(new byte[] { (byte)i, 1 });
            await second.Send(new byte[] { (byte)i, 2 });
        }

        Assert.Equal(Enumerable.Range(0, 40).Select(i => new byte[] { (byte)i, 1 }), await atSecond.Next(40), new ByteArrayComparer());
        Assert.Equal(Enumerable.Range(0, 40).Select(i => new byte[] { (byte)i, 2 }), await atFirst.Next(40), new ByteArrayComparer());
    }

    [Fact]
    public async Task Send_LargePayload_ArrivesIntact()
    {
        (SocketMicroGateDevice firstSocket, SocketMicroGateDevice secondSocket) = await SocketMicroGateDevice.CreatePair();
        await using MicroGatePeer large = Create(firstSocket);
        await using MicroGatePeer largeSecond = Create(secondSocket);
        MicroGatePeerOptions withMaxInfoField = requesting with { MaxInfoField = 4090 };
        PayloadObserver atSecond = new();
        largeSecond.Receiver = atSecond.Receive;
        Task listening = largeSecond.Start("second", 0x03, 0x01, passive with { MaxInfoField = 4090 }).AsTask();
        await large.Start("first", 0x01, 0x03, withMaxInfoField).AsTask().WaitAsync(timeout);
        await listening.WaitAsync(timeout);
        byte[] payload = new byte[4090];
        new Random(1).NextBytes(payload);

        await large.Send(payload);

        Assert.Equal(payload, await atSecond.Next());
    }

    [Fact]
    public async Task Send_PooledOwner_ArrivesAndIsDisposed()
    {
        PayloadObserver atSecond = new();
        second.Receiver = atSecond.Receive;
        await ConnectBoth();
        Mock<IMemoryOwner<byte>> owner = new();
        owner.SetupGet(x => x.Memory).Returns(new byte[] { 7, 8, 9 });

        await first.Send(owner.Object);

        Assert.Equal(new byte[] { 7, 8, 9 }, await atSecond.Next());
        owner.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    public async Task Dispose_RemotePeerDisconnectsAndCompletesTheStateStream()
    {
        TestObserver<MicroGatePeerState> states = new();
        second.StateChanged.Subscribe(states);
        await ConnectBoth();

        await first.DisposeAsync();

        await states.Completed.WaitAsync(timeout);
        Assert.Equal([MicroGatePeerState.Connecting, MicroGatePeerState.Connected, MicroGatePeerState.Disconnected], states.Seen);
    }

    [Fact]
    public async Task Send_AfterDispose_Throws()
    {
        await ConnectBoth();
        await first.DisposeAsync();

        await Assert.ThrowsAnyAsync<Exception>(async () => await first.Send(new byte[] { 1 }));
    }

    private MicroGatePeer Create(IMicroGateDevice device)
    {
        Mock<IMicroGateDeviceOpener> opener = new();
        opener.Setup(x => x.Open(It.IsAny<string>(), It.IsAny<MicroGatePeerOptions>())).Returns(device);
        return new MicroGatePeer(opener.Object, opener.Object);
    }

    private async Task ConnectBoth()
    {
        Task listening = second.Start("second", 0x03, 0x01, passive).AsTask();
        await first.Start("first", 0x01, 0x03, requesting).AsTask().WaitAsync(timeout);
        await listening.WaitAsync(timeout);
    }

    [Fact]
    public async Task Send_WhenInformationFramesAreLostOnTheLine_StillArrivesCompleteAndInOrder()
    {
        (SocketMicroGateDevice firstSocket, SocketMicroGateDevice secondSocket) = await SocketMicroGateDevice.CreatePair();
        await using MicroGatePeer lossy = Create(new DroppingMicroGateDevice(firstSocket, (_, index) => index is 3 or 4 or 10 or 25));
        await using MicroGatePeer healthy = Create(secondSocket);
        MicroGatePeerOptions quick = requesting with { RetransmitInterval = TimeSpan.FromMilliseconds(100) };
        PayloadObserver atHealthy = new();
        healthy.Receiver = atHealthy.Receive;
        Task listening = healthy.Start("healthy", 0x03, 0x01, passive with { RetransmitInterval = TimeSpan.FromMilliseconds(100) }).AsTask();
        await lossy.Start("lossy", 0x01, 0x03, quick).AsTask().WaitAsync(timeout);
        await listening.WaitAsync(timeout);

        for (int i = 0; i < 40; i++)
        {
            await lossy.Send(new byte[] { (byte)i }).AsTask().WaitAsync(timeout);
        }

        Assert.Equal(Enumerable.Range(0, 40).Select(i => new byte[] { (byte)i }), await atHealthy.Next(40), new ByteArrayComparer());
    }

    [Fact]
    public async Task Send_WhenTheLastFrameIsLost_IsRecoveredByTheRetransmitTimer()
    {
        (SocketMicroGateDevice firstSocket, SocketMicroGateDevice secondSocket) = await SocketMicroGateDevice.CreatePair();
        await using MicroGatePeer lossy = Create(new DroppingMicroGateDevice(firstSocket, (_, index) => index == 1));
        await using MicroGatePeer healthy = Create(secondSocket);
        PayloadObserver atHealthy = new();
        healthy.Receiver = atHealthy.Receive;
        Task listening = healthy.Start("healthy", 0x03, 0x01, passive).AsTask();
        await lossy.Start("lossy", 0x01, 0x03, requesting with { RetransmitInterval = TimeSpan.FromMilliseconds(100) }).AsTask().WaitAsync(timeout);
        await listening.WaitAsync(timeout);

        await lossy.Send(new byte[] { 42 });

        Assert.Equal(new byte[] { 42 }, await atHealthy.Next());
    }

    [Fact]
    public async Task Send_MoreThanTheWindowOverASlowReceiver_DeliversEverythingInOrder()
    {
        PayloadObserver atSecond = new();
        second.Receiver = atSecond.Receive;
        await ConnectBoth();

        Task[] sends = [.. Enumerable.Range(0, 100).Select(i => first.Send(new byte[] { (byte)i }).AsTask())];
        await Task.WhenAll(sends).WaitAsync(timeout);

        List<byte[]> arrived = await atSecond.Next(100);
        Assert.Equal(100, arrived.Count);
        Assert.Equal(Enumerable.Range(0, 100).Select(i => (byte)i), arrived.Select(item => item[0]).Order());
    }

    private sealed class ByteArrayComparer : IEqualityComparer<byte[]>
    {
        public bool Equals(byte[]? x, byte[]? y) => x is not null && y is not null && x.AsSpan().SequenceEqual(y);

        public int GetHashCode(byte[] obj) => obj.Length;
    }
}
