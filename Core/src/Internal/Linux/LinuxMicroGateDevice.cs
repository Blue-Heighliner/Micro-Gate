namespace BlueHeighliner.MicroGate.Linux;

/// <summary>
/// An <see cref="IMicroGateDevice"/> over an opened SyncLink tty device on Linux.
/// </summary>
/// <param name="native">The native device operations.</param>
/// <param name="fileDescriptor">The opened and configured file descriptor, owned by the device from this point on.</param>
internal sealed class LinuxMicroGateDevice(ILinuxNative native, int fileDescriptor) : IMicroGateDevice
{
    /// <inheritdoc />
    public int Read(byte[] buffer) => native.Read(fileDescriptor, buffer);

    /// <inheritdoc />
    public void Write(ReadOnlyMemory<byte> frame)
    {
        byte[] buffer = frame.ToArray();

        int bytesWritten = native.Write(fileDescriptor, buffer);
        if (bytesWritten != buffer.Length)
        {
            throw new IOException("Failed to write the frame to the device.", new Win32Exception(Marshal.GetLastPInvokeError()));
        }

        native.Drain(fileDescriptor);
    }

    /// <inheritdoc />
    public void DisableReceiver() => native.EnableReceiver(fileDescriptor, false);

    /// <inheritdoc />
    public void Dispose() => native.Close(fileDescriptor);
}
