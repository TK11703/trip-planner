using TripPlanner.Api.Features.FavoriteDestinations;

namespace TripPlanner.Api.Tests.FavoriteDestinations;

public sealed class FavoriteDestinationImportParserTests
{
    [Fact]
    public async Task ParseJsonArray_TracksSourceRowsAndOptionalFields()
    {
        const string json = """
        [
          { "name": "Museum", "address": "Berlin", "notes": "A, B" },
          { "name": "Garden", "address": "Paris" }
        ]
        """;

        var result = await new FavoriteDestinationImportParser().ParseAsync("favorites.json", new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json)));

        Assert.True(result.IsValid);
        Assert.Equal(2, result.Rows.Count);
        Assert.Equal(1, result.Rows[0].RowNumber);
        Assert.Equal("A, B", result.Rows[0].Record.Notes);
        Assert.Equal(2, result.Rows[1].RowNumber);
        Assert.Null(result.Rows[1].Record.Notes);
    }

    [Fact]
    public async Task ParseJson_RejectsNonArrayAndMalformedContent()
    {
        var parser = new FavoriteDestinationImportParser();
        var objectResult = await parser.ParseAsync("favorites.json", Bytes("{\"name\":\"Museum\"}"));
        var malformedResult = await parser.ParseAsync("favorites.json", Bytes("[{"));

        Assert.False(objectResult.IsValid);
        Assert.False(malformedResult.IsValid);
        Assert.NotEmpty(objectResult.Errors);
        Assert.NotEmpty(malformedResult.Errors);
    }

    [Fact]
    public async Task ParseCsv_HandlesQuotedCommasQuotesAndNewlines_AndUsesSourceLineNumbers()
    {
        const string csv = "name,address,notes\r\n\"Museum, North\",\"1 \"\"Main\"\" St\",\"first line\nsecond line\"\r\nGarden,Paris,\r\n";

        var result = await new FavoriteDestinationImportParser().ParseAsync("favorites.csv", Bytes(csv));

        Assert.True(result.IsValid);
        Assert.Equal(2, result.Rows.Count);
        Assert.Equal(2, result.Rows[0].RowNumber);
        Assert.Equal("Museum, North", result.Rows[0].Record.Name);
        Assert.Equal("1 \"Main\" St", result.Rows[0].Record.Address);
        Assert.Contains("second line", result.Rows[0].Record.Notes);
        Assert.Equal(3, result.Rows[1].RowNumber);
    }

    [Fact]
    public async Task ParseCsv_MapsGoogleExportFields_AndSkipsEmptyPlaceholderRows()
    {
        const string csv = """
        Title,Note,URL,Tags,Comment
        ,,,,
        Galeries Lafayette Haussmann,Visit rooftop,https://www.google.com/maps/place/Galeries+Lafayette+Haussmann/data=!4m2!3m1!1s0x47e66e3703a1108b:0xe6773845cdab1593,Paris,Great view
        """;

        var result = await new FavoriteDestinationImportParser().ParseAsync("saved-places.csv", Bytes(csv));

        Assert.True(result.IsValid);
        var row = Assert.Single(result.Rows);
        Assert.Equal(3, row.RowNumber);
        Assert.Equal("Galeries Lafayette Haussmann", row.Record.Name);
        Assert.Equal("https://www.google.com/maps/place/Galeries+Lafayette+Haussmann/data=!4m2!3m1!1s0x47e66e3703a1108b:0xe6773845cdab1593", row.Record.Address);
        Assert.Equal($"Visit rooftop{Environment.NewLine}Great view", row.Record.Notes);
    }

    [Fact]
    public async Task ParseCsv_RejectsMissingOrUnexpectedHeaders()
    {
        var parser = new FavoriteDestinationImportParser();

        var missing = await parser.ParseAsync("favorites.csv", Bytes("name,address\r\nMuseum,Berlin\r\n"));
        var unexpected = await parser.ParseAsync("favorites.csv", Bytes("name,address,notes,source\r\nMuseum,Berlin,,Guide\r\n"));

        Assert.False(missing.IsValid);
        Assert.False(unexpected.IsValid);
        Assert.NotEmpty(missing.Errors);
        Assert.NotEmpty(unexpected.Errors);
    }

    [Fact]
    public async Task ParseCsv_ReportsMalformedEscapingAndEmptyFile()
    {
        var parser = new FavoriteDestinationImportParser();
        var malformed = await parser.ParseAsync("favorites.csv", Bytes("name,address,notes\r\n\"Museum,Berlin,notes\r\n"));
        var empty = await parser.ParseAsync("favorites.csv", Bytes(string.Empty));

        Assert.False(malformed.IsValid);
        Assert.False(empty.IsValid);
        Assert.NotEmpty(malformed.Errors);
        Assert.NotEmpty(empty.Errors);
    }

    private static MemoryStream Bytes(string value) => new(System.Text.Encoding.UTF8.GetBytes(value));
}