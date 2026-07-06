using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TripPlanner.Api.Tests.Infrastructure;
using TripPlanner.Database.UserProfiles;

namespace TripPlanner.Api.Tests.UserProfiles;

internal sealed class ProfileApiFactory : TestApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.RemoveAll<IUserProfileRepository>(services);
            services.AddSingleton<IUserProfileRepository, InMemoryUserProfileRepository>();
        });
    }
}
