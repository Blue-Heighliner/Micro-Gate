namespace BlueHeighliner.MicroGate;

public sealed class LinuxUartTerminalTests
{
    private readonly int controlFlagsOffset = 8;

    [Theory]
    [InlineData(9600, 1, 13u, 0u)]
    [InlineData(19200, 2, 14u, 0x40u)]
    [InlineData(115200, 1, 0x1002u, 0u)]
    public void ConfigureAsynchronous_SetsSpeedAndStopBitsAndEnablesTheReceiver(int baudRate, int stopBits, uint speed, uint stopFlag)
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
        Assert.True(descriptor >= 0);

        Assert.Equal(0, native.ConfigureAsynchronous(descriptor, baudRate, 8, stopBits, 0));

        byte[] termios = new byte[64];
        Assert.Equal(0, LibC.Tcgetattr(descriptor, termios));
        uint flags = BitConverter.ToUInt32(termios, controlFlagsOffset);
        Assert.Equal(stopFlag, flags & 0x40u);
        Assert.Equal(speed, flags & 0x100Fu);
        Assert.NotEqual(0u, flags & 0x80u);
        native.Close(descriptor);
    }

    [Fact]
    public void ConfigureAsynchronous_ForANonStandardRate_StillSucceeds()
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

        Assert.Equal(0, native.ConfigureAsynchronous(descriptor, 12345, 8, 1, 0));

        native.Close(descriptor);
    }

    [Fact]
    public void ConfigureAsynchronous_ProducesATransparentByteStream()
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
        Assert.Equal(0, native.ConfigureAsynchronous(descriptor, 9600, 8, 1, 0));
        byte[] data = [.. Enumerable.Range(0, 256).Select(i => (byte)i), 0x0D, 0x0A, 0x03, 0x1A, 0x04, 0x11, 0x13];

        terminal.Write(data);

        List<byte> received = [];
        byte[] buffer = new byte[512];
        while (received.Count < data.Length && native.WaitReadable(descriptor, 2000) > 0)
        {
            int read = native.Read(descriptor, buffer);
            Assert.True(read > 0);
            received.AddRange(buffer[..read]);
        }

        Assert.Equal(data, received);

        byte[] echoed = new byte[512];
        Assert.Equal(-2, terminal.Read(echoed, 200));
        native.Close(descriptor);
    }

    [Fact]
    public void SelectTtyLineDiscipline_OnRegularFile_ReportsFailureWithoutThrowing()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        string file = Path.GetTempFileName();
        try
        {
            LinuxNative native = new();
            int descriptor = native.Open(file);

            Assert.True(native.SelectTtyLineDiscipline(descriptor) < 0);
            Assert.True(native.ConfigureAsynchronous(descriptor, 9600, 8, 1, 0) < 0);

            native.Close(descriptor);
        }
        finally
        {
            File.Delete(file);
        }
    }
}
