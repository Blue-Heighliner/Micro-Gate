namespace BlueHeighliner.MicroGate;

/// <summary>
/// Watches the modem wires of an open device and reports their changes, and the loss of any wire a link depends on.
/// </summary>
internal interface ISignalWatcher : IAsyncDisposable
{
}

/// <summary>
/// An <see cref="ISignalWatcher"/> that blocks on the device's wire change events on a thread of its own, so a change is reported as soon as the driver signals it and nothing is polled. A watched wire counts as lost only once all the watched wires have been seen active, so a remote station that has not yet raised its outputs when the device opens does not end the link before it exists. Watching stops by itself when the device cannot report the wires.
/// </summary>
internal sealed class SignalWatcher : ISignalWatcher
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SignalWatcher"/> class and starts watching.
    /// </summary>
    /// <param name="device">The open device to read the wires of. It must stay open until the watcher has been disposed.</param>
    /// <param name="watched">The wires to watch for loss; none to only report changes.</param>
    /// <param name="changed">Called, unless <see langword="null"/>, with the wires that are active each time they change, starting with the first reading, from a thread of the watcher's own.</param>
    /// <param name="lost">Called once, with the wires that are inactive, when a watched wire goes inactive after all had been active.</param>
    public SignalWatcher(IMicroGateDevice device, MicroGateSignals watched, Action<MicroGateSignals>? changed, Action<MicroGateSignals> lost)
    {
        this.device = device;
        this.watched = watched;
        this.changed = changed;
        this.lost = lost;
        thread = new Thread(Watch) { IsBackground = true, Name = "MicroGate signal watcher" };
        thread.Start();
    }

    private readonly IMicroGateDevice device;
    private readonly MicroGateSignals watched;
    private readonly Action<MicroGateSignals>? changed;
    private readonly Action<MicroGateSignals> lost;
    private readonly Thread thread;
    private readonly TaskCompletionSource finished = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TimeSpan cancelRetryInterval = TimeSpan.FromMilliseconds(20);
    private int stopping;

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        Volatile.Write(ref stopping, 1);

        // A wake-up that arrives before the thread has started waiting is lost, so it is repeated until the thread has finished.
        while (!finished.Task.IsCompleted)
        {
            device.CancelSignalWait();
            await Task.WhenAny(finished.Task, Task.Delay(cancelRetryInterval)).ConfigureAwait(false);
        }
    }

    private void Watch()
    {
        try
        {
            MicroGateSignals? reading = device.ReadSignals();
            MicroGateSignals? last = null;
            bool armed = false;

            while (reading is { } signals && Volatile.Read(ref stopping) == 0)
            {
                if (signals != last)
                {
                    last = signals;
                    changed?.Invoke(signals);
                }

                MicroGateSignals missing = watched & ~signals;
                if (watched != MicroGateSignals.None && missing == MicroGateSignals.None)
                {
                    armed = true;
                }
                else if (armed && missing != MicroGateSignals.None)
                {
                    lost(missing);
                    return;
                }

                reading = device.WaitSignals(signals);
            }
        }
        catch (Exception exception) when (exception is IOException or ObjectDisposedException)
        {
        }
        finally
        {
            finished.TrySetResult();
        }
    }
}
