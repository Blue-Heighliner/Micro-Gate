namespace BlueHeighliner.MicroGate;

internal sealed class TolerantLinuxNative(ILinuxNative inner) : ILinuxNative
{
    public int Open(string path) => inner.Open(path);

    public int Close(int fileDescriptor) => inner.Close(fileDescriptor);

    public int Read(int fileDescriptor, byte[] buffer) => inner.Read(fileDescriptor, buffer);

    public int Write(int fileDescriptor, byte[] buffer) => inner.Write(fileDescriptor, buffer);

    public int Drain(int fileDescriptor) => inner.Drain(fileDescriptor);

    public int WaitReadable(int fileDescriptor, int timeoutMilliseconds) => inner.WaitReadable(fileDescriptor, timeoutMilliseconds);

    public int ClearNonBlocking(int fileDescriptor) => inner.ClearNonBlocking(fileDescriptor);

    public int SelectHdlcLineDiscipline(int fileDescriptor) => 0;

    public int SelectTtyLineDiscipline(int fileDescriptor) => 0;

    public int ConfigureAsynchronous(int fileDescriptor, int baudRate, int dataBits, int stopBits, int parity) => inner.ConfigureAsynchronous(fileDescriptor, baudRate, dataBits, stopBits, parity);

    public int SetParams(int fileDescriptor, SynclinkParams parameters) => 0;

    public int SetTransmitIdle(int fileDescriptor, int idlePattern) => 0;

    public int SetInterface(int fileDescriptor, int interfaceType) => 0;

    public int EnableReceiver(int fileDescriptor, bool enabled) => 0;

    public int EnableTransmitter(int fileDescriptor, bool enabled) => 0;
}
