namespace BlueHeighliner.MicroGate;

internal sealed class UartDeviceHarness : IDisposable
{
    public UartDeviceHarness()
    {
        Device
            .Setup(x => x.Read(It.IsAny<byte[]>()))
            .Returns((byte[] buffer) =>
            {
                try
                {
                    if (!inbound.TryTake(out byte[]? chunk, Timeout.Infinite, closed.Token))
                    {
                        return 0;
                    }

                    chunk.CopyTo(buffer, 0);
                    return chunk.Length;
                }
                catch (OperationCanceledException)
                {
                    return 0;
                }
            });
        Device
            .Setup(x => x.Write(It.IsAny<ReadOnlyMemory<byte>>()))
            .Callback((ReadOnlyMemory<byte> data) =>
            {
                lock (written)
                {
                    written.Add(data.ToArray());
                }
            });
        Device.Setup(x => x.DisableReceiver()).Callback(closed.Cancel);
        Opener.Setup(x => x.Open(It.IsAny<string>(), It.IsAny<UartPeerOptions>())).Returns(Device.Object);
    }

    private readonly BlockingCollection<byte[]> inbound = [];
    private readonly CancellationTokenSource closed = new();
    private readonly List<byte[]> written = [];

    public Mock<IMicroGateDevice> Device { get; } = new();

    public Mock<IUartDeviceOpener> Opener { get; } = new();

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

    public void Receive(params byte[] data) => inbound.Add(data);

    public void EndOfInput() => inbound.CompleteAdding();

    public UartPeer CreatePeer(TimeSpan? shutdownTimeout = null) => new(Opener.Object, Opener.Object, shutdownTimeout);

    public void Dispose()
    {
        closed.Cancel();
        inbound.Dispose();
        closed.Dispose();
    }
}
