namespace BlueHeighliner.MicroGate;

internal sealed class DeviceHarness : IDisposable
{
    public DeviceHarness(HdlcPeerOptions? options = null)
    {
        Options = options ?? new HdlcPeerOptions { RetryInterval = TimeSpan.FromMinutes(1), RetransmitInterval = null, AcknowledgeDelay = TimeSpan.Zero };

        Device
            .Setup(x => x.Read(It.IsAny<byte[]>()))
            .Returns((byte[] buffer) =>
            {
                try
                {
                    byte[] frame = inbound.Take(closed.Token);
                    frame.CopyTo(buffer, 0);
                    return frame.Length;
                }
                catch (OperationCanceledException)
                {
                    return 0;
                }
            });
        Device
            .Setup(x => x.Write(It.IsAny<ReadOnlyMemory<byte>>()))
            .Callback(Record);
        Device.Setup(x => x.DisableReceiver()).Callback(closed.Cancel);
        Opener.Setup(x => x.Open(It.IsAny<string>(), It.IsAny<HdlcPeerOptions>())).Returns(Device.Object);
    }

    private readonly BlockingCollection<byte[]> inbound = [];
    private readonly CancellationTokenSource closed = new();
    private readonly List<byte[]> written = [];
    private readonly SemaphoreSlim writtenSignal = new(0);

    public byte Address { get; } = 0x21;

    public byte RemoteAddress { get; } = 0x22;

    public HdlcPeerOptions Options { get; }

    public Mock<IMicroGateDevice> Device { get; } = new();

    public Mock<IMicroGateDeviceOpener> Opener { get; } = new();

    public IReadOnlyList<byte[]> Written
    {
        get
        {
            lock (written)
            {
                return [.. written];
            }
        }
    }

    public HdlcWireFrame Peer(HdlcWireFrameKind kind, bool pollFinal = true, int sendSequence = 0, ReadOnlyMemory<byte> payload = default, int receiveSequence = 0) =>
        new()
        {
            Address = kind is HdlcWireFrameKind.Information or HdlcWireFrameKind.SetAsynchronousBalancedMode or HdlcWireFrameKind.Disconnect ? Address : RemoteAddress,
            Kind = kind,
            PollFinal = pollFinal,
            SendSequence = sendSequence,
            ReceiveSequence = receiveSequence,
            Payload = payload,
        };

    public void Record(ReadOnlyMemory<byte> frame)
    {
        lock (written)
        {
            written.Add(frame.ToArray());
        }

        writtenSignal.Release();
    }

    public void Receive(HdlcWireFrame frame) => inbound.Add(frame.ToArray());

    public void Receive(byte[] raw) => inbound.Add(raw);

    public void EndOfInput() => closed.Cancel();

    public async Task<HdlcWireFrame> NextWritten(int index)
    {
        while (Written.Count <= index)
        {
            await writtenSignal.WaitAsync(TimeSpan.FromSeconds(5));
        }

        return HdlcWireFrame.Parse(Written[index]);
    }

    public HdlcPeer CreatePeer(TimeSpan? shutdownTimeout = null) => new(Opener.Object, Opener.Object, shutdownTimeout);

    public async Task<HdlcPeer> Connect()
    {
        HdlcPeer peer = CreatePeer();
        Task connecting = peer.StartAndConnect("port", Address, RemoteAddress, Options).AsTask();
        await NextWritten(0);
        Receive(Peer(HdlcWireFrameKind.UnnumberedAcknowledge));
        await connecting.WaitAsync(TimeSpan.FromSeconds(5));
        return peer;
    }

    public async Task<HdlcPeer> Listen()
    {
        HdlcPeer peer = CreatePeer();
        Task listening = peer.StartAndConnect("port", Address, RemoteAddress, Options with { RetryInterval = null }).AsTask();
        Receive(Peer(HdlcWireFrameKind.SetAsynchronousBalancedMode));
        await listening.WaitAsync(TimeSpan.FromSeconds(5));
        return peer;
    }

    public void Dispose()
    {
        closed.Cancel();
        inbound.Dispose();
        closed.Dispose();
        writtenSignal.Dispose();
    }
}
