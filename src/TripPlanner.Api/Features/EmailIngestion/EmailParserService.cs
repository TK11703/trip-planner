using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using TripPlanner.Database.EmailIngestion;

namespace TripPlanner.Api.Features.EmailIngestion;

/// <summary>
/// Uses Azure OpenAI chat completions to extract structured event data from a raw email body.
/// The Azure OpenAI client is resolved via <see cref="AzureOpenAIClient"/> (injected from DI),
/// which is configured with <c>DefaultAzureCredential</c> — managed identity when hosted,
/// developer credentials locally. No API key is stored in code or configuration.
/// </summary>
public sealed class EmailParserService
{
    private readonly AzureOpenAIClient _openAi;
    private readonly IConfiguration _config;
    private readonly ILogger<EmailParserService> _logger;

    private const double ConfidenceThreshold = 0.5;

    public EmailParserService(AzureOpenAIClient openAi, IConfiguration config, ILogger<EmailParserService> logger)
    {
        _openAi = openAi;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Attempts to parse <paramref name="email"/> into a <see cref="NewParsedEventDraft"/>.
    /// Returns <c>(draft, ParseStatus.Parsed)</c> on success,
    /// <c>(null, ParseStatus.Unsupported)</c> when confidence is below the threshold, and
    /// <c>(null, ParseStatus.Failed)</c> when OpenAI is unavailable or returns unparseable JSON.
    /// </summary>
    public async Task<(NewParsedEventDraft? Draft, string ParseStatus)> ParseAsync(
        InboxEmailRecord email, CancellationToken ct = default)
    {
        var deploymentName = _config["AzureOpenAI:DeploymentName"] ?? "gpt-4o";

        var systemPrompt =
            "You are a travel assistant that extracts structured booking information from email text. " +
            "Return ONLY a JSON object with these fields (omit fields you cannot determine): " +
            "eventType (string: 'flight'|'hotel'|'car_rental'|'activity'|'other'), " +
            "title (string), location (string), " +
            "startLocal (ISO-8601 datetime without timezone, e.g. '2026-07-15T09:30:00'), " +
            "startTimeZoneId (IANA tz id), " +
            "endLocal (ISO-8601 datetime without timezone), " +
            "endTimeZoneId (IANA tz id), " +
            "confirmationCode (string), notes (string), " +
            "confidence (number 0.0–1.0 reflecting how certain you are). " +
            "Return only valid JSON with no markdown fences.";

        var userMessage = $"Subject: {email.Subject}\n\n{email.BodyText}";

        try
        {
            var chatClient = _openAi.GetChatClient(deploymentName);
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(systemPrompt),
                new UserChatMessage(userMessage)
            };

            var response = await chatClient.CompleteChatAsync(messages, cancellationToken: ct);
            var content = response.Value.Content.FirstOrDefault()?.Text ?? string.Empty;

            var parsed = JsonSerializer.Deserialize<ParsedEmailResult>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });

            if (parsed is null || parsed.Confidence < ConfidenceThreshold)
            {
                _logger.LogInformation("Email {InboxEmailId} parsed with low confidence ({Confidence:F2}); marking unsupported.", email.InboxEmailId, parsed?.Confidence ?? 0);
                return (null, "unsupported");
            }

            var draft = new NewParsedEventDraft(
                InboxEmailId: email.InboxEmailId,
                UserId: email.UserId,
                TripId: null,
                TripLegId: null,
                EventType: parsed.EventType,
                Title: parsed.Title,
                Location: parsed.Location,
                StartLocal: ParseDateTime(parsed.StartLocal),
                StartTimeZoneId: parsed.StartTimeZoneId,
                EndLocal: ParseDateTime(parsed.EndLocal),
                EndTimeZoneId: parsed.EndTimeZoneId,
                ConfirmationCode: parsed.ConfirmationCode,
                Notes: parsed.Notes,
                Confidence: parsed.Confidence);

            return (draft, "parsed");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse email {InboxEmailId} via Azure OpenAI.", email.InboxEmailId);
            return (null, "failed");
        }
    }

    private static DateTime? ParseDateTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return DateTime.TryParse(value, out var dt) ? dt : null;
    }

    private sealed class ParsedEmailResult
    {
        public string? EventType { get; set; }
        public string? Title { get; set; }
        public string? Location { get; set; }
        public string? StartLocal { get; set; }
        public string? StartTimeZoneId { get; set; }
        public string? EndLocal { get; set; }
        public string? EndTimeZoneId { get; set; }
        public string? ConfirmationCode { get; set; }
        public string? Notes { get; set; }
        public double Confidence { get; set; }
    }
}
