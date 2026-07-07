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
    Task<IReadOnlyList<DirectoryUserResult>> SearchAsync(string query, CancellationToken ct);
}

public sealed class GraphUserDirectoryLookup : IUserDirectoryLookup
{
    public const string HttpClientName = "graph";
    private static readonly string[] GraphScopes = ["https://graph.microsoft.com/.default"];

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

            // Escape single quotes for OData and URL-encode the term.
            var term = Uri.EscapeDataString(query.Trim().Replace("'", "''"));
            var filter = $"startswith(displayName,'{term}') or startswith(mail,'{term}') or startswith(userPrincipalName,'{term}')";
            var requestUri = $"v1.0/users?$select=id,displayName,mail,userPrincipalName&$top=10&$filter={Uri.EscapeDataString(filter)}";

            using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning(
                    "Directory lookup returned {StatusCode}. A 403 usually means the app is missing the Microsoft Graph application permission User.ReadBasic.All with admin consent. Graph response: {Body}",
                    (int)response.StatusCode,
                    body);
                return Array.Empty<DirectoryUserResult>();
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
            _logger.LogWarning(
                ex,
                "Directory lookup could not acquire a Microsoft Graph token. Configure AzureEntra:ClientSecret (with the User.ReadBasic.All application permission, admin-consented) or sign in locally (for example `az login`).");
            return Array.Empty<DirectoryUserResult>();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Directory lookup failed for a share search.");
            return Array.Empty<DirectoryUserResult>();
        }
    }

    private static string? GetString(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
