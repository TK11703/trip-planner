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
    private static readonly string[] NativeCsvHeaders = ["name", "address", "notes"];
    private static readonly string[] GoogleCsvHeaders = ["Title", "Note", "URL", "Tags", "Comment"];
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
            var csvFormat = GetCsvFormat(csv.HeaderRecord);
            if (csvFormat is null)
            {
                return Invalid("The CSV headers must be exactly either: name,address,notes or Title,Note,URL,Tags,Comment.");
            }

            while (await csv.ReadAsync())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var rowNumber = csv.Parser.Row;
                var record = ReadCsvRecord(csv, csvFormat.Value);
                if (csvFormat == CsvFormat.Google && IsEmptyGoogleRow(csv))
                {
                    continue;
                }
                if (rows.Count >= MaximumRows)
                {
                    errors.Add(new FavoriteDestinationImportParseError(null, $"A file can contain at most {MaximumRows} records."));
                    break;
                }

                rows.Add(new FavoriteDestinationImportRow(rowNumber, record));
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

    private static CsvFormat? GetCsvFormat(string[]? headers)
    {
        if (headers?.SequenceEqual(NativeCsvHeaders, StringComparer.Ordinal) == true)
        {
            return CsvFormat.Native;
        }
        if (headers?.SequenceEqual(GoogleCsvHeaders, StringComparer.Ordinal) == true)
        {
            return CsvFormat.Google;
        }

        return null;
    }

    private static FavoriteDestinationImportRecord ReadCsvRecord(CsvReader csv, CsvFormat format)
        => format switch
        {
            CsvFormat.Native => new FavoriteDestinationImportRecord(
                csv.GetField("name"),
                csv.GetField("address"),
                csv.GetField("notes")),
            CsvFormat.Google => new FavoriteDestinationImportRecord(
                csv.GetField("Title"),
                csv.GetField("URL"),
                CombineNotes(csv.GetField("Note"), csv.GetField("Comment"))),
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };

    private static string? CombineNotes(params string?[] values)
    {
        var notes = values.Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
        return notes.Length == 0 ? null : string.Join(Environment.NewLine, notes);
    }

    private static bool IsEmptyGoogleRow(CsvReader csv)
        => GoogleCsvHeaders.All(header => string.IsNullOrWhiteSpace(csv.GetField(header)));

    private static FavoriteDestinationImportParseResult Invalid(string message)
        => new(Array.Empty<FavoriteDestinationImportRow>(), [new FavoriteDestinationImportParseError(null, message)]);

    private enum CsvFormat
    {
        Native,
        Google
    }
}