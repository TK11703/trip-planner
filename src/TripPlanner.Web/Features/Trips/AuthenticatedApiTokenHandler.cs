using System.Net.Http.Headers;
using Microsoft.Identity.Web;

namespace TripPlanner.Web.Features.Trips;

public interface ITripPlannerApiTokenProvider
{
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken);
}

public sealed class MicrosoftIdentityTripPlannerApiTokenProvider : ITripPlannerApiTokenProvider
{
    private readonly ITokenAcquisition _tokenAcquisition;
    private readonly IConfiguration _configuration;

    public MicrosoftIdentityTripPlannerApiTokenProvider(ITokenAcquisition tokenAcquisition, IConfiguration configuration)
    {
        _tokenAcquisition = tokenAcquisition;
        _configuration = configuration;
    }

    public Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        var scopes = _configuration.GetSection("AzureEntra:ApiScopes").Get<string[]>()
            ?? throw new InvalidOperationException("Trip Planner API scopes are not configured.");

        if (scopes.Length == 0 || scopes.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidOperationException("Trip Planner API scopes are not configured.");
        }

        return _tokenAcquisition.GetAccessTokenForUserAsync(scopes);
    }
}

public sealed class AuthenticatedApiTokenHandler : DelegatingHandler
{
    private readonly ITripPlannerApiTokenProvider _tokenProvider;

    public AuthenticatedApiTokenHandler(ITripPlannerApiTokenProvider tokenProvider)
    {
        _tokenProvider = tokenProvider;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var accessToken = await _tokenProvider.GetAccessTokenAsync(cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await base.SendAsync(request, cancellationToken);
    }
}
