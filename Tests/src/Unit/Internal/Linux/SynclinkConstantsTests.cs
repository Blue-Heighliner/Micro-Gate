namespace BlueHeighliner.MicroGate;

public sealed class SynclinkConstantsTests
{
    [Fact]
    public void RequestCodes_MatchKernelIoctlEncoding()
    {
        Assert.Equal((nuint)0x6D02, SynclinkConstants.SetTransmitIdle);
        Assert.Equal((nuint)0x6D0A, SynclinkConstants.SetInterface);
        Assert.Equal((nuint)0x6D04, SynclinkConstants.EnableTransmitter);
        Assert.Equal((nuint)0x6D05, SynclinkConstants.EnableReceiver);
        Assert.Equal((nuint)0x5423, SynclinkConstants.SetLineDiscipline);
        Assert.Equal((nuint)(uint)((1 << 30) | (Marshal.SizeOf<SynclinkParams>() << 16) | (0x6D << 8)), SynclinkConstants.SetParams);
    }

    [Fact]
    public void FileStatusFlagMask_ClearsOnlyNonBlocking()
    {
        Assert.Equal(0, SynclinkConstants.FileStatusFlagMask & SynclinkConstants.FileStatusNonBlocking);
        Assert.Equal(SynclinkConstants.FileAccessReadWrite, SynclinkConstants.FileAccessReadWrite & SynclinkConstants.FileStatusFlagMask);
    }

    [Fact]
    public void SynclinkParams_MatchesNativeLayoutOn64BitLinux()
    {
        if (!OperatingSystem.IsLinux() || !Environment.Is64BitProcess)
        {
            return;
        }

        Assert.Equal(48, Marshal.SizeOf<SynclinkParams>());
    }
}
