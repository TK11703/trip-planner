using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using TripPlanner.Web.Features.Authentication;

namespace TripPlanner.Web.Components;

/// <summary>
/// Base for components that call the Trip Planner API on the signed-in user's behalf.
/// </summary>
public abstract class ApiComponentBase : ComponentBase
{
    [Inject] protected IServiceProvider Services { get; set; } = default!;

    /// <summary>
    /// <c>true</c> once a call has handed the browser back to Entra, so the component can keep
    /// showing a neutral state instead of flashing an empty or "not found" view before it leaves.
    /// </summary>
    protected bool IsReauthenticating { get; private set; }

    /// <summary>
    /// Runs an API call, routing a lost or stale access token back through Entra instead of
    /// showing the traveler a protocol error they cannot act on.
    /// </summary>
    /// <param name="operation">The API call.</param>
    /// <param name="onError">Receives a message to display when the call genuinely failed.</param>
    protected async Task CallApiAsync(Func<Task> operation, Action<string> onError)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(onError);

        try
        {
            await operation();
        }
        catch (OperationCanceledException)
        {
            // The component went away mid-request, so there is no longer anyone to tell.
        }
        catch (Exception ex)
        {
            // Resolved per call rather than injected, so component tests that never provoke a
            // challenge do not have to register the Entra consent stack in order to render.
            if (Services.GetService<IApiChallengeHandler>()?.TryHandle(ex) == true)
            {
                IsReauthenticating = true;
            }
            else
            {
                onError(ex.Message);
            }
        }
    }
}
