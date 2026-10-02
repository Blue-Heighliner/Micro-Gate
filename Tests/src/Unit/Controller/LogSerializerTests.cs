namespace BlueHeighliner.MicroGate;

public sealed class LogSerializerTests
{
    private readonly LogSerializer serializer = new();

    [Fact]
    public async Task SaveThenLoad_RestoresTextDataAndPerCellDisplayChoices()
    {
        LogEntry status = new("12:00:00.000  Connected.", null);
        LogEntry data = new("12:00:01.000  RX  3 bytes", [0x41, 0x0A, 0xFF]);
        data.Cells[1].ShowRaw = true;
        using MemoryStream stream = new();

        await serializer.Save([status, data], stream);
        stream.Position = 0;
        IReadOnlyList<LogEntry> loaded = await serializer.Load(stream);

        Assert.Equal(2, loaded.Count);
        Assert.Equal(status.Text, loaded[0].Text);
        Assert.False(loaded[0].HasData);
        Assert.Equal(data.Text, loaded[1].Text);
        Assert.Equal([(byte)0x41, (byte)0x0A, (byte)0xFF], loaded[1].Cells.Select(cell => cell.Value));
        Assert.Equal([false, true, false], loaded[1].Cells.Select(cell => cell.ShowRaw));
    }

    [Fact]
    public async Task SaveThenLoad_KeepsAnEmptyDataRowDistinctFromAStatusMessage()
    {
        using MemoryStream stream = new();

        await serializer.Save([new LogEntry("empty", [])], stream);
        stream.Position = 0;
        IReadOnlyList<LogEntry> loaded = await serializer.Load(stream);

        Assert.True(loaded[0].HasData);
        Assert.Empty(loaded[0].Cells);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{\"Version\":1,\"Entries\":[{\"Text\":\"x\",\"Data\":\"***\"}]}")]
    [InlineData("{\"Version\":2,\"Entries\":[]}")]
    public async Task Load_WithAnInvalidFile_ThrowsInvalidData(string content)
    {
        using MemoryStream stream = new(Encoding.UTF8.GetBytes(content));

        await Assert.ThrowsAsync<InvalidDataException>(async () => await serializer.Load(stream));
    }

    [Fact]
    public async Task Load_IgnoresRawCellIndexesOutsideTheData()
    {
        using MemoryStream stream = new(Encoding.UTF8.GetBytes("{\"Version\":1,\"Entries\":[{\"Text\":\"x\",\"Data\":\"AQI=\",\"RawCells\":[-1,0,5]}]}"));

        IReadOnlyList<LogEntry> loaded = await serializer.Load(stream);

        Assert.Equal([true, false], loaded[0].Cells.Select(cell => cell.ShowRaw));
    }
}
