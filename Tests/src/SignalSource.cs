namespace BlueHeighliner.MicroGate;

internal sealed class SignalSource(MicroGateSignals initial = MicroGateSignals.None)
{
    private readonly object gate = new();
    private MicroGateSignals value = initial;
    private bool cancelled;

    public void Set(MicroGateSignals signals)
    {
        lock (gate)
        {
            value = signals;
            Monitor.PulseAll(gate);
        }
    }

    public MicroGateSignals Read()
    {
        lock (gate)
        {
            return value;
        }
    }

    public MicroGateSignals? Wait(MicroGateSignals current)
    {
        lock (gate)
        {
            while (!cancelled && value == current)
            {
                Monitor.Wait(gate);
            }

            return cancelled ? null : value;
        }
    }

    public void Cancel()
    {
        lock (gate)
        {
            cancelled = true;
            Monitor.PulseAll(gate);
        }
    }

    public void Attach(Mock<IMicroGateDevice> device)
    {
        device.Setup(x => x.ReadSignals()).Returns(Read);
        device.Setup(x => x.WaitSignals(It.IsAny<MicroGateSignals>())).Returns(Wait);
        device.Setup(x => x.CancelSignalWait()).Callback(Cancel);
    }
}
