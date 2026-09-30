namespace BlueHeighliner.MicroGate;

internal sealed class PayloadObserver : TestObserver<byte[]>
{
    public void Receive(IMemoryOwner<byte> data)
    {
        using (data)
        {
            OnNext(data.Memory.ToArray());
        }
    }
}
