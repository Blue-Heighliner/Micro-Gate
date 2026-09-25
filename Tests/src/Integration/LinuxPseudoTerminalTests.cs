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

        MicroGateConnectionOptions options = new() { Address = 0x33 };
        HdlcStateMachine peer = new(options);
        BlockingCollection<byte[]> peerPayloads = [];
        TaskCompletionSource peerSawDisconnect = new();
        bool stopPeer = false;
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

                HdlcReceiveResult result = peer.Receive(buffer.AsMemory(0, read));
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
            }
        });

        IMicroGateConnection connection = await new MicroGateConnector().Connect(terminal.SlavePath, options).AsTask().WaitAsync(timeout);
        TaskCompletionSource<byte[]> received = new();
        connection.Received += (_, data) =>
        {
            using (data)
            {
                received.TrySetResult(data.Memory.ToArray());
            }
        };

        Assert.True(connection.IsConnected);

        await connection.Send(new byte[] { 1, 2, 3 });
        Assert.Equal(new byte[] { 1, 2, 3 }, peerPayloads.Take(new CancellationTokenSource(timeout).Token));

        terminal.Write(peer.CreateInformation(new byte[] { 9, 8 }).Span);
        Assert.Equal(new byte[] { 9, 8 }, await received.Task.WaitAsync(timeout));

        Task dispose = connection.DisposeAsync().AsTask();
        await peerSawDisconnect.Task.WaitAsync(timeout);
        Volatile.Write(ref stopPeer, true);
        await peerLoop.WaitAsync(timeout);
        terminal.CloseMaster();
        await dispose.WaitAsync(timeout);
    }
}
