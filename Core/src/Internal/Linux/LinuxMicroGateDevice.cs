namespace BlueHeighliner.MicroGate.Linux;

/// <summary>
/// An <see cref="IMicroGateDevice"/> over an opened SyncLink tty device on Linux. Reads wait in short polls rather than one blocking call, so cancelling them does not depend on the driver waking a blocked read.
/// </summary>
/// <param name="native">The native device operations.</param>
/// <param name="fileDescriptor">The opened and configured file descriptor, owned by the device from this point on.</param>
internal sealed class LinuxMicroGateDevice(ILinuxNative native, int fileDescriptor) : IMicroGateDevice
{
    private readonly int pollIntervalMilliseconds = 100;
    private readonly Lock waitLock = new();
    private readonly int signalFallbackIntervalMilliseconds = 100;
    private readonly MicroGateSignals inputs = MicroGateSignals.DataCarrierDetect | MicroGateSignals.DataSetReady | MicroGateSignals.ClearToSend;
    private int receiverDisabled;
    private int signalWaitCancelled;
    private nuint waitingThread;
    private bool waiting;

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
    public MicroGateSignals? ReadSignals()
    {
        int signals = native.GetSignals(fileDescriptor);
        if (signals < 0)
        {
            return null;
        }

        MicroGateSignals result = MicroGateSignals.None;
        result |= (signals & SynclinkConstants.ModemCarrierDetect) != 0 ? MicroGateSignals.DataCarrierDetect : MicroGateSignals.None;
        result |= (signals & SynclinkConstants.ModemDataSetReady) != 0 ? MicroGateSignals.DataSetReady : MicroGateSignals.None;
        result |= (signals & SynclinkConstants.ModemClearToSend) != 0 ? MicroGateSignals.ClearToSend : MicroGateSignals.None;
        result |= (signals & SynclinkConstants.ModemDataTerminalReady) != 0 ? MicroGateSignals.DataTerminalReady : MicroGateSignals.None;
        result |= (signals & SynclinkConstants.ModemRequestToSend) != 0 ? MicroGateSignals.RequestToSend : MicroGateSignals.None;
        return result;
    }

    /// <inheritdoc />
    public MicroGateSignals? WaitSignals(MicroGateSignals current)
    {
        while (Volatile.Read(ref signalWaitCancelled) == 0)
        {
            int result;
            lock (waitLock)
            {
                waitingThread = native.CurrentThread();
                waiting = true;
            }

            try
            {
                result = Volatile.Read(ref signalWaitCancelled) != 0 ? 0 : native.WaitSignalEvents(fileDescriptor, current.FlipEvents);
            }
            finally
            {
                lock (waitLock)
                {
                    waiting = false;
                }
            }

            if (result > 0)
            {
                MicroGateSignals? reading = ReadSignals();
                if (reading is not { } signals || (signals & inputs) != (current & inputs))
                {
                    return reading;
                }
            }
            else if (result < 0)
            {
                return PollSignals(current & inputs);
            }
        }

        return null;
    }

    /// <inheritdoc />
    public void CancelSignalWait()
    {
        Volatile.Write(ref signalWaitCancelled, 1);
        lock (waitLock)
        {
            if (waiting)
            {
                native.InterruptThread(waitingThread);
            }
        }
    }

    /// <inheritdoc />
    public void DisableTransmitter() => native.EnableTransmitter(fileDescriptor, false);

    /// <inheritdoc />
    public void Dispose() => native.Close(fileDescriptor);

    private MicroGateSignals? PollSignals(MicroGateSignals watched)
    {
        while (Volatile.Read(ref signalWaitCancelled) == 0)
        {
            Thread.Sleep(signalFallbackIntervalMilliseconds);
            MicroGateSignals? reading = ReadSignals();
            if (reading is not { } signals || (signals & inputs) != watched)
            {
                return reading;
            }
        }

        return null;
    }
}
