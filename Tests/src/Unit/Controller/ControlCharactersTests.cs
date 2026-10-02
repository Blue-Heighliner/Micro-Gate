namespace BlueHeighliner.MicroGate;

public sealed class ControlCharactersTests
{
    private readonly ControlCharacters characters = new();

    [Fact]
    public void All_HoldsTheThirtyTwoControlCharactersThenDelete()
    {
        Assert.Equal(33, characters.All.Count);
        Assert.Equal(Enumerable.Range(0, 32).Select(value => (byte)value).Append((byte)127), characters.All.Select(character => character.Value));
        Assert.Equal(characters.All.Count, characters.All.Select(character => character.Abbreviation).Distinct().Count());
    }

    [Theory]
    [InlineData(0, "NUL", "Null")]
    [InlineData(1, "SOH", "Start of Heading")]
    [InlineData(2, "STX", "Start of Text")]
    [InlineData(3, "ETX", "End of Text")]
    [InlineData(10, "LF", "Line Feed")]
    [InlineData(13, "CR", "Carriage Return")]
    [InlineData(27, "ESC", "Escape")]
    [InlineData(127, "DEL", "Delete")]
    public void Find_ReturnsTheNamedCharacter(byte value, string abbreviation, string name)
    {
        ControlCharacter? found = characters.Find(value);

        Assert.NotNull(found);
        Assert.Equal(abbreviation, found.Abbreviation);
        Assert.Equal(name, found.Name);
    }

    [Theory]
    [InlineData(32)]
    [InlineData(65)]
    [InlineData(126)]
    [InlineData(128)]
    [InlineData(255)]
    public void Find_ForAPrintableOrHighValue_ReturnsNull(byte value) => Assert.Null(characters.Find(value));
}
