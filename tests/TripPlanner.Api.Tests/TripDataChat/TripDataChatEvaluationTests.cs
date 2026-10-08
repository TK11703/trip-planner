using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using TripPlanner.Api.Features.TripDataChat;
using TripPlanner.Database.TripDataChat;
using Xunit;

namespace TripPlanner.Api.Tests.TripDataChat;

public sealed class TripChatEvaluationFactAttribute : FactAttribute
{
    public TripChatEvaluationFactAttribute()
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<TripDataChatAgent>(optional: true)
            .AddEnvironmentVariables()
            .Build();
        if (string.IsNullOrWhiteSpace(configuration["TripChat:Endpoint"])
            || string.IsNullOrWhiteSpace(configuration["TripChat:ChatDeploymentName"])
            || string.IsNullOrWhiteSpace(configuration["TripChat:EmbeddingDeploymentName"]))
        {
            Skip = "Requires TripChat endpoint and chat/embedding deployments in API user-secrets or environment.";
        }
    }
}

public sealed class TripDataChatEvaluationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [TripChatEvaluationFact]
    [Trait("Category", "LiveModelEvaluation")]
    public async Task ControlledCasesMeetGroundingAndCitationExpectations()
    {
        // Mirror the AppHost so other signed-in tools (e.g., Visual Studio in another tenant) are not used.
        Environment.SetEnvironmentVariable("AZURE_TOKEN_CREDENTIALS",
            Environment.GetEnvironmentVariable("AZURE_TOKEN_CREDENTIALS") ?? "AzureCliCredential");
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<TripDataChatAgent>(optional: true)
            .AddEnvironmentVariables()
            .Build();
        var path = Path.Combine(AppContext.BaseDirectory, "TripDataChat", "TestData", "chat-evaluation-cases.json");
        var dataset = JsonSerializer.Deserialize<EvaluationDataset>(
            await File.ReadAllTextAsync(path), JsonOptions)!;
        var agent = new TripDataChatAgent(configuration);
        var latencySamples = new List<double>();

        foreach (var evaluationCase in dataset.Cases)
        {
            var sources = evaluationCase.Sources;
            var citationKeys = sources.Select((source, index) =>
                new KeyValuePair<(Guid TripId, string SourceKind, Guid SourceId), string>(
                    (source.TripId, source.SourceKind, source.SourceId), $"source-{index + 1}"))
                .ToDictionary(pair => pair.Key, pair => pair.Value);

            var stopwatch = Stopwatch.StartNew();
            await agent.EmbedAsync(string.Join('\n', evaluationCase.PriorUserMessages.Append(evaluationCase.Question)), default);
            var generated = await agent.GenerateAsync(
                evaluationCase.Question,
                evaluationCase.PriorUserMessages,
                sources,
                citationKeys,
                new TripDataChatDateContext(DateOnly.FromDateTime(DateTime.UtcNow), "UTC"),
                default);
            stopwatch.Stop();
            latencySamples.Add(stopwatch.Elapsed.TotalMilliseconds);

            Assert.NotNull(generated);
            Assert.True(
                evaluationCase.ExpectedAnswerContainsAny.Any(expected =>
                    generated!.Answer.Contains(expected, StringComparison.OrdinalIgnoreCase)),
                $"Case '{evaluationCase.Id}' answer did not contain any expected text: {generated!.Answer}");
            Assert.True(
                evaluationCase.ExpectedCitationCount == generated!.CitationKeys.Count,
                $"Case '{evaluationCase.Id}' expected {evaluationCase.ExpectedCitationCount} citations but got {generated.CitationKeys.Count}.");
            Assert.All(generated.CitationKeys, key => Assert.Contains(key, citationKeys.Values));
            if (evaluationCase.ExpectedNoLeakSentinel is { } sentinel)
            {
                Assert.DoesNotContain(sentinel, generated.Answer, StringComparison.Ordinal);
                Assert.DoesNotContain(sentinel, string.Join(' ', sources.Select(source => source.SearchText)), StringComparison.Ordinal);
            }
        }

        Assert.Equal(dataset.Cases.Count, latencySamples.Count);
        Assert.Contains(dataset.OperationalScenarios, scenario => scenario.Category == "throttling" && scenario.ExpectedHttpStatus == 429);
        Assert.Contains(dataset.OperationalScenarios, scenario => scenario.Category == "provider-failure" && scenario.ExpectedHttpStatus == 503);
    }

    private sealed record EvaluationDataset(
        int Version,
        IReadOnlyList<EvaluationCase> Cases,
        IReadOnlyList<OperationalScenario> OperationalScenarios);

    private sealed record EvaluationCase(
        string Id,
        string Category,
        string Question,
        IReadOnlyList<string> PriorUserMessages,
        IReadOnlyList<TripSearchSource> Sources,
        IReadOnlyList<string> ExpectedAnswerContainsAny,
        int ExpectedCitationCount,
        string? ExpectedNoLeakSentinel);

    private sealed record OperationalScenario(string Category, int? ExpectedHttpStatus, int? TargetP95Milliseconds);
}