using System.Net.Http.Headers;
using System.Text.Json;
using Azure.Core;
using Azure.Identity;
using TripPlanner.Contracts.Trips;

namespace TripPlanner.Api.Features.TripSharing;

/// <summary>
/// Looks up users from the Azure/Entra tenant so an owner can pick people to share a trip with.
/// Uses Microsoft Graph via an access token acquired with <see cref="TokenCredential"/>
/// (managed identity when hosted, developer credentials locally) so no secrets are embedded.
/// Requests only the fields needed for sharing and degrades to an empty result when not configured.
/// </summary>
public interface IUserDirectoryLookup
{
    bool IsConfigured { get; }

    /// <exception cref="DirectoryLookupException">The directory could not be reached or rejected the request.</exception>
    Task<IReadOnlyList<DirectoryUserResult>> SearchAsync(string query, CancellationToken ct);
}

/// <summary>
/// Raised when the directory itself failed, so callers can tell that apart from "no one matched".
/// The message is safe to show to a signed-in owner; diagnostic detail stays in the logs.
/// </summary>
public sealed class DirectoryLookupException : Exception
{
    public DirectoryLookupException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

public sealed partial class GraphUserDirectoryLookup : IUserDirectoryLookup
{
    public const string HttpClientName = "graph";
    private static readonly string[] GraphScopes = ["https://graph.microsoft.com/.default"];
    private static readonly string[] SearchFields = ["displayName", "givenName", "surname", "mail", "userPrincipalName"];

    private readonly IHttpClientFactory _httpFactory;
    private readonly TokenCredential _credential;
    private readonly ILogger<GraphUserDirectoryLookup> _logger;
    private readonly bool _enabled;

    public GraphUserDirectoryLookup(
        IHttpClientFactory httpFactory,
        TokenCredential credential,
        IConfiguration configuration,
        ILogger<GraphUserDirectoryLookup> logger)
    {
        _httpFactory = httpFactory;
        _credential = credential;
        _logger = logger;
        _enabled = configuration.GetValue("AzureEntra:DirectoryLookupEnabled", false);
    }

    public bool IsConfigured => _enabled;

    public async Task<IReadOnlyList<DirectoryUserResult>> SearchAsync(string query, CancellationToken ct)
    {
        if (!_enabled || string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<DirectoryUserResult>();
        }

        try
        {
            var token = await _credential.GetTokenAsync(new TokenRequestContext(GraphScopes), ct);
            var http = _httpFactory.CreateClient(HttpClientName);

            using var request = new HttpRequestMessage(HttpMethod.Get, BuildSearchRequestUri(query));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            // $search over directory objects is an advanced query and is rejected without this header.
            request.Headers.Add("ConsistencyLevel", "eventual");

            using var response = await http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                var statusCode = (int)response.StatusCode;
                LogLookupFailed(statusCode, body);
                throw new DirectoryLookupException(DescribeFailure(statusCode));
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            if (!document.RootElement.TryGetProperty("value", out var value) || value.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<DirectoryUserResult>();
            }

            var results = new List<DirectoryUserResult>(value.GetArrayLength());
            foreach (var user in value.EnumerateArray())
            {
                var id = GetString(user, "id");
                if (string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }
                results.Add(new DirectoryUserResult(
                    id,
                    GetString(user, "displayName"),
                    GetString(user, "mail"),
                    GetString(user, "userPrincipalName")));
            }
            return results;
        }
        catch (AuthenticationFailedException ex)
        {
            LogTokenAcquisitionFailed(ex);
            throw new DirectoryLookupException("Directory search is unavailable because the app could not sign in to Microsoft Graph. Check the server logs for details.", ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not DirectoryLookupException)
        {
            LogLookupError(ex);
            throw new DirectoryLookupException("Directory search is unavailable right now. Check the server logs for details.", ex);
        }
    }

    private static string DescribeFailure(int statusCode) => statusCode switch
    {
        401 or 403 => $"Directory search was denied by Microsoft Graph ({statusCode}). The app registration or managed identity needs the User.ReadBasic.All application permission with admin consent.",
        429 => "Directory search is being throttled by Microsoft Graph. Try again in a moment.",
        _ => $"Directory search failed because Microsoft Graph returned {statusCode}. Check the server logs for details."
    };

    /// <summary>
    /// Builds the Graph users query. <c>$search</c> is used rather than <c>startswith</c> filtering because
    /// it tokenizes <c>displayName</c>, so a surname matches a "First Last" display name; the remaining
    /// fields fall back to prefix matching. Requires the <c>ConsistencyLevel: eventual</c> header.
    /// </summary>
    public static string BuildSearchRequestUri(string query)
    {
        // Each clause is wrapped in double quotes, so quotes and backslashes in the term must be escaped.
        var term = query.Trim().Replace("\\", "\\\\").Replace("\"", "\\\"");
        var search = string.Join(" OR ", SearchFields.Select(field => $"\"{field}:{term}\""));
        return $"v1.0/users?$select=id,displayName,mail,userPrincipalName&$top=10&$search={Uri.EscapeDataString(search)}";
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Directory lookup returned {StatusCode}. A 403 usually means the app is missing the Microsoft Graph application permission User.ReadBasic.All with admin consent. Graph response: {Body}")]
    private partial void LogLookupFailed(int statusCode, string body);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Directory lookup could not acquire a Microsoft Graph token. Configure AzureEntra:ClientSecret (with the User.ReadBasic.All application permission, admin-consented) or sign in locally (for example `az login`).")]
    private partial void LogTokenAcquisitionFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Directory lookup failed for a share search.")]
    private partial void LogLookupError(Exception exception);

    private static string? GetString(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
