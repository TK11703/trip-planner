using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;
using TripPlanner.Api.Features.EmailIngestion;
using TripPlanner.Contracts.Errors;

namespace TripPlanner.Api.Extensions;

public static partial class AuthenticationExtensions
{
    public const string AuthenticatedUserPolicy = "AuthenticatedUser";

    public static WebApplicationBuilder AddTripPlannerAuthentication(this WebApplicationBuilder builder)
    {
        var requiredScopes = GetConfiguredScopeAliases(builder.Configuration).ToArray();

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddMicrosoftIdentityWebApi(builder.Configuration, "AzureEntra");

        builder.Services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            options.IncludeErrorDetails = true;
            options.MapInboundClaims = false;
            options.TokenValidationParameters.NameClaimType = "name";
            options.TokenValidationParameters.RoleClaimType = "roles";
            options.Events ??= new JwtBearerEvents();
            options.Events.OnAuthenticationFailed = context =>
            {
                var logger = context.HttpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("TripPlanner.Api.JwtBearer");
                LogAuthenticationFailed(logger, context.Exception, context.Exception?.Message);
                return Task.CompletedTask;
            };
            options.Events.OnChallenge = async context =>
            {
                var logger = context.HttpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("TripPlanner.Api.JwtBearer");
                LogChallenge(logger, context.Error, context.ErrorDescription, context.AuthenticateFailure?.Message);
                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(ApiError.AuthenticationRequired(), context.HttpContext.RequestAborted);
            };
            options.Events.OnForbidden = async context =>
            {
                var logger = context.HttpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("TripPlanner.Api.JwtBearer");
                LogForbidden(
                    logger,
                    context.HttpContext.Request.Path,
                    string.Join(" ", context.Principal?.FindAll("scp").Concat(context.Principal.FindAll("scope")).Select(c => c.Value) ?? Array.Empty<string>()));
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(ApiError.ReauthenticationRequired(), context.HttpContext.RequestAborted);
            };
        });

        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthenticatedUserPolicy, policy =>
            {
                policy.RequireAuthenticatedUser();
                if (requiredScopes.Length > 0)
                {
                    policy.RequireAssertion(context => HasAnyRequiredScope(context.User, requiredScopes));
                }
            });
            options.DefaultPolicy = options.GetPolicy(AuthenticatedUserPolicy)!;
            options.AddEmailIngestionPolicies();
        });

        return builder;
    }

    private static IEnumerable<string> GetConfiguredScopeAliases(ConfigurationManager configuration)
    {
        var values = configuration.GetSection("AzureEntra:RequiredScopes").Get<string[]>()
            ?? configuration.GetSection("AzureEntra:ApiScopes").Get<string[]>()
            ?? [];

        foreach (var value in values.Where(v => !string.IsNullOrWhiteSpace(v)))
        {
            yield return value;
            var slash = value.LastIndexOf('/');
            if (slash >= 0 && slash < value.Length - 1)
            {
                yield return value[(slash + 1)..];
            }
        }
    }

    private static bool HasAnyRequiredScope(ClaimsPrincipal user, IReadOnlyCollection<string> requiredScopes)
    {
        var grantedScopes = user.FindAll("scp")
            .Concat(user.FindAll("scope"))
            .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        return grantedScopes.Any(scope => requiredScopes.Contains(scope));
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "JWT authentication failed: {Message}")]
    private static partial void LogAuthenticationFailed(ILogger logger, Exception? exception, string? message);

    [LoggerMessage(Level = LogLevel.Warning, Message = "JWT challenge: error={Error}, description={Description}, failure={Failure}")]
    private static partial void LogChallenge(ILogger logger, string? error, string? description, string? failure);

    [LoggerMessage(Level = LogLevel.Warning, Message = "JWT forbidden for {Path}. Token had scopes: {Scopes}")]
    private static partial void LogForbidden(ILogger logger, string path, string scopes);
}
