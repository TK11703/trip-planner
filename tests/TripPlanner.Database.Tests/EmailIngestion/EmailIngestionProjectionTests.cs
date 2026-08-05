using System.Text.RegularExpressions;
using TripPlanner.Database.EmailIngestion;
using TripPlanner.Database.Sql;
using Xunit;

namespace TripPlanner.Database.Tests.EmailIngestion;

/// <summary>
/// Dapper picks a record constructor whose parameters all match returned columns, so a projection
/// that drifts from the record it feeds throws at request time instead of failing the build. These
/// tests pin each statement's alias list to the shape it materializes into.
/// </summary>
public class EmailIngestionProjectionTests
{
    public static TheoryData<string, Type> Projections => new()
    {
        { "Commands/EmailIngestion/InsertInboxEmail.sql", typeof(InboxEmailRecord) },
        { "Queries/EmailIngestion/GetInboxEmailById.sql", typeof(InboxEmailRecord) },
        { "Queries/EmailIngestion/GetInboxEmails.sql", typeof(InboxEmailSummary) },
        { "Commands/EmailIngestion/InsertParsedItemDraft.sql", typeof(ParsedItemDraftRecord) },
        { "Commands/EmailIngestion/UpdateParsedItemDraft.sql", typeof(ParsedItemDraftRecord) },
        { "Queries/EmailIngestion/GetParsedItemDrafts.sql", typeof(ParsedItemDraftRecord) },
        { "Queries/EmailIngestion/GetParsedItemDraftById.sql", typeof(ParsedItemDraftRecord) },
    };

    [Theory]
    [MemberData(nameof(Projections))]
    public void EveryStatementReturnsExactlyTheColumnsItsRecordNeeds(string script, Type recordType)
    {
        var aliases = Regex.Matches(new SqlFileProvider().Get(script), @"\bAS\s+([A-Za-z_][A-Za-z0-9_]*)")
            .Select(m => m.Groups[1].Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missing = recordType.GetConstructors().Single().GetParameters()
            .Select(p => p.Name!)
            .Where(name => !aliases.Contains(name))
            .ToArray();

        Assert.True(missing.Length == 0, $"{script} does not return: {string.Join(", ", missing)}");
    }
}
