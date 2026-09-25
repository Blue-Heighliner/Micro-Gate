namespace BlueHeighliner.MicroGate;

public sealed class LoopbackConnectionTests : IAsyncLifetime
{
    private readonly MicroGateConnectionOptions options = new() { Address = 0x21 };
    private readonly TimeSpan timeout = TimeSpan.FromSeconds(10);
    private MicroGateDeviceConnection first = null!;
    private MicroGateDeviceConnection second = null!;

    public async Task InitializeAsync()
    {
        (SocketMicroGateDevice firstDevice, SocketMicroGateDevice secondDevice) = await SocketMicroGateDevice.CreatePair();
        first = new MicroGateDeviceConnection(firstDevice, new HdlcStateMachine(options));
        second = new MicroGateDeviceConnection(secondDevice, new HdlcStateMachine(options));
    }

    public async Task DisposeAsync()
    {
        await first.DisposeAsync();
        await second.DisposeAsync();
    }

    [Fact]
    public async Task Establish_OneSideInitiates_BothSidesConnect()
    {
        await first.Establish(CancellationToken.None).WaitAsync(timeout);

        Assert.True(first.IsConnected);
        await Eventually(() => second.IsConnected);
    }

    [Fact]
    public async Task Establish_BothSidesInitiateSimultaneously_BothSidesConnect()
    {
        await Task.WhenAll(first.Establish(CancellationToken.None), second.Establish(CancellationToken.None)).WaitAsync(timeout);

        await Eventually(() => first.IsConnected && second.IsConnected);
    }

    [Fact]
    public async Task Send_ManyMessagesBothWays_ArrivesInOrder()
    {
        await first.Establish(CancellationToken.None).WaitAsync(timeout);
        Collector atSecond = new(second);
        Collector atFirst = new(first);

        for (int i = 0; i < 40; i++)
        {
            await first.Send(new byte[] { (byte)i, 1 });
            await second.Send(new byte[] { (byte)i, 2 });
        }

        Assert.Equal(Enumerable.Range(0, 40).Select(i => new byte[] { (byte)i, 1 }), await atSecond.Wait(40), new ByteArrayComparer());
        Assert.Equal(Enumerable.Range(0, 40).Select(i => new byte[] { (byte)i, 2 }), await atFirst.Wait(40), new ByteArrayComparer());
    }

    [Fact]
    public async Task Send_LargePayload_ArrivesIntact()
    {
        await first.Establish(CancellationToken.None).WaitAsync(timeout);
        Collector atSecond = new(second);
        byte[] payload = new byte[60000];
        new Random(1).NextBytes(payload);

        await first.Send(payload);

        Assert.Equal(payload, (await atSecond.Wait(1))[0]);
    }

    [Fact]
    public async Task Send_PooledOwner_ArrivesAndIsDisposed()
    {
        await first.Establish(CancellationToken.None).WaitAsync(timeout);
        Collector atSecond = new(second);
        Mock<IMemoryOwner<byte>> owner = new();
        owner.SetupGet(x => x.Memory).Returns(new byte[] { 7, 8, 9 });

        await first.Send(owner.Object);

        Assert.Equal(new byte[] { 7, 8, 9 }, (await atSecond.Wait(1))[0]);
        owner.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    public async Task Dispose_PeerRaisesDisconnected()
    {
        await first.Establish(CancellationToken.None).WaitAsync(timeout);
        await Eventually(() => second.IsConnected);
        TaskCompletionSource disconnected = new();
        second.Disconnected += (_, _) => disconnected.TrySetResult();

        await first.DisposeAsync();

        await disconnected.Task.WaitAsync(timeout);
        Assert.False(second.IsConnected);
    }

    [Fact]
    public async Task Send_AfterDispose_Throws()
    {
        await first.Establish(CancellationToken.None).WaitAsync(timeout);
        await first.DisposeAsync();

        await Assert.ThrowsAnyAsync<Exception>(async () => await first.Send(new byte[] { 1 }));
    }

    private async Task Eventually(Func<bool> condition)
    {
        using CancellationTokenSource cancellation = new(timeout);
        while (!condition())
        {
            await Task.Delay(10, cancellation.Token);
        }
    }

    private sealed class Collector
    {
        public Collector(IMicroGateConnection connection) =>
            connection.Received += (_, data) =>
            {
                using (data)
                {
                    messages.Add(data.Memory.ToArray());
                }
            };

        private readonly BlockingCollection<byte[]> messages = [];

        public async Task<List<byte[]>> Wait(int count) =>
            await Task.Run(() =>
            {
                List<byte[]> result = [];
                using CancellationTokenSource cancellation = new(TimeSpan.FromSeconds(10));
                while (result.Count < count)
                {
                    result.Add(messages.Take(cancellation.Token));
                }

                return result;
            });
    }

    private sealed class ByteArrayComparer : IEqualityComparer<byte[]>
    {
        public bool Equals(byte[]? x, byte[]? y) => x is not null && y is not null && x.AsSpan().SequenceEqual(y);

        public int GetHashCode(byte[] obj) => obj.Length;
    }
}
