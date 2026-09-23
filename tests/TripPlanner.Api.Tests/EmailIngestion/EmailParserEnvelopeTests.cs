using System.Collections;
using System.Reflection;
using TripPlanner.Api.Features.EmailIngestion;

namespace TripPlanner.Api.Tests.EmailIngestion;

/// <summary>
/// The language model is told to return <c>{"items":[{"itemType": ...}]}</c>. Nothing enforces
/// that at runtime: if the prompt and the envelope type ever disagree, deserialization succeeds
/// and silently yields an empty list, so every message would be reported as unsupported with no
/// exception anywhere. These tests pin the wire shape the prompt asks for.
/// </summary>
public sealed class EmailParserEnvelopeTests
{
    private static readonly MethodInfo DeserializeMethod =
        typeof(EmailParserService).GetMethod("Deserialize", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("EmailParserService.Deserialize was renamed or removed.");

    private static IList Deserialize(string json)
        => (IList?)DeserializeMethod.Invoke(null, [json])
           ?? throw new InvalidOperationException("Deserialization returned null for a well-formed payload.");

    private static string? ItemTypeOf(object recognized)
        => (string?)recognized.GetType().GetProperty("ItemType")!.GetValue(recognized);

    [Fact]
    public void TheEnvelopeTheModelIsAskedForDeserializesIntoRecognizedItems()
    {
        var payload = """
            {"items":[{"itemType":"hotel","title":"Contoso Suites","confidence":0.91}]}
            """;

        var recognized = Deserialize(payload);

        Assert.Single(recognized);
        Assert.Equal("hotel", ItemTypeOf(recognized[0]!));
    }

    [Fact]
    public void ABareArrayIsStillAccepted()
    {
        var payload = """
            [{"itemType":"flight","title":"Flight ABC123","confidence":0.88}]
            """;

        var recognized = Deserialize(payload);

        Assert.Single(recognized);
        Assert.Equal("flight", ItemTypeOf(recognized[0]!));
    }

    [Fact]
    public void AnEmptyItemsArrayYieldsNoDraftsRatherThanAFailure()
    {
        var recognized = Deserialize("""{"items":[]}""");

        Assert.Empty(recognized);
    }
}

/// <summary>
/// A zone the model returns is only useful if <c>TimezoneOptions</c> can resolve it — the draft
/// editor preselects from that list and <c>DraftPlacementMatcher</c> needs it to build an instant.
/// An unresolvable id would reach the traveler looking populated while behaving as if absent.
/// </summary>
public sealed class EmailParserTimeZoneNormalizationTests
{
    private static readonly MethodInfo NormalizeMethod =
        typeof(EmailParserService).GetMethod("NormalizeTimeZoneId", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("EmailParserService.NormalizeTimeZoneId was renamed or removed.");

    private static string? Normalize(string? value) => (string?)NormalizeMethod.Invoke(null, [value]);

    [Theory]
    [InlineData("Europe/London")]
    [InlineData("America/Denver")]
    [InlineData("UTC")]
    public void AnIanaZoneIsKept(string id) => Assert.Equal(id, Normalize(id));

    [Fact]
    public void SurroundingWhitespaceDoesNotDiscardAnOtherwiseValidZone()
        => Assert.Equal("Europe/London", Normalize("  Europe/London  "));

    [Fact]
    public void AWindowsZoneIdIsConvertedRatherThanDiscarded()
        => Assert.Equal("Europe/London", Normalize("GMT Standard Time"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("BST")]
    [InlineData("UTC+1")]
    [InlineData("Middle/Earth")]
    public void AnythingUnresolvableBecomesNullSoTheTravelerIsAsked(string? id)
        => Assert.Null(Normalize(id));
}
