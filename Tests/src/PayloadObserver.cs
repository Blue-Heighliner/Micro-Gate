namespace BlueHeighliner.MicroGate;

internal sealed class PayloadObserver : TestObserver<byte[]>, IObserver<ReadOnlyMemory<byte>>
{
    public void OnNext(ReadOnlyMemory<byte> value) => base.OnNext(value.ToArray());
}
