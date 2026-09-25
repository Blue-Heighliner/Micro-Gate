namespace BlueHeighliner.MicroGate.Linux;

/// <summary>
/// Mirrors the native <c>struct pollfd</c> used by <c>poll(2)</c>.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct PollDescriptor
{
    /// <summary>
    /// The file descriptor to watch.
    /// </summary>
    public int FileDescriptor;

    /// <summary>
    /// The events to watch for.
    /// </summary>
    public short Events;

    /// <summary>
    /// The events that occurred, set by the call.
    /// </summary>
    public short ReturnedEvents;
}
