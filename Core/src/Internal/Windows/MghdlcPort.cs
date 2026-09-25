namespace BlueHeighliner.MicroGate.Windows;

/// <summary>
/// Mirrors the native <c>struct _MGSL_PORT</c> layout defined by <c>Mghdlc.h</c>, as returned by <c>MgslEnumeratePorts</c>.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct MghdlcPort
{
    private const int DeviceNameLength = 25;

    /// <summary>
    /// The port identifier, combining an adapter number and a port number, usable with <c>MgslOpen</c>.
    /// </summary>
    public uint PortID;

    /// <summary>
    /// The hardware device identifier.
    /// </summary>
    public uint DeviceID;

    /// <summary>
    /// The hardware bus type, one of the <c>MGSL_BUS_TYPE_*</c> constants in <see cref="MghdlcConstants"/>.
    /// </summary>
    public uint BusType;

    /// <summary>
    /// The null-terminated ASCII device name, usable with <c>MgslOpenByName</c>.
    /// </summary>
    public fixed byte DeviceName[DeviceNameLength];

    /// <summary>
    /// Decodes <see cref="DeviceName"/> as a null-terminated ASCII string.
    /// </summary>
    /// <returns>The decoded device name.</returns>
    public readonly string GetDeviceName()
    {
        fixed (byte* name = DeviceName)
        {
            int length = 0;
            while (length < DeviceNameLength && name[length] != 0)
            {
                length++;
            }

            return Encoding.ASCII.GetString(name, length);
        }
    }
}
