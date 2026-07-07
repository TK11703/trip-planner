using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using TripPlanner.Api.Security;
using TripPlanner.Api.Tests.Infrastructure;
using TripPlanner.Contracts.Audit;
using TripPlanner.Contracts.Trips;
using TripPlanner.Database.Audit;
using TripPlanner.Database.TripSharing;
using TripPlanner.Api.Features.TripSharing;
using Xunit;

namespace TripPlanner.Api.Tests.TripSharing;

public class TripSharingEndpointTests : IClassFixture<TestApiFactory>
{
    private static readonly Guid TripId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private const string Stranger = "user-c-immutable-id";

    private readonly WebApplicationFactory<Program> _factory;

    public TripSharingEndpointTests(TestApiFactory factory)
    {
        _factory = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ITripAccessResolver>();
            services.RemoveAll<ITripSharingRepository>();
            services.RemoveAll<IUserDirectoryLookup>();
            services.RemoveAll<IAuditRepository>();
            // UserA owns TripId; UserB is a collaborator; everyone else has no access.
            services.AddSingleton<ITripAccessResolver>(new FakeAccessResolver(TripId, TestUsers.UserA));
            services.AddSingleton<ITripSharingRepository, FakeSharingRepository>();
            services.AddSingleton<IUserDirectoryLookup, FakeDirectoryLookup>();
            services.AddSingleton<IAuditRepository, NoopAuditRepository>();
        }));
    }

    [Fact]
    public async Task Owner_CanCreateShare()
    {
        var client = ClientFor(TestUsers.UserA);
        var response = await client.PostAsJsonAsync($"/api/trips/{TripId}/shares",
            new UpsertTripShareRequest(Stranger, "Casey", "casey@example.com", TripAccessLevel.Viewer));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var member = await response.Content.ReadFromJsonAsync<TripShareMember>();
        Assert.Equal(Stranger, member!.UserId);
        Assert.Equal(TripAccessLevel.Viewer, member.AccessLevel);
    }

    [Fact]
    public async Task Owner_CannotShareWithSelf()
    {
        var client = ClientFor(TestUsers.UserA);
        var response = await client.PostAsJsonAsync($"/api/trips/{TripId}/shares",
            new UpsertTripShareRequest(TestUsers.UserA, "Me", null, TripAccessLevel.Collaborator));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Collaborator_CannotManageShares()
    {
        var client = ClientFor(TestUsers.UserB);
        var response = await client.PostAsJsonAsync($"/api/trips/{TripId}/shares",
            new UpsertTripShareRequest(Stranger, "Casey", null, TripAccessLevel.Viewer));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Stranger_CannotManageShares()
    {
        var client = ClientFor(Stranger);
        var response = await client.PostAsJsonAsync($"/api/trips/{TripId}/shares",
            new UpsertTripShareRequest("someone", null, null, TripAccessLevel.Viewer));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Owner_CanUpdateAccessLevel()
    {
        var client = ClientFor(TestUsers.UserA);
        var response = await client.PutAsJsonAsync($"/api/trips/{TripId}/shares/{Stranger}",
            new UpdateTripShareAccessRequest(TripAccessLevel.Collaborator));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var member = await response.Content.ReadFromJsonAsync<TripShareMember>();
        Assert.Equal(TripAccessLevel.Collaborator, member!.AccessLevel);
    }

    [Fact]
    public async Task Collaborator_CannotUpdateAccessLevel()
    {
        var client = ClientFor(TestUsers.UserB);
        var response = await client.PutAsJsonAsync($"/api/trips/{TripId}/shares/{Stranger}",
            new UpdateTripShareAccessRequest(TripAccessLevel.Collaborator));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Owner_CanRemoveShare()
    {
        var client = ClientFor(TestUsers.UserA);
        var response = await client.DeleteAsync($"/api/trips/{TripId}/shares/{Stranger}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Owner_CanSearchDirectory_ExcludingSelf()
    {
        var client = ClientFor(TestUsers.UserA);
        var results = await client.GetFromJsonAsync<DirectoryUserResult[]>($"/api/trips/{TripId}/shares/directory-users?query=ca");

        Assert.NotNull(results);
        Assert.DoesNotContain(results!, r => r.UserId == TestUsers.UserA);
        Assert.Contains(results!, r => r.UserId == Stranger);
    }

    [Fact]
    public async Task DirectorySearch_TooShort_IsRejected()
    {
        var client = ClientFor(TestUsers.UserA);
        var response = await client.GetAsync($"/api/trips/{TripId}/shares/directory-users?query=a");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private HttpClient ClientFor(string userId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.TestUserHeader, userId);
        return client;
    }

    private sealed class FakeAccessResolver : ITripAccessResolver
    {
        private readonly Guid _tripId;
        private readonly string _ownerId;
        public FakeAccessResolver(Guid tripId, string ownerId) { _tripId = tripId; _ownerId = ownerId; }

        public Task<TripAccess?> ResolveAsync(string callerUserId, Guid tripId, CancellationToken ct)
        {
            if (tripId != _tripId) return Task.FromResult<TripAccess?>(null);
            if (callerUserId == _ownerId) return Task.FromResult<TripAccess?>(new TripAccess(_ownerId, TripAccessLevel.Owner));
            if (callerUserId == TestUsers.UserB) return Task.FromResult<TripAccess?>(new TripAccess(_ownerId, TripAccessLevel.Collaborator));
            return Task.FromResult<TripAccess?>(null);
        }
    }

    private sealed class FakeSharingRepository : ITripSharingRepository
    {
        public Task<TripAccess?> GetAccessAsync(string userId, string? email, Guid tripId, CancellationToken ct)
            => Task.FromResult<TripAccess?>(null);

        public Task<IReadOnlyList<TripShareMember>> GetSharesAsync(Guid tripId, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<TripShareMember>>(Array.Empty<TripShareMember>());

        public Task<TripShareMember?> UpsertShareAsync(string ownerUserId, Guid tripId, UpsertTripShareRequest request, DateTimeOffset nowUtc, CancellationToken ct)
            => Task.FromResult<TripShareMember?>(new TripShareMember(request.UserId, request.DisplayName, request.Email, request.AccessLevel, nowUtc));

        public Task<TripShareMember?> UpdateAccessAsync(string ownerUserId, Guid tripId, string memberUserId, TripAccessLevel accessLevel, DateTimeOffset nowUtc, CancellationToken ct)
            => Task.FromResult<TripShareMember?>(new TripShareMember(memberUserId, null, null, accessLevel, nowUtc));

        public Task<int> DeleteShareAsync(string ownerUserId, Guid tripId, string memberUserId, CancellationToken ct)
            => Task.FromResult(1);
    }

    private sealed class FakeDirectoryLookup : IUserDirectoryLookup
    {
        public bool IsConfigured => true;
        public Task<IReadOnlyList<DirectoryUserResult>> SearchAsync(string query, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<DirectoryUserResult>>(new[]
            {
                new DirectoryUserResult(Stranger, "Casey", "casey@example.com", "casey@example.com"),
                new DirectoryUserResult(TestUsers.UserA, "Owner", "owner@example.com", "owner@example.com")
            });
    }

    private sealed class NoopAuditRepository : IAuditRepository
    {
        public Task RecordAsync(string? userId, string operation, string resourceType, string? resourceId, string result, DateTimeOffset occurredAtUtc, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
