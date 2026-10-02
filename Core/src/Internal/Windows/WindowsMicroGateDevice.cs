namespace BlueHeighliner.MicroGate.Windows;

/// <summary>
/// An <see cref="IMicroGateDevice"/> over an opened SyncLink device handle on Windows. Once the receiver is disabled, reads return immediately instead of reaching the driver, so a read that starts after the cancellation cannot block.
/// </summary>
/// <param name="native">The native device operations.</param>
/// <param name="handle">The opened and configured device handle, owned by the device from this point on.</param>
internal sealed class WindowsMicroGateDevice(IWindowsNative native, nint handle) : IMicroGateDevice
{
    private int receiverDisabled;

    /// <inheritdoc />
    public int Read(byte[] buffer) => Volatile.Read(ref receiverDisabled) == 0 ? native.Read(handle, buffer) : 0;

    /// <inheritdoc />
    public void Write(ReadOnlyMemory<byte> frame)
    {
        byte[] buffer = frame.ToExactArray();

        if (native.Write(handle, buffer) != buffer.Length)
        {
            throw new IOException("Failed to write the frame to the device.");
        }
    }

    /// <inheritdoc />
    public void EnableTransmitter()
    {
        uint status = native.EnableTransmitter(handle, true);
        if (status != MghdlcConstants.Success)
        {
            throw new IOException("Failed to enable the transmitter.", new Win32Exception((int)status));
        }
    }

    /// <inheritdoc />
    public void DisableReceiver()
    {
        Volatile.Write(ref receiverDisabled, 1);
        native.EnableReceiver(handle, false);
        native.CancelReceive(handle);
    }

    /// <inheritdoc />
    public void DisableTransmitter()
    {
        native.EnableTransmitter(handle, false);
        native.CancelTransmit(handle);
    }

    /// <inheritdoc />
    public void Dispose() => native.Close(handle);
}
