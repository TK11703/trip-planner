using System.Globalization;
using System.Text;
using System.Text.Json;
using CsvHelper;
using CsvHelper.Configuration;

namespace TripPlanner.Api.Features.FavoriteDestinations;

public sealed record FavoriteDestinationImportRecord(
    string? Name,
    string? Address,
    string? Notes);

public sealed record FavoriteDestinationImportRow(int RowNumber, FavoriteDestinationImportRecord Record);

public sealed record FavoriteDestinationImportParseError(int? RowNumber, string Message);

public sealed record FavoriteDestinationImportParseResult(
    IReadOnlyList<FavoriteDestinationImportRow> Rows,
    IReadOnlyList<FavoriteDestinationImportParseError> Errors)
{
    public bool IsValid => Errors.Count == 0;
}

public sealed class FavoriteDestinationImportParser
{
    private static readonly string[] CsvHeaders = ["name", "address", "notes"];
    private const int MaximumRows = 1000;

    public async Task<FavoriteDestinationImportParseResult> ParseAsync(
        string fileName,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        if (Path.GetExtension(fileName).Equals(".json", StringComparison.OrdinalIgnoreCase))
        {
            return await ParseJsonAsync(content, cancellationToken);
        }
        if (Path.GetExtension(fileName).Equals(".csv", StringComparison.OrdinalIgnoreCase))
        {
            return await ParseCsvAsync(content, cancellationToken);
        }

        return new FavoriteDestinationImportParseResult(
            Array.Empty<FavoriteDestinationImportRow>(),
            [new FavoriteDestinationImportParseError(null, "Choose a JSON or CSV file.")]);
    }

    private static async Task<FavoriteDestinationImportParseResult> ParseJsonAsync(Stream content, CancellationToken cancellationToken)
    {
        try
        {
            using var document = await JsonDocument.ParseAsync(content, cancellationToken: cancellationToken);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return Invalid("The JSON file must contain an array of favorite destinations.");
            }

            var rows = new List<FavoriteDestinationImportRow>();
            var errors = new List<FavoriteDestinationImportParseError>();
            var rowNumber = 0;
            foreach (var element in document.RootElement.EnumerateArray())
            {
                rowNumber++;
                if (rowNumber > MaximumRows)
                {
                    errors.Add(new FavoriteDestinationImportParseError(null, $"A file can contain at most {MaximumRows} records."));
                    break;
                }
                if (element.ValueKind != JsonValueKind.Object)
                {
                    errors.Add(new FavoriteDestinationImportParseError(rowNumber, "Each JSON array entry must be an object."));
                    continue;
                }

                var record = new FavoriteDestinationImportRecord(
                    ReadString(element, "name", rowNumber, errors),
                    ReadString(element, "address", rowNumber, errors),
                    ReadString(element, "notes", rowNumber, errors));
                rows.Add(new FavoriteDestinationImportRow(rowNumber, record));
            }
            return new FavoriteDestinationImportParseResult(rows, errors);
        }
        catch (JsonException)
        {
            return Invalid("The JSON file is malformed.");
        }
    }

    private static string? ReadString(
        JsonElement element,
        string propertyName,
        int rowNumber,
        ICollection<FavoriteDestinationImportParseError> errors)
    {
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }
        if (value.ValueKind == JsonValueKind.String)
        {
            return value.GetString();
        }

        errors.Add(new FavoriteDestinationImportParseError(rowNumber, $"'{propertyName}' must be a string."));
        return null;
    }

    private static async Task<FavoriteDestinationImportParseResult> ParseCsvAsync(Stream content, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(content, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var errors = new List<FavoriteDestinationImportParseError>();
        var rows = new List<FavoriteDestinationImportRow>();
        var configuration = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            IgnoreBlankLines = true,
            DetectColumnCountChanges = true,
            BadDataFound = args => errors.Add(new FavoriteDestinationImportParseError(args.Context.Parser.Row, "The CSV contains malformed quoted data.")),
            MissingFieldFound = null,
            HeaderValidated = null
        };

        try
        {
            using var csv = new CsvReader(reader, configuration);
            if (!await csv.ReadAsync())
            {
                return Invalid("The CSV file is empty.");
            }
            csv.ReadHeader();
            if (csv.HeaderRecord is null || !csv.HeaderRecord.SequenceEqual(CsvHeaders, StringComparer.Ordinal))
            {
                return Invalid("The CSV headers must be exactly: name,address,notes.");
            }

            while (await csv.ReadAsync())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var rowNumber = csv.Parser.Row;
                if (rows.Count >= MaximumRows)
                {
                    errors.Add(new FavoriteDestinationImportParseError(null, $"A file can contain at most {MaximumRows} records."));
                    break;
                }

                rows.Add(new FavoriteDestinationImportRow(rowNumber, new FavoriteDestinationImportRecord(
                    csv.GetField("name"),
                    csv.GetField("address"),
                    csv.GetField("notes"))));
            }
        }
        catch (CsvHelperException)
        {
            errors.Add(new FavoriteDestinationImportParseError(null, "The CSV file is malformed."));
        }
        catch (DecoderFallbackException)
        {
            errors.Add(new FavoriteDestinationImportParseError(null, "The CSV file must be valid UTF-8."));
        }

        return new FavoriteDestinationImportParseResult(rows, errors);
    }

    private static FavoriteDestinationImportParseResult Invalid(string message)
        => new(Array.Empty<FavoriteDestinationImportRow>(), [new FavoriteDestinationImportParseError(null, message)]);
}