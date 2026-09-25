namespace BlueHeighliner.MicroGate.Windows;

/// <summary>
/// An <see cref="IMicroGateDevice"/> over an opened SyncLink device handle on Windows.
/// </summary>
/// <param name="native">The native device operations.</param>
/// <param name="handle">The opened and configured device handle, owned by the device from this point on.</param>
internal sealed class WindowsMicroGateDevice(IWindowsNative native, nint handle) : IMicroGateDevice
{
    /// <inheritdoc />
    public int Read(byte[] buffer) => native.Read(handle, buffer);

    /// <inheritdoc />
    public void Write(ReadOnlyMemory<byte> frame)
    {
        byte[] buffer = frame.ToArray();

        if (native.Write(handle, buffer) != buffer.Length)
        {
            throw new IOException("Failed to write the frame to the device.");
        }
    }

    /// <inheritdoc />
    public void DisableReceiver() => native.EnableReceiver(handle, false);

    /// <inheritdoc />
    public void Dispose() => native.Close(handle);
}
