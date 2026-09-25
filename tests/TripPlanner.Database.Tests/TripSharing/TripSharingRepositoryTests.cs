using TripPlanner.Contracts.Trips;
using TripPlanner.Database.Sql;
using TripPlanner.Database.Tests.Infrastructure;
using TripPlanner.Database.TripSharing;
using TripPlanner.Database.Trips;
using Xunit;

namespace TripPlanner.Database.Tests.TripSharing;

/// <summary>
/// Sharing decides who may see and change someone else's itinerary, so the rules live in SQL
/// where they cannot be bypassed: only the owner may write a share, a member appears once
/// regardless of how many times they are invited, and access resolves by id or by the email the
/// invitation was sent to.
/// </summary>
[Trait("Category", "DatabaseIntegration")]
[Collection(SharedPostgres.Name)]
public class TripSharingRepositoryTests
{
    private readonly TripSharingRepository _sharing;
    private readonly TripCommandRepository _commands;
    private readonly TripReadRepository _reads;

    public TripSharingRepositoryTests(PostgresFixture fixture)
    {
        var factory = new StubConnectionFactory(fixture.ConnectionString);
        var sql = new SqlFileProvider();
        _sharing = new TripSharingRepository(factory, sql);
        _commands = new TripCommandRepository(factory, sql);
        _reads = new TripReadRepository(factory, sql);
    }

