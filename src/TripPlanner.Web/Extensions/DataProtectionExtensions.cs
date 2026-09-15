using Azure.Identity;
using Microsoft.AspNetCore.DataProtection;

namespace TripPlanner.Web.Extensions;

public static class DataProtectionExtensions
{
    /// <summary>
    /// Persists the ASP.NET data-protection key ring outside the container.
    /// </summary>
    /// <remarks>
    /// Container Apps replicas are ephemeral and scale from zero, so the default
    /// file-system key ring would be lost on every revision and would differ between
    /// replicas — invalidating auth cookies and antiforgery tokens. Keys are written to the
    /// <c>dataprotection</c> blob container and encrypted with a Key Vault key so the blob
    /// alone is not enough to read them.
    ///
    /// When the storage URI is not configured (local development, tests) the default
    /// in-container key ring is left in place.
    /// </remarks>
    public static WebApplicationBuilder AddTripPlannerDataProtection(this WebApplicationBuilder builder)
    {
        var blobUri = builder.Configuration["DataProtection:BlobUri"];
        if (string.IsNullOrWhiteSpace(blobUri))
        {
            return builder;
        }

        // User-assigned identity in Container Apps; developer sign-in elsewhere.
        var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
        {
            ManagedIdentityClientId = builder.Configuration["AZURE_CLIENT_ID"]
        });

        var dataProtection = builder.Services
            .AddDataProtection()
            .SetApplicationName("TripPlanner")
            .PersistKeysToAzureBlobStorage(new Uri(blobUri), credential);

        var keyVaultKeyUri = builder.Configuration["DataProtection:KeyVaultKeyUri"];
        if (!string.IsNullOrWhiteSpace(keyVaultKeyUri))
        {
            dataProtection.ProtectKeysWithAzureKeyVault(new Uri(keyVaultKeyUri), credential);
        }

        return builder;
    }
}
