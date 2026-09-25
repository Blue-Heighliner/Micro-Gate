namespace BlueHeighliner.MicroGate;

/// <summary>
/// Extensions for handing frame memory to native calls that require an array.
/// </summary>
internal static class ReadOnlyMemoryExtensions
{
    extension(ReadOnlyMemory<byte> memory)
    {
        /// <summary>
        /// Gets the array that exactly backs the memory without copying, or a copy if the memory is only part of an array or not array backed.
        /// </summary>
        /// <returns>An array holding exactly the bytes of the memory. It must not be modified.</returns>
        public byte[] ToExactArray() =>
            MemoryMarshal.TryGetArray(memory, out ArraySegment<byte> segment) && segment.Array is { } array && segment.Offset == 0 && segment.Count == array.Length
                ? array
                : memory.ToArray();
    }
}
