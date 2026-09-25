namespace BlueHeighliner.MicroGate;

public sealed class MicroGateDeviceConnectionTests : IDisposable
{
    private readonly DeviceHarness harness = new();
    private readonly TimeSpan timeout = TimeSpan.FromSeconds(5);

    public void Dispose() => harness.Dispose();

    [Fact]
    public async Task Establish_SendsSabmAndCompletesOnUa()
    {
        await using MicroGateDeviceConnection connection = await harness.Connect();

        HdlcFrame sabm = await harness.NextWritten(0);
        Assert.Equal(HdlcFrameKind.SetAsynchronousBalancedMode, sabm.Kind);
        Assert.True(connection.IsConnected);
    }

    [Fact]
    public async Task Establish_WhenCanceled_ThrowsAndDisposesDevice()
    {
        using CancellationTokenSource cancellation = new();
        await using MicroGateDeviceConnection connection = new(harness.Device.Object, new HdlcStateMachine(harness.Options));
        Task establish = connection.Establish(cancellation.Token);
        await harness.NextWritten(0);

        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => establish);
        Assert.False(connection.IsConnected);
    }

    [Fact]
    public async Task Establish_WhenWriteFails_Throws()
    {
        harness.Device.Setup(x => x.Write(It.IsAny<ReadOnlyMemory<byte>>())).Throws<IOException>();
        await using MicroGateDeviceConnection connection = new(harness.Device.Object, new HdlcStateMachine(harness.Options));

        await Assert.ThrowsAsync<IOException>(() => connection.Establish(CancellationToken.None));
    }

    [Fact]
    public async Task Receive_InformationFrame_RaisesReceivedAndAcknowledges()
    {
        await using MicroGateDeviceConnection connection = await harness.Connect();
        TaskCompletionSource<byte[]> received = new();
        connection.Received += (_, data) =>
        {
            using (data)
            {
                received.SetResult(data.Memory.ToArray());
            }
        };

        harness.Receive(harness.Peer(HdlcFrameKind.Information, false, 0, new byte[] { 1, 2, 3 }));

        Assert.Equal(new byte[] { 1, 2, 3 }, await received.Task.WaitAsync(timeout));
        HdlcFrame acknowledgement = await harness.NextWritten(1);
        Assert.Equal(HdlcFrameKind.ReceiveReady, acknowledgement.Kind);
        Assert.Equal(1, acknowledgement.ReceiveSequence);
    }

    [Fact]
    public async Task Receive_MalformedFrame_IsDroppedAndLoopContinues()
    {
        await using MicroGateDeviceConnection connection = await harness.Connect();
        TaskCompletionSource<byte[]> received = new();
        connection.Received += (_, data) =>
        {
            using (data)
            {
                received.SetResult(data.Memory.ToArray());
            }
        };

        harness.Receive([0xFF]);
        harness.Receive(harness.Peer(HdlcFrameKind.Information, false, 0, new byte[] { 9 }));

        Assert.Equal(new byte[] { 9 }, await received.Task.WaitAsync(timeout));
    }

    [Fact]
    public async Task Receive_InformationFrameWithNoSubscriber_StillAcknowledges()
    {
        await using MicroGateDeviceConnection connection = await harness.Connect();

        harness.Receive(harness.Peer(HdlcFrameKind.Information, false, 0, new byte[] { 1 }));

        Assert.Equal(HdlcFrameKind.ReceiveReady, (await harness.NextWritten(1)).Kind);
    }

    [Fact]
    public async Task Send_Memory_WritesInformationFrameWithIncreasingSequence()
    {
        await using MicroGateDeviceConnection connection = await harness.Connect();

        await connection.Send(new byte[] { 5, 6 });
        await connection.Send(new byte[] { 7 });

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
        await using MicroGateDeviceConnection connection = await harness.Connect();
        Mock<IMemoryOwner<byte>> owner = new();
        owner.SetupGet(x => x.Memory).Returns(new byte[] { 4, 4 });

        await connection.Send(owner.Object);

        Assert.Equal(new byte[] { 4, 4 }, (await harness.NextWritten(1)).Payload.ToArray());
        owner.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    public async Task Send_Owner_WhenNotConnected_DisposesOwnerAndThrows()
    {
        await using MicroGateDeviceConnection connection = new(harness.Device.Object, new HdlcStateMachine(harness.Options));
        Mock<IMemoryOwner<byte>> owner = new();
        owner.SetupGet(x => x.Memory).Returns(new byte[] { 1 });

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await connection.Send(owner.Object));

        owner.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    public async Task Send_WhenNotConnected_Throws()
    {
        await using MicroGateDeviceConnection connection = new(harness.Device.Object, new HdlcStateMachine(harness.Options));

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await connection.Send(new byte[] { 1 }));
    }

    [Fact]
    public async Task Send_WhenWriteFails_Throws()
    {
        await using MicroGateDeviceConnection connection = await harness.Connect();
        harness.Device.Setup(x => x.Write(It.IsAny<ReadOnlyMemory<byte>>())).Throws<IOException>();

        await Assert.ThrowsAsync<IOException>(async () => await connection.Send(new byte[] { 1 }));
    }

    [Fact]
    public async Task Receive_PeerDisconnect_RaisesDisconnectedOnceAndAcknowledges()
    {
        await using MicroGateDeviceConnection connection = await harness.Connect();
        int raised = 0;
        TaskCompletionSource disconnected = new();
        connection.Disconnected += (_, _) =>
        {
            Interlocked.Increment(ref raised);
            disconnected.TrySetResult();
        };

        harness.Receive(harness.Peer(HdlcFrameKind.Disconnect));
        await disconnected.Task.WaitAsync(timeout);
        harness.EndOfInput();
        await connection.DisposeAsync();

        Assert.Equal(HdlcFrameKind.UnnumberedAcknowledge, (await harness.NextWritten(1)).Kind);
        Assert.Equal(1, raised);
        Assert.False(connection.IsConnected);
    }

    [Fact]
    public async Task Receive_EndOfInputWhileConnected_RaisesDisconnected()
    {
        await using MicroGateDeviceConnection connection = await harness.Connect();
        TaskCompletionSource disconnected = new();
        connection.Disconnected += (_, _) => disconnected.TrySetResult();

        harness.EndOfInput();

        await disconnected.Task.WaitAsync(timeout);
    }

    [Fact]
    public async Task DisposeAsync_WhenConnected_SendsDisconnectDisablesReceiverAndDisposesDevice()
    {
        MicroGateDeviceConnection connection = await harness.Connect();

        await connection.DisposeAsync();
        await connection.DisposeAsync();

        Assert.Equal(HdlcFrameKind.Disconnect, (await harness.NextWritten(1)).Kind);
        harness.Device.Verify(x => x.DisableReceiver(), Times.Once);
        harness.Device.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    public async Task DisposeAsync_WhenDisconnectWriteFails_StillDisposesDevice()
    {
        MicroGateDeviceConnection connection = await harness.Connect();
        harness.Device.Setup(x => x.Write(It.IsAny<ReadOnlyMemory<byte>>())).Throws<IOException>();

        await connection.DisposeAsync();

        harness.Device.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    public async Task DisposeAsync_WhenNotConnected_DoesNotSendDisconnect()
    {
        MicroGateDeviceConnection connection = new(harness.Device.Object, new HdlcStateMachine(harness.Options));

        await connection.DisposeAsync();

        Assert.Empty(harness.Written);
        harness.Device.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    public async Task Dispose_BlocksUntilDisposed()
    {
        MicroGateDeviceConnection connection = await harness.Connect();

        connection.Dispose();

        harness.Device.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    public async Task DisposeAsync_WhenReceiveLoopFaulted_DoesNotThrow()
    {
        MicroGateDeviceConnection connection = await harness.Connect();
        harness.Device.Setup(x => x.Write(It.IsAny<ReadOnlyMemory<byte>>())).Throws<IOException>();
        harness.Receive(harness.Peer(HdlcFrameKind.Information, false, 0, new byte[] { 1 }));
        await Task.Delay(200);

        await connection.DisposeAsync();

        harness.Device.Verify(x => x.Dispose(), Times.Once);
    }
}
