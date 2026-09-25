namespace BlueHeighliner.MicroGate;

internal sealed class DroppingMicroGateDevice(IMicroGateDevice inner, Func<HdlcFrame, int, bool> shouldDrop) : IMicroGateDevice
{
    private int informationFrames;

    public int Read(byte[] buffer) => inner.Read(buffer);

    public void Write(ReadOnlyMemory<byte> frame)
    {
        HdlcFrame parsed = HdlcFrame.Parse(frame);
        int index = parsed.Kind == HdlcFrameKind.Information ? Interlocked.Increment(ref informationFrames) : 0;
        if (index > 0 && shouldDrop(parsed, index))
        {
            return;
        }

        inner.Write(frame);
    }

    public void DisableReceiver() => inner.DisableReceiver();

    public void DisableTransmitter() => inner.DisableTransmitter();

    public void Dispose() => inner.Dispose();
}
