namespace BlueHeighliner.MicroGate;

public sealed class HdlcFrameTests
{
    [Fact]
    public void ToArray_ThenParse_Information_PreservesFields()
    {
        HdlcFrame frame = new()
        {
            Address = 0x7F,
            Kind = HdlcFrameKind.Information,
            PollFinal = true,
            SendSequence = 3,
            ReceiveSequence = 5,
            Payload = new byte[] { 1, 2, 3, 4 },
        };

        HdlcFrame parsed = HdlcFrame.Parse(frame.ToArray());

        Assert.Equal(frame.Address, parsed.Address);
        Assert.Equal(frame.Kind, parsed.Kind);
        Assert.Equal(frame.PollFinal, parsed.PollFinal);
        Assert.Equal(frame.SendSequence, parsed.SendSequence);
        Assert.Equal(frame.ReceiveSequence, parsed.ReceiveSequence);
        Assert.Equal(frame.Payload.ToArray(), parsed.Payload.ToArray());
    }

    [Fact]
    public void ToArray_ThenParse_ReceiveReady_PreservesFields() => AssertSupervisoryRoundTrip(HdlcFrameKind.ReceiveReady);

    [Fact]
    public void ToArray_ThenParse_ReceiveNotReady_PreservesFields() => AssertSupervisoryRoundTrip(HdlcFrameKind.ReceiveNotReady);

    [Fact]
    public void ToArray_ThenParse_Reject_PreservesFields() => AssertSupervisoryRoundTrip(HdlcFrameKind.Reject);

    [Fact]
    public void ToArray_ThenParse_SetAsynchronousBalancedMode_PreservesFields() => AssertUnnumberedRoundTrip(HdlcFrameKind.SetAsynchronousBalancedMode);

    [Fact]
    public void ToArray_ThenParse_Disconnect_PreservesFields() => AssertUnnumberedRoundTrip(HdlcFrameKind.Disconnect);

    [Fact]
    public void ToArray_ThenParse_UnnumberedAcknowledge_PreservesFields() => AssertUnnumberedRoundTrip(HdlcFrameKind.UnnumberedAcknowledge);

    [Fact]
    public void ToArray_ThenParse_DisconnectedMode_PreservesFields() => AssertUnnumberedRoundTrip(HdlcFrameKind.DisconnectedMode);

    [Fact]
    public void ToArray_ThenParse_FrameReject_PreservesFields() => AssertUnnumberedRoundTrip(HdlcFrameKind.FrameReject);

    [Fact]
    public void Parse_TooShort_Throws()
    {
        Assert.Throws<HdlcFrameException>(() => HdlcFrame.Parse(new byte[] { 0xFF }));
    }

    [Fact]
    public void Parse_UnrecognizedUnnumberedControlByte_Throws()
    {
        byte[] data = [0xFF, 0xEF];

        Assert.Throws<HdlcFrameException>(() => HdlcFrame.Parse(data));
    }

    private void AssertSupervisoryRoundTrip(HdlcFrameKind kind)
    {
        HdlcFrame frame = new()
        {
            Address = 0x01,
            Kind = kind,
            PollFinal = false,
            ReceiveSequence = 6,
        };

        HdlcFrame parsed = HdlcFrame.Parse(frame.ToArray());

        Assert.Equal(kind, parsed.Kind);
        Assert.Equal(6, parsed.ReceiveSequence);
        Assert.False(parsed.PollFinal);
    }

    private void AssertUnnumberedRoundTrip(HdlcFrameKind kind)
    {
        HdlcFrame frame = new()
        {
            Address = 0xFF,
            Kind = kind,
            PollFinal = true,
        };

        HdlcFrame parsed = HdlcFrame.Parse(frame.ToArray());

        Assert.Equal(kind, parsed.Kind);
        Assert.True(parsed.PollFinal);
    }

    [Fact]
    public void Parse_UnsupportedSupervisoryControlByte_Throws()
    {
        byte[] data = [0xFF, 0x0D];

        Assert.Throws<HdlcFrameException>(() => HdlcFrame.Parse(data));
    }

    [Fact]
    public void ToArray_UnrecognizedKind_Throws()
    {
        HdlcFrame frame = new() { Address = 1, Kind = (HdlcFrameKind)99, PollFinal = false };

        Assert.Throws<ArgumentOutOfRangeException>(() => frame.ToArray());
    }

    [Fact]
    public void ToArray_EncodesKnownControlBytes()
    {
        Assert.Equal(new byte[] { 0x01, 0x3F }, new HdlcFrame { Address = 1, Kind = HdlcFrameKind.SetAsynchronousBalancedMode, PollFinal = true }.ToArray());
        Assert.Equal(new byte[] { 0x01, 0x43 }, new HdlcFrame { Address = 1, Kind = HdlcFrameKind.Disconnect, PollFinal = false }.ToArray());
        Assert.Equal(new byte[] { 0x01, 0x73 }, new HdlcFrame { Address = 1, Kind = HdlcFrameKind.UnnumberedAcknowledge, PollFinal = true }.ToArray());
        Assert.Equal(new byte[] { 0x01, 0x01 }, new HdlcFrame { Address = 1, Kind = HdlcFrameKind.ReceiveReady, PollFinal = false }.ToArray());
        Assert.Equal(new byte[] { 0x01, 0xA5 }, new HdlcFrame { Address = 1, Kind = HdlcFrameKind.ReceiveNotReady, PollFinal = false, ReceiveSequence = 5 }.ToArray());
        Assert.Equal(new byte[] { 0x01, 0x09 }, new HdlcFrame { Address = 1, Kind = HdlcFrameKind.Reject, PollFinal = false }.ToArray());
        Assert.Equal(new byte[] { 0x01, 0xA6, 9 }, new HdlcFrame { Address = 1, Kind = HdlcFrameKind.Information, PollFinal = false, SendSequence = 3, ReceiveSequence = 5, Payload = new byte[] { 9 } }.ToArray());
    }
}
