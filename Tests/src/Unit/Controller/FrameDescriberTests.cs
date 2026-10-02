namespace BlueHeighliner.MicroGate;

public sealed class FrameDescriberTests
{
    private readonly FrameDescriber describer = new();

    [Fact]
    public void Describe_AnInformationFrame_NamesEveryFieldWithAColonAndByteValues() =>
        Assert.Equal("I  Address:33  Send:2  Receive:5  Poll/Final:1  Data:3B", describer.Describe(Create(MicroGateFrameKind.Information, [0x21, 0xB5, 1, 2, 3], [1, 2, 3], sendSequence: 2, receiveSequence: 5, pollFinal: true)));

    [Fact]
    public void Describe_ASupervisoryFrame_NamesTheReceiveSequenceButNoSendSequence() =>
        Assert.Equal("RR  Address:3  Receive:4  Poll/Final:0  Data:0B", describer.Describe(Create(MicroGateFrameKind.ReceiveReady, [0x03, 0x81], [], receiveSequence: 4)));

    [Fact]
    public void Describe_AnUnnumberedFrame_NamesNeitherSequence() =>
        Assert.Equal("SABM  Address:255  Poll/Final:0  Data:0B", describer.Describe(Create(MicroGateFrameKind.SetAsynchronousBalancedMode, [0xFF, 0x2F], [])));

    [Fact]
    public void Describe_OneByteOfData_IsSingular() =>
        Assert.EndsWith("Data:1B", describer.Describe(Create(MicroGateFrameKind.Information, [0x21, 0x00, 9], [9], sendSequence: 0, receiveSequence: 0)));

    [Fact]
    public void Describe_AMalformedFrame_ShowsTheErrorAndSize() =>
        Assert.Equal("MALFORMED  Error:too short  Size:1B", describer.Describe(Create(MicroGateFrameKind.Malformed, [0x01], [], errorMessage: "too short")));

    [Fact]
    public void Details_AnInformationFrame_ListsEveryFieldWithItsName()
    {
        IReadOnlyList<LogField> fields = describer.Details(Create(MicroGateFrameKind.Information, [0x2D, 0x22, 1, 2], [1, 2], sendSequence: 1, receiveSequence: 1));

        Assert.Equal(
            [("Address", "45"), ("Control", "34"), ("Frame Type", "Information (I)"), ("Poll/Final", "0"), ("Send", "1"), ("Receive", "1"), ("Data", "2B")],
            fields.Select(field => (field.Name, field.Value)));
    }

    [Fact]
    public void Details_AnUnnumberedFrame_OmitsTheSequenceFields()
    {
        IReadOnlyList<LogField> fields = describer.Details(Create(MicroGateFrameKind.UnnumberedAcknowledge, [0x13, 0x63], []));

        Assert.Equal(
            [("Address", "19"), ("Control", "99"), ("Frame Type", "Unnumbered Acknowledge (UA)"), ("Poll/Final", "0"), ("Data", "none")],
            fields.Select(field => (field.Name, field.Value)));
    }

    [Fact]
    public void Details_AMalformedFrame_ShowsTheErrorAndPointsAtTheBytes() =>
        Assert.Equal(
            [("Frame Type", "Malformed"), ("Error", "too short"), ("Size", "1B (shown below)")],
            describer.Details(Create(MicroGateFrameKind.Malformed, [0x01], [], errorMessage: "too short")).Select(field => (field.Name, field.Value)));

    private MicroGateFrame Create(MicroGateFrameKind kind, byte[] raw, byte[] payload, int? sendSequence = null, int? receiveSequence = null, bool pollFinal = false, string? errorMessage = null) =>
        new()
        {
            Timestamp = DateTimeOffset.Now,
            Address = raw[0],
            Kind = kind,
            PollFinal = pollFinal,
            SendSequence = sendSequence,
            ReceiveSequence = receiveSequence,
            Payload = payload,
            Raw = raw,
            ErrorMessage = errorMessage,
        };
}
