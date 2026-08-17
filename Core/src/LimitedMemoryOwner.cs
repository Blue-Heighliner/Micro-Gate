namespace BlueHeighliner.MicroGate;

/// <summary>
/// Wraps a pooled <see cref="IMemoryOwner{T}"/> so that only its first <paramref name="length"/> bytes, rather than the pool's full rented buffer, are exposed through <see cref="Memory"/>.
/// </summary>
/// <param name="owner">The pooled memory owner to wrap and dispose on behalf of.</param>
/// <param name="length">The number of bytes, from the start of <paramref name="owner"/>'s memory, that are valid.</param>
internal sealed class LimitedMemoryOwner(IMemoryOwner<byte> owner, int length) : IMemoryOwner<byte>
{
    public Memory<byte> Memory => owner.Memory[..length];

    public void Dispose() => owner.Dispose();
}
