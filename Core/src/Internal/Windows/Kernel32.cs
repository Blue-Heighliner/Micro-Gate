namespace BlueHeighliner.MicroGate.Windows;

/// <summary>
/// The Windows kernel functions used to wait for an overlapped driver call to complete.
/// </summary>
internal static partial class Kernel32
{
    /// <summary>
    /// Creates an event object, per <c>CreateEventW</c>.
    /// </summary>
    /// <param name="attributes">Security attributes, or zero.</param>
    /// <param name="manualReset">Non-zero for a manual reset event.</param>
    /// <param name="initialState">Non-zero for an event that starts signaled.</param>
    /// <param name="name">The event name, or zero for none.</param>
    /// <returns>The event handle, or zero on failure.</returns>
    [LibraryImport("kernel32.dll", EntryPoint = "CreateEventW", SetLastError = true)]
    public static partial nint CreateEvent(nint attributes, int manualReset, int initialState, nint name);

    /// <summary>
    /// Waits for an object to be signaled, per <c>WaitForSingleObject</c>.
    /// </summary>
    /// <param name="handle">The object to wait for.</param>
    /// <param name="milliseconds">The longest time to wait, or <c>0xFFFFFFFF</c> for no limit.</param>
    /// <returns>0 if the object was signaled.</returns>
    [LibraryImport("kernel32.dll", EntryPoint = "WaitForSingleObject")]
    public static partial uint WaitForSingleObject(nint handle, uint milliseconds);

    /// <summary>
    /// Closes a handle, per <c>CloseHandle</c>.
    /// </summary>
    /// <param name="handle">The handle to close.</param>
    /// <returns>Non-zero on success.</returns>
    [LibraryImport("kernel32.dll", EntryPoint = "CloseHandle", SetLastError = true)]
    public static partial int CloseHandle(nint handle);
}
