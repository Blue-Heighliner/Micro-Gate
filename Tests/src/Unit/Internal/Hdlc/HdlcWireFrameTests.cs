namespace BlueHeighliner.MicroGate;

public sealed class HdlcWireFrameTests
{
    [Fact]
    public void ToArray_ThenParse_Information_PreservesFields()
    {
        HdlcWireFrame frame = new()
        {
            Address = 0x7F,
            Kind = HdlcWireFrameKind.Information,
            PollFinal = true,
            SendSequence = 3,
            ReceiveSequence = 5,
            Payload = new byte[] { 1, 2, 3, 4 },
        };

        HdlcWireFrame parsed = HdlcWireFrame.Parse(frame.ToArray());

        Assert.Equal(frame.Address, parsed.Address);
        Assert.Equal(frame.Kind, parsed.Kind);
        Assert.Equal(frame.PollFinal, parsed.PollFinal);
        Assert.Equal(frame.SendSequence, parsed.SendSequence);
        Assert.Equal(frame.ReceiveSequence, parsed.ReceiveSequence);
        Assert.Equal(frame.Payload.ToArray(), parsed.Payload.ToArray());
    }

    [Fact]
    public void ToArray_ThenParse_ReceiveReady_PreservesFields() => AssertSupervisoryRoundTrip(HdlcWireFrameKind.ReceiveReady);

    [Fact]
    public void ToArray_ThenParse_ReceiveNotReady_PreservesFields() => AssertSupervisoryRoundTrip(HdlcWireFrameKind.ReceiveNotReady);

    [Fact]
    public void ToArray_ThenParse_Reject_PreservesFields() => AssertSupervisoryRoundTrip(HdlcWireFrameKind.Reject);

    [Fact]
    public void ToArray_ThenParse_SetAsynchronousBalancedMode_PreservesFields() => AssertUnnumberedRoundTrip(HdlcWireFrameKind.SetAsynchronousBalancedMode);

    [Fact]
    public void ToArray_ThenParse_Disconnect_PreservesFields() => AssertUnnumberedRoundTrip(HdlcWireFrameKind.Disconnect);

    [Fact]
    public void ToArray_ThenParse_UnnumberedAcknowledge_PreservesFields() => AssertUnnumberedRoundTrip(HdlcWireFrameKind.UnnumberedAcknowledge);

    [Fact]
    public void ToArray_ThenParse_DisconnectedMode_PreservesFields() => AssertUnnumberedRoundTrip(HdlcWireFrameKind.DisconnectedMode);

    [Fact]
    public void ToArray_ThenParse_FrameReject_PreservesFields() => AssertUnnumberedRoundTrip(HdlcWireFrameKind.FrameReject);

    [Fact]
    public void Parse_TooShort_Throws()
    {
        Assert.Throws<HdlcWireFrameException>(() => HdlcWireFrame.Parse(new byte[] { 0xFF }));
    }

    [Fact]
    public void Parse_UnrecognizedUnnumberedControlByte_Throws()
    {
        byte[] data = [0xFF, 0xEF];

        Assert.Throws<HdlcWireFrameException>(() => HdlcWireFrame.Parse(data));
    }

    private void AssertSupervisoryRoundTrip(HdlcWireFrameKind kind)
    {
        HdlcWireFrame frame = new()
        {
            Address = 0x01,
            Kind = kind,
            PollFinal = false,
            ReceiveSequence = 6,
        };

        HdlcWireFrame parsed = HdlcWireFrame.Parse(frame.ToArray());

        Assert.Equal(kind, parsed.Kind);
        Assert.Equal(6, parsed.ReceiveSequence);
        Assert.False(parsed.PollFinal);
    }

    private void AssertUnnumberedRoundTrip(HdlcWireFrameKind kind)
    {
        HdlcWireFrame frame = new()
        {
            Address = 0xFF,
            Kind = kind,
            PollFinal = true,
        };

        HdlcWireFrame parsed = HdlcWireFrame.Parse(frame.ToArray());

        Assert.Equal(kind, parsed.Kind);
        Assert.True(parsed.PollFinal);
    }

    [Fact]
    public void Parse_UnsupportedSupervisoryControlByte_Throws()
    {
        byte[] data = [0xFF, 0x0D];

        Assert.Throws<HdlcWireFrameException>(() => HdlcWireFrame.Parse(data));
    }

    [Fact]
    public void ToArray_UnrecognizedKind_Throws()
    {
        HdlcWireFrame frame = new() { Address = 1, Kind = (HdlcWireFrameKind)99, PollFinal = false };

        Assert.Throws<ArgumentOutOfRangeException>(() => frame.ToArray());
    }

    [Fact]
    public void ToArray_EncodesKnownControlBytes()
    {
        Assert.Equal(new byte[] { 0x01, 0x3F }, new HdlcWireFrame { Address = 1, Kind = HdlcWireFrameKind.SetAsynchronousBalancedMode, PollFinal = true }.ToArray());
        Assert.Equal(new byte[] { 0x01, 0x43 }, new HdlcWireFrame { Address = 1, Kind = HdlcWireFrameKind.Disconnect, PollFinal = false }.ToArray());
        Assert.Equal(new byte[] { 0x01, 0x73 }, new HdlcWireFrame { Address = 1, Kind = HdlcWireFrameKind.UnnumberedAcknowledge, PollFinal = true }.ToArray());
        Assert.Equal(new byte[] { 0x01, 0x01 }, new HdlcWireFrame { Address = 1, Kind = HdlcWireFrameKind.ReceiveReady, PollFinal = false }.ToArray());
        Assert.Equal(new byte[] { 0x01, 0xA5 }, new HdlcWireFrame { Address = 1, Kind = HdlcWireFrameKind.ReceiveNotReady, PollFinal = false, ReceiveSequence = 5 }.ToArray());
        Assert.Equal(new byte[] { 0x01, 0x09 }, new HdlcWireFrame { Address = 1, Kind = HdlcWireFrameKind.Reject, PollFinal = false }.ToArray());
        Assert.Equal(new byte[] { 0x01, 0xA6, 9 }, new HdlcWireFrame { Address = 1, Kind = HdlcWireFrameKind.Information, PollFinal = false, SendSequence = 3, ReceiveSequence = 5, Payload = new byte[] { 9 } }.ToArray());
    }

    [Fact]
    public void Parse_PayloadReferencesTheInputInsteadOfCopying()
    {
        byte[] data = [0x01, 0x00, 7, 8, 9];

        HdlcWireFrame frame = HdlcWireFrame.Parse(data);

        Assert.True(frame.Payload.Span.Overlaps(data.AsSpan(2), out int offset));
        Assert.Equal(0, offset);
        Assert.Equal(new byte[] { 7, 8, 9 }, frame.Payload.ToArray());
    }
}
