namespace BlueHeighliner.MicroGate.Windows;

/// <summary>
/// An <see cref="IMicroGateDevice"/> over an opened SyncLink device handle on Windows. Once the receiver is disabled, reads return immediately instead of reaching the driver, so a read that starts after the cancellation cannot block.
/// </summary>
/// <param name="native">The native device operations.</param>
/// <param name="handle">The opened and configured device handle, owned by the device from this point on.</param>
internal sealed class WindowsMicroGateDevice(IWindowsNative native, nint handle) : IMicroGateDevice
{
    private readonly int signalFallbackIntervalMilliseconds = 100;
    private readonly MicroGateSignals inputs = MicroGateSignals.DataCarrierDetect | MicroGateSignals.DataSetReady | MicroGateSignals.ClearToSend;
    private int receiverDisabled;
    private int signalWaitCancelled;

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

        native.WaitAllSent(handle);
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
    public MicroGateSignals? ReadSignals()
    {
        if (native.GetSignals(handle, out byte signals) != MghdlcConstants.Success)
        {
            return null;
        }

        MicroGateSignals result = MicroGateSignals.None;
        result |= (signals & MghdlcConstants.SignalCarrierDetect) != 0 ? MicroGateSignals.DataCarrierDetect : MicroGateSignals.None;
        result |= (signals & MghdlcConstants.SignalDataSetReady) != 0 ? MicroGateSignals.DataSetReady : MicroGateSignals.None;
        result |= (signals & MghdlcConstants.SignalClearToSend) != 0 ? MicroGateSignals.ClearToSend : MicroGateSignals.None;
        result |= (signals & MghdlcConstants.SignalDataTerminalReady) != 0 ? MicroGateSignals.DataTerminalReady : MicroGateSignals.None;
        result |= (signals & MghdlcConstants.SignalRequestToSend) != 0 ? MicroGateSignals.RequestToSend : MicroGateSignals.None;
        return result;
    }

    /// <inheritdoc />
    public MicroGateSignals? WaitSignals(MicroGateSignals current)
    {
        while (Volatile.Read(ref signalWaitCancelled) == 0)
        {
            uint status = native.WaitSignalEvents(handle, (uint)current.FlipEvents, out uint events);
            if (status != MghdlcConstants.Success)
            {
                return PollSignals(current & inputs);
            }

            if (events != 0)
            {
                MicroGateSignals? reading = ReadSignals();
                if (reading is not { } signals || (signals & inputs) != (current & inputs))
                {
                    return reading;
                }
            }
        }

        return null;
    }

    /// <inheritdoc />
    public void CancelSignalWait()
    {
        Volatile.Write(ref signalWaitCancelled, 1);
        native.CancelSignalWait(handle);
    }

    /// <inheritdoc />
    public void DisableTransmitter()
    {
        native.EnableTransmitter(handle, false);
        native.CancelTransmit(handle);
    }

    /// <inheritdoc />
    public void Dispose() => native.Close(handle);

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
