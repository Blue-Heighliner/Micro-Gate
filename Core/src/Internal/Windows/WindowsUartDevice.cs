namespace BlueHeighliner.MicroGate.Windows;

/// <summary>
/// An <see cref="IMicroGateDevice"/> over an opened SyncLink device handle on Windows in asynchronous mode. The driver returns an asynchronous read only once it has the requested number of bytes, so reading one byte at a time is what delivers each as soon as it arrives. Everything else is the same as for a frame device.
/// </summary>
/// <param name="native">The native device operations.</param>
/// <param name="handle">The opened and configured device handle, owned by the device from this point on.</param>
internal sealed class WindowsUartDevice(IWindowsNative native, nint handle) : IMicroGateDevice
{
    private readonly WindowsMicroGateDevice inner = new(native, handle);
    private int receiverDisabled;

    /// <inheritdoc />
    public int Read(byte[] buffer)
    {
        if (Volatile.Read(ref receiverDisabled) != 0)
        {
            return 0;
        }

        byte[] single = new byte[1];
        int count = native.Read(handle, single);
        if (count > 0)
        {
            buffer[0] = single[0];
        }

        return count;
    }

    /// <inheritdoc />
    public void Write(ReadOnlyMemory<byte> frame) => inner.Write(frame);

    /// <inheritdoc />
    public void EnableTransmitter() => inner.EnableTransmitter();

    /// <inheritdoc />
    public void DisableReceiver()
    {
        Volatile.Write(ref receiverDisabled, 1);
        inner.DisableReceiver();
    }

    /// <inheritdoc />
    public void DisableTransmitter() => inner.DisableTransmitter();

    /// <inheritdoc />
    public void Dispose() => inner.Dispose();
}
