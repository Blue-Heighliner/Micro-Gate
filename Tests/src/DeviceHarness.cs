namespace BlueHeighliner.MicroGate;

internal sealed class DeviceHarness : IDisposable
{
    public DeviceHarness(MicroGatePeerOptions? options = null)
    {
        Options = options ?? new MicroGatePeerOptions { RetryInterval = TimeSpan.FromMinutes(1), RetransmitInterval = null, AcknowledgeDelay = TimeSpan.Zero };

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
        Opener.Setup(x => x.Open(It.IsAny<string>(), It.IsAny<MicroGatePeerOptions>())).Returns(Device.Object);
    }

    private readonly BlockingCollection<byte[]> inbound = [];
    private readonly CancellationTokenSource closed = new();
    private readonly List<byte[]> written = [];
    private readonly SemaphoreSlim writtenSignal = new(0);

    public byte Address { get; } = 0x21;

    public byte RemoteAddress { get; } = 0x22;

    public MicroGatePeerOptions Options { get; }

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

    public HdlcFrame Peer(HdlcFrameKind kind, bool pollFinal = true, int sendSequence = 0, ReadOnlyMemory<byte> payload = default, int receiveSequence = 0) =>
        new()
        {
            Address = kind is HdlcFrameKind.Information or HdlcFrameKind.SetAsynchronousBalancedMode or HdlcFrameKind.Disconnect ? Address : RemoteAddress,
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

    public void Receive(HdlcFrame frame) => inbound.Add(frame.ToArray());

    public void Receive(byte[] raw) => inbound.Add(raw);

    public void EndOfInput() => closed.Cancel();

    public async Task<HdlcFrame> NextWritten(int index)
    {
        while (Written.Count <= index)
        {
            await writtenSignal.WaitAsync(TimeSpan.FromSeconds(5));
        }

        return HdlcFrame.Parse(Written[index]);
    }

    public MicroGatePeer CreatePeer(TimeSpan? shutdownTimeout = null) => new(Opener.Object, Opener.Object, shutdownTimeout);

    public async Task<MicroGatePeer> Connect()
    {
        MicroGatePeer peer = CreatePeer();
        Task connecting = peer.StartAndConnect("port", Address, RemoteAddress, Options).AsTask();
        await NextWritten(0);
        Receive(Peer(HdlcFrameKind.UnnumberedAcknowledge));
        await connecting.WaitAsync(TimeSpan.FromSeconds(5));
        return peer;
    }

    public async Task<MicroGatePeer> Listen()
    {
        MicroGatePeer peer = CreatePeer();
        Task listening = peer.StartAndConnect("port", Address, RemoteAddress, Options with { RetryInterval = null }).AsTask();
        Receive(Peer(HdlcFrameKind.SetAsynchronousBalancedMode));
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
