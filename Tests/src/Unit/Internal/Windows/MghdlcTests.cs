namespace BlueHeighliner.MicroGate;

public sealed class MghdlcTests
{
    [Fact]
    public void MghdlcParams_MatchesNativeLayout()
    {
        Assert.Equal(32, Marshal.SizeOf<MghdlcParams>());
    }

    [Fact]
    public void Constants_MatchHeader()
    {
        Assert.Equal(2u, MghdlcConstants.ModeHdlc);
        Assert.Equal(1u, MghdlcConstants.Enabled);
        Assert.Equal(0u, MghdlcConstants.Disabled);
        Assert.Equal(0u, MghdlcConstants.Success);
        Assert.Equal(0xFF, MghdlcConstants.AddressFilterDisabled);
    }
}
