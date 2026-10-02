namespace BlueHeighliner.MicroGate;

public sealed class LinuxPseudoTerminalTests
{
    private readonly TimeSpan timeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task ConnectSendReceiveAndDispose_AgainstPeerOnPseudoTerminal()
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

        MicroGatePeerOptions options = new();
        HdlcStateMachine peer = new(options, 0x34, 0x33);
        BlockingCollection<byte[]> peerPayloads = [];
        TaskCompletionSource peerSawDisconnect = new();
        bool stopPeer = false;
        SemaphoreSlim peerProcessed = new(0);
        Task peerLoop = Task.Run(() =>
        {
            byte[] buffer = new byte[4096];
            while (!Volatile.Read(ref stopPeer))
            {
                int read = terminal.Read(buffer, 50);
                if (read == -2)
                {
                    continue;
                }

                if (read <= 0)
                {
                    return;
                }

                HdlcReceiveResult result;
                lock (peer)
                {
                    result = peer.Receive(buffer.AsMemory(0, read));
                }

                if (result.Payload is { } payload)
                {
                    peerPayloads.Add(payload.ToArray());
                }

                if (result.Response is { } response)
                {
                    terminal.Write(response.Span);
                }

                if (result.State == HdlcConnectionState.Disconnected)
                {
                    peerSawDisconnect.TrySetResult();
                }

                peerProcessed.Release();
            }
        });

        IMicroGatePeer connection = new MicroGatePeer(new LinuxMicroGateDeviceOpener(new TolerantLinuxNative(new LinuxNative())), new Mock<IMicroGateDeviceOpener>().Object);
        PayloadObserver received = new();
        TestObserver<MicroGatePeerState> states = new();
        connection.Receiver = received.Receive;
        connection.StateChanged.Subscribe(states);

        await connection.StartAndConnect(terminal.SlavePath, 0x33, 0x34, options).AsTask().WaitAsync(timeout);

        Assert.True(connection.IsConnected);

        ReadOnlyMemory<byte> information;
        lock (peer)
        {
            information = peer.CreateInformation(new byte[] { 9, 8 });
        }

        terminal.Write(information.Span);
        Assert.Equal(new byte[] { 9, 8 }, await received.Next());
        await peerProcessed.WaitAsync(timeout);
        await peerProcessed.WaitAsync(timeout);

        await connection.Send(new byte[] { 1, 2, 3 });
        Assert.Equal(new byte[] { 1, 2, 3 }, peerPayloads.Take(new CancellationTokenSource(timeout).Token));
        await peerProcessed.WaitAsync(timeout);
        await Task.Delay(100);

        Task dispose = connection.DisposeAsync().AsTask();
        await peerSawDisconnect.Task.WaitAsync(timeout);
        Volatile.Write(ref stopPeer, true);
        await peerLoop.WaitAsync(timeout);
        terminal.CloseMaster();
        await dispose.WaitAsync(timeout);
        Assert.Equal([MicroGatePeerState.Ready, MicroGatePeerState.Connecting, MicroGatePeerState.Connected, MicroGatePeerState.Disconnected], states.Seen);
    }
}
