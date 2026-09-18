using Microsoft.Identity.Client;
using Microsoft.Identity.Web;

namespace TripPlanner.Web.Features.Authentication;

/// <summary>
/// Turns an Entra "I need the user back" signal into a sign-in redirect.
/// </summary>
public interface IApiChallengeHandler
{
    /// <summary>
    /// Returns <c>true</c> when the exception was a challenge and the user is being redirected,
    /// so the caller knows not to render it as an error.
    /// </summary>
    bool TryHandle(Exception exception);
}

public sealed class MicrosoftIdentityApiChallengeHandler : IApiChallengeHandler
{
    private readonly MicrosoftIdentityConsentAndConditionalAccessHandler _consentHandler;
    private readonly string[] _apiScopes;

    public MicrosoftIdentityApiChallengeHandler(
        MicrosoftIdentityConsentAndConditionalAccessHandler consentHandler,
        IConfiguration configuration)
    {
        _consentHandler = consentHandler;
        _apiScopes = configuration.GetSection("AzureEntra:ApiScopes").Get<string[]>() ?? [];
    }

    public bool TryHandle(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var challenge = AsChallenge(exception);
        if (challenge is null)
        {
            return false;
        }

        try
        {
            // Redirects the browser back through Entra. The user normally sees nothing, because
            // the SSO session is still live and only the app's own token cache was lost.
            _consentHandler.HandleException(challenge);
            return true;
        }
        catch (Exception rethrown) when (ReferenceEquals(rethrown, challenge))
        {
            // HandleException rethrows rather than returning false when re-signing the user in
            // would not fix the challenge. Report it unhandled so the caller shows a message
            // instead of letting it tear down the circuit.
            return false;
        }
    }

    /// <summary>
    /// Normalizes to the one exception shape the consent handler acts on.
    /// </summary>
    private MicrosoftIdentityWebChallengeUserException? AsChallenge(Exception exception)
    {
        for (Exception? candidate = exception; candidate is not null; candidate = candidate.InnerException)
        {
            if (candidate is MicrosoftIdentityWebChallengeUserException challenge)
            {
                return challenge;
            }

            // A bare MsalUiRequiredException carries the same signal but the consent handler only
            // unwraps the challenge type, so wrap it rather than let it through unhandled.
            if (candidate is MsalUiRequiredException msal)
            {
                return new MicrosoftIdentityWebChallengeUserException(msal, _apiScopes);
            }
        }

        return null;
    }
}
