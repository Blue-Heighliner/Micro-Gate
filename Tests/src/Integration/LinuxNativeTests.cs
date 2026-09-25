namespace BlueHeighliner.MicroGate;

public sealed class LinuxNativeTests : IDisposable
{
    private readonly string file = Path.GetTempFileName();

    public void Dispose() => File.Delete(file);

    [Fact]
    public void Open_MissingPath_ReturnsFailure()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        Assert.True(new LinuxNative().Open(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))) < 0);
    }

    [Fact]
    public void WriteReadAndClose_RoundTripThroughFile()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        LinuxNative native = new();
        int descriptor = native.Open(file);
        Assert.True(descriptor >= 0);

        Assert.Equal(3, native.Write(descriptor, new byte[] { 1, 2, 3 }));
        Assert.Equal(0, native.ClearNonBlocking(descriptor));
        Assert.Equal(0, native.Close(descriptor));

        int reopened = native.Open(file);
        byte[] buffer = new byte[16];
        Assert.Equal(3, native.Read(reopened, buffer));
        Assert.Equal(new byte[] { 1, 2, 3 }, buffer[..3]);
        native.Close(reopened);
    }

    [Fact]
    public void DeviceOnlyOperations_OnRegularFile_ReportFailureWithoutThrowing()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        LinuxNative native = new();
        int descriptor = native.Open(file);

        Assert.True(native.Drain(descriptor) < 0);
        Assert.True(native.SelectHdlcLineDiscipline(descriptor) < 0);
        Assert.True(native.SetParams(descriptor, new SynclinkParams()) < 0);
        Assert.True(native.SetTransmitIdle(descriptor, 0) < 0);
        Assert.True(native.EnableReceiver(descriptor, true) < 0);
        Assert.True(native.EnableTransmitter(descriptor, false) < 0);

        native.Close(descriptor);
    }

    [Fact]
    public void WaitReadable_OnRegularFile_ReportsReadable()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        LinuxNative native = new();
        int descriptor = native.Open(file);

        Assert.Equal(1, native.WaitReadable(descriptor, 10));

        native.Close(descriptor);
    }

    [Fact]
    public void WaitReadable_OnPseudoTerminalWithoutData_TimesOutThenReportsReadableOnceDataArrives()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using PseudoTerminal? terminal = PseudoTerminal.Create();
        if (terminal is null)
        {
            return;
        }

        LinuxNative native = new();
        int descriptor = native.Open(terminal.SlavePath);

        Assert.Equal(0, native.WaitReadable(descriptor, 20));
        terminal.Write(new byte[] { 1, 2, 3 });
        Assert.Equal(1, native.WaitReadable(descriptor, 2000));
        byte[] buffer = new byte[16];
        Assert.Equal(3, native.Read(descriptor, buffer));

        native.Close(descriptor);
    }

    [Fact]
    public void WaitReadable_OnInvalidDescriptor_ReportsFailure()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        int result = new LinuxNative().WaitReadable(9999, 10);

        Assert.True(result != 0);
    }
}
