namespace BlueHeighliner.MicroGate.Linux;

/// <summary>
/// An <see cref="IMicroGateDevice"/> over an opened SyncLink tty device on Linux. Reads wait in short polls rather than one blocking call, so cancelling them does not depend on the driver waking a blocked read.
/// </summary>
/// <param name="native">The native device operations.</param>
/// <param name="fileDescriptor">The opened and configured file descriptor, owned by the device from this point on.</param>
internal sealed class LinuxMicroGateDevice(ILinuxNative native, int fileDescriptor) : IMicroGateDevice
{
    private readonly int pollIntervalMilliseconds = 100;
    private int receiverDisabled;

    /// <inheritdoc />
    public int Read(byte[] buffer)
    {
        while (Volatile.Read(ref receiverDisabled) == 0)
        {
            int ready = native.WaitReadable(fileDescriptor, pollIntervalMilliseconds);
            if (ready < 0)
            {
                return -1;
            }

            if (ready > 0)
            {
                return native.Read(fileDescriptor, buffer);
            }
        }

        return 0;
    }

    /// <inheritdoc />
    public void Write(ReadOnlyMemory<byte> frame)
    {
        byte[] buffer = frame.ToExactArray();

        int bytesWritten = native.Write(fileDescriptor, buffer);
        if (bytesWritten != buffer.Length)
        {
            throw new IOException("Failed to write the frame to the device.", new Win32Exception(Marshal.GetLastPInvokeError()));
        }

        native.Drain(fileDescriptor);
    }

    /// <inheritdoc />
    public void EnableTransmitter()
    {
        if (native.EnableTransmitter(fileDescriptor, true) < 0)
        {
            throw new IOException("Failed to enable the transmitter.", new Win32Exception(Marshal.GetLastPInvokeError()));
        }
    }

    /// <inheritdoc />
    public void DisableReceiver()
    {
        Volatile.Write(ref receiverDisabled, 1);
        native.EnableReceiver(fileDescriptor, false);
    }

    /// <inheritdoc />
    public void DisableTransmitter() => native.EnableTransmitter(fileDescriptor, false);

    /// <inheritdoc />
    public void Dispose() => native.Close(fileDescriptor);
}
