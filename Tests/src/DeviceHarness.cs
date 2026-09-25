namespace BlueHeighliner.MicroGate;

internal sealed class DeviceHarness : IDisposable
{
    public DeviceHarness(MicroGateConnectionOptions? options = null)
    {
        Options = options ?? new MicroGateConnectionOptions();

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
            .Callback((ReadOnlyMemory<byte> frame) =>
            {
                lock (written)
                {
                    written.Add(frame.ToArray());
                }

                writtenSignal.Release();
            });
        Device.Setup(x => x.DisableReceiver()).Callback(closed.Cancel);
    }

    private readonly BlockingCollection<byte[]> inbound = [];
    private readonly CancellationTokenSource closed = new();
    private readonly List<byte[]> written = [];
    private readonly SemaphoreSlim writtenSignal = new(0);

    public MicroGateConnectionOptions Options { get; }

    public Mock<IMicroGateDevice> Device { get; } = new();

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

    public HdlcFrame Peer(HdlcFrameKind kind, bool pollFinal = true, int sendSequence = 0, ReadOnlyMemory<byte> payload = default) =>
        new()
        {
            Address = Options.Address,
            Kind = kind,
            PollFinal = pollFinal,
            SendSequence = sendSequence,
            Payload = payload,
        };

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

    public async Task<MicroGateDeviceConnection> Connect()
    {
        MicroGateDeviceConnection connection = new(Device.Object, new HdlcStateMachine(Options));
        Task establish = connection.Establish(CancellationToken.None);
        await NextWritten(0);
        Receive(Peer(HdlcFrameKind.UnnumberedAcknowledge));
        await establish.WaitAsync(TimeSpan.FromSeconds(5));
        return connection;
    }

    public void Dispose()
    {
        closed.Cancel();
        inbound.Dispose();
        closed.Dispose();
        writtenSignal.Dispose();
    }
}
