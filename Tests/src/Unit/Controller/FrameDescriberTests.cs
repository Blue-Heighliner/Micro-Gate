namespace BlueHeighliner.MicroGate;

public sealed class FrameDescriberTests
{
    private readonly FrameDescriber describer = new();

    [Fact]
    public void Describe_AnInformationFrame_ShowsKindAddressSequencesAndSize()
    {
        MicroGateFrame frame = Create(MicroGateFrameKind.Information, [0x21, 0x10, 1, 2, 3], sendSequence: 2, receiveSequence: 5, pollFinal: true);

        Assert.Equal("I     addr=0x21  N(S)=2 N(R)=5  P/F  5 bytes", describer.Describe(frame));
    }

    [Fact]
    public void Describe_ASupervisoryFrame_ShowsOnlyTheReceiveSequence() =>
        Assert.Equal("RR    addr=0x03  N(R)=4  2 bytes", describer.Describe(Create(MicroGateFrameKind.ReceiveReady, [0x03, 0x81], receiveSequence: 4)));

    [Fact]
    public void Describe_AnUnnumberedFrame_ShowsNoSequences() =>
        Assert.Equal("SABM  addr=0x03  2 bytes", describer.Describe(Create(MicroGateFrameKind.SetAsynchronousBalancedMode, [0x03, 0x3F])));

    [Fact]
    public void Describe_AMalformedFrame_ShowsTheError() =>
        Assert.Equal("MALFORMED  too short  1 byte", describer.Describe(Create(MicroGateFrameKind.Malformed, [0x01], errorMessage: "too short")));

    private MicroGateFrame Create(MicroGateFrameKind kind, byte[] raw, int? sendSequence = null, int? receiveSequence = null, bool pollFinal = false, string? errorMessage = null) =>
        new()
        {
            Timestamp = DateTimeOffset.Now,
            Address = raw[0],
            Kind = kind,
            PollFinal = pollFinal,
            SendSequence = sendSequence,
            ReceiveSequence = receiveSequence,
            Raw = raw,
            ErrorMessage = errorMessage,
        };
}
