namespace BlueHeighliner.MicroGate;

internal sealed class MonitorDeviceHarness : IDisposable
{
    public MonitorDeviceHarness(MicroGateMonitorOptions? options = null)
    {
        Options = options ?? new MicroGateMonitorOptions();

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
        Device.Setup(x => x.DisableReceiver()).Callback(closed.Cancel);
        Opener.Setup(x => x.Open(It.IsAny<string>(), It.IsAny<MicroGateMonitorOptions>())).Returns(Device.Object);
    }

    private readonly BlockingCollection<byte[]> inbound = [];
    private readonly CancellationTokenSource closed = new();

    public MicroGateMonitorOptions Options { get; }

    public Mock<IMicroGateDevice> Device { get; } = new();

    public Mock<IMicroGateMonitorDeviceOpener> Opener { get; } = new();

    public HdlcFrame Frame(HdlcFrameKind kind, byte address = 0xFF, bool pollFinal = true, int sendSequence = 0, int receiveSequence = 0, ReadOnlyMemory<byte> payload = default) =>
        new()
        {
            Address = address,
            Kind = kind,
            PollFinal = pollFinal,
            SendSequence = sendSequence,
            ReceiveSequence = receiveSequence,
            Payload = payload,
        };

    public void Receive(HdlcFrame frame) => inbound.Add(frame.ToArray());

    public void Receive(byte[] raw) => inbound.Add(raw);

    public void EndOfInput() => closed.Cancel();

    public MicroGateMonitor CreateMonitor(TimeSpan? shutdownTimeout = null) => new(Opener.Object, Opener.Object, shutdownTimeout);

    public async Task<MicroGateMonitor> Started()
    {
        MicroGateMonitor monitor = CreateMonitor();
        await monitor.Start("port", Options);
        return monitor;
    }

    public void Dispose()
    {
        closed.Cancel();
        inbound.Dispose();
        closed.Dispose();
    }
}