    private static string NewUser(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    /// <summary>
    /// The share list deliberately includes the owner first — the dialog shows everyone with
    /// access, not just the invitations. These assertions are about the invited members, so the
    /// owner row is filtered out rather than counted.
    /// </summary>
    private async Task<IReadOnlyList<TripShareMember>> GetMembersAsync(Guid tripId)
        => (await _sharing.GetSharesAsync(tripId, default))
            .Where(s => s.AccessLevel != TripAccessLevel.Owner)
            .ToArray();

    private Task<Guid> SeedTripAsync(string owner, string name = "Shared trip") =>
        _commands.InsertAsync(
            owner,
            new CreateTripRequest(name, null, new DateOnly(2026, 7, 10), new DateOnly(2026, 7, 20)),
            DateTimeOffset.UtcNow,
            default);

    [Fact]
    public async Task UpsertShare_PersistsOneAccessLevelPerMember()
    {
        var owner = NewUser("owner");
        var member = NewUser("member");
        var tripId = await SeedTripAsync(owner);

        var share = await _sharing.UpsertShareAsync(
            owner, tripId,
            new UpsertTripShareRequest(member, "Member Name", "member@contoso.com", TripAccessLevel.Viewer),
            DateTimeOffset.UtcNow, default);

        Assert.NotNull(share);
        Assert.Equal(member, share!.UserId);
        Assert.Equal(TripAccessLevel.Viewer, share.AccessLevel);

        Assert.Single(await GetMembersAsync(tripId));
    }

    [Fact]
    public async Task UpsertShare_SecondCall_UpdatesAccessLevelInsteadOfDuplicating()
    {
        var owner = NewUser("owner");
        var member = NewUser("member");
        var tripId = await SeedTripAsync(owner);

        await _sharing.UpsertShareAsync(
            owner, tripId,
            new UpsertTripShareRequest(member, "Member", "member@contoso.com", TripAccessLevel.Viewer),
            DateTimeOffset.UtcNow, default);

        // Re-inviting the same person is a change of mind about their access, not a second seat.
        await _sharing.UpsertShareAsync(
            owner, tripId,
            new UpsertTripShareRequest(member, "Member", "member@contoso.com", TripAccessLevel.Collaborator),
            DateTimeOffset.UtcNow.AddMinutes(1), default);

        var only = Assert.Single(await GetMembersAsync(tripId));
        Assert.Equal(TripAccessLevel.Collaborator, only.AccessLevel);
    }

    /// <summary>Only the owner may hand out access; anyone else writing is a no-op, not an error.</summary>
    [Fact]
    public async Task UpsertShare_FromSomeoneOtherThanTheOwner_WritesNothing()
    {
        var owner = NewUser("owner");
        var tripId = await SeedTripAsync(owner);

        var result = await _sharing.UpsertShareAsync(
            NewUser("impostor"), tripId,
            new UpsertTripShareRequest(NewUser("member"), "M", "m@contoso.com", TripAccessLevel.Collaborator),
            DateTimeOffset.UtcNow, default);

        Assert.Null(result);
        Assert.Empty(await GetMembersAsync(tripId));
    }

    [Fact]
    public async Task GetAccess_ReturnsOwnerForTripOwner_AndShareLevelForMember()
    {
        var owner = NewUser("owner");
        var member = NewUser("member");
        var stranger = NewUser("stranger");
        var tripId = await SeedTripAsync(owner);

        await _sharing.UpsertShareAsync(
            owner, tripId,
            new UpsertTripShareRequest(member, "Member", "member@contoso.com", TripAccessLevel.Collaborator),
            DateTimeOffset.UtcNow, default);

        var ownerAccess = await _sharing.GetAccessAsync(owner, null, tripId, default);
        Assert.Equal(TripAccessLevel.Owner, ownerAccess!.AccessLevel);
        Assert.Equal(owner, ownerAccess.OwnerUserId);

        var memberAccess = await _sharing.GetAccessAsync(member, null, tripId, default);
        Assert.Equal(TripAccessLevel.Collaborator, memberAccess!.AccessLevel);
        Assert.Equal(owner, memberAccess.OwnerUserId);

        // No access returns nothing at all, so the API can deny without revealing the trip exists.
        Assert.Null(await _sharing.GetAccessAsync(stranger, "stranger@contoso.com", tripId, default));
    }

    /// <summary>
    /// Someone invited by email before they ever signed in has no stable id yet, so access has to
    /// resolve on the address the invitation was sent to.
    /// </summary>
    [Fact]
    public async Task GetAccess_MatchesAnInviteByEmailCaseInsensitively()
    {
        var owner = NewUser("owner");
        var tripId = await SeedTripAsync(owner);
        var invitedEmail = $"Invited.{Guid.NewGuid():N}@Contoso.com";

        await _sharing.UpsertShareAsync(
            owner, tripId,
            new UpsertTripShareRequest(NewUser("placeholder"), "Invited", invitedEmail, TripAccessLevel.Viewer),
            DateTimeOffset.UtcNow, default);

        var access = await _sharing.GetAccessAsync(NewUser("newly-signed-in"), invitedEmail.ToUpperInvariant(), tripId, default);

        Assert.NotNull(access);
        Assert.Equal(TripAccessLevel.Viewer, access!.AccessLevel);
    }

    [Fact]
    public async Task GetTripsPage_IncludesOwnedAndSharedTrips()
    {
        var owner = NewUser("owner");
        var member = NewUser("member");

        var ownedByMember = await SeedTripAsync(member, "Member's own");
        var sharedWithMember = await SeedTripAsync(owner, "Shared with member");
        await SeedTripAsync(owner, "Not shared");

        await _sharing.UpsertShareAsync(
            owner, sharedWithMember,
            new UpsertTripShareRequest(member, "Member", "member@contoso.com", TripAccessLevel.Viewer),
            DateTimeOffset.UtcNow, default);

        var page = await _reads.GetPageAsync(member, callerEmail: null, page: 1, pageSize: 50, default);
        var names = page.Trips.Select(t => t.Name).ToArray();

        Assert.Contains("Member's own", names);
        Assert.Contains("Shared with member", names);
        Assert.DoesNotContain("Not shared", names);

        // The shared one is not presented as theirs to control.
        var shared = page.Trips.Single(t => t.TripId == sharedWithMember);
        Assert.Equal(TripAccessLevel.Viewer, shared.AccessLevel);
        Assert.False(shared.IsOwner);

        var owned = page.Trips.Single(t => t.TripId == ownedByMember);
        Assert.Equal(TripAccessLevel.Owner, owned.AccessLevel);
        Assert.True(owned.IsOwner);
    }

    [Fact]
    public async Task DeleteShare_RemovesMemberAccess()
    {
        var owner = NewUser("owner");
        var member = NewUser("member");
        var tripId = await SeedTripAsync(owner);

        await _sharing.UpsertShareAsync(
            owner, tripId,
            new UpsertTripShareRequest(member, "Member", "member@contoso.com", TripAccessLevel.Collaborator),
            DateTimeOffset.UtcNow, default);

        Assert.NotNull(await _sharing.GetAccessAsync(member, null, tripId, default));

        // Someone other than the owner cannot revoke.
        Assert.Equal(0, await _sharing.DeleteShareAsync(NewUser("impostor"), tripId, member, default));
        Assert.NotNull(await _sharing.GetAccessAsync(member, null, tripId, default));

        Assert.Equal(1, await _sharing.DeleteShareAsync(owner, tripId, member, default));
        Assert.Null(await _sharing.GetAccessAsync(member, null, tripId, default));
        Assert.Empty(await GetMembersAsync(tripId));
    }
}
