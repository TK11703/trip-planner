using System.Threading.Channels;
using TripPlanner.Api.Features.Places;
using TripPlanner.Api.Features.TripMaps;
using TripPlanner.Contracts.Trips;

namespace TripPlanner.Api.Tests.TripMaps;

public class TripMapLocationResolverTests
{
    [Fact]
    public async Task ManyLocations_ResolveFourAtATime_PreserveOrderAndDeduplicate()
    {
        var items = Enumerable.Range(0, 12).Select(i => Item($"Place {i}")).ToArray();
        var duplicate = Item(" PLACE 0 ");
        var input = items.Concat([duplicate, Item(null), Item("?!"), Item(new string('a', 201))]).ToArray();
        var geocoder = new ControlledGeocoder();
        var resolver = new TripMapLocationResolver(geocoder);
        var task = resolver.ResolveAsync(input, CancellationToken.None);

        for (var batch = 0; batch < 3; batch++)
        {
            var calls = new List<Lookup>();
            for (var i = 0; i < 4; i++)
            {
                calls.Add(await geocoder.Started.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5)));
            }

            Assert.Equal((batch + 1) * 4, geocoder.CallCount);
            Assert.False(task.IsCompleted);
            foreach (var call in calls.AsEnumerable().Reverse())
            {
                call.Completion.SetResult(call.Query == "Place 5" ? null : new GeoPoint(20, -155));
            }
        }

        var response = await task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(12, geocoder.CallCount);
        Assert.Equal(
            items.Where(i => i.Location != "Place 5").Append(duplicate).Select(i => i.TrackedItemId),
            response.Locations.Select(i => i.TrackedItemId));
        Assert.Equal("PLACE 0", response.Locations[^1].Location);
        Assert.All(response.Locations, l => Assert.Equal(20, l.Latitude));
    }

    [Fact]
    public async Task Cancellation_StopsPendingLookupsAndDoesNotStartRemainingLocations()
    {
        var geocoder = new ControlledGeocoder();
        var resolver = new TripMapLocationResolver(geocoder);
        using var cancellation = new CancellationTokenSource();
        var task = resolver.ResolveAsync(
            Enumerable.Range(0, 20).Select(i => Item($"Place {i}")).ToArray(), cancellation.Token);
        for (var i = 0; i < 4; i++)
        {
            await geocoder.Started.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        }

        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(4, geocoder.CallCount);
    }

    private static TrackedItemDto Item(string? location)
        => new(Guid.NewGuid(), Guid.NewGuid(), null, "activity", "Visit", location,
            DateTime.Now, "UTC", DateTimeOffset.UtcNow, null, null, null, "blue", null, null, 0);

    private sealed record Lookup(string Query, TaskCompletionSource<GeoPoint?> Completion);

    private sealed class ControlledGeocoder : IPlaceGeocoder
    {
        private int _callCount;
        public int CallCount => Volatile.Read(ref _callCount);
        public bool IsConfigured => true;
        public Channel<Lookup> Started { get; } = Channel.CreateUnbounded<Lookup>();

        public async Task<GeoPoint?> GeocodeAsync(string query, CancellationToken ct)
        {
            Interlocked.Increment(ref _callCount);
            var completion = new TaskCompletionSource<GeoPoint?>(TaskCreationOptions.RunContinuationsAsynchronously);
            await Started.Writer.WriteAsync(new Lookup(query, completion), ct);
            return await completion.Task.WaitAsync(ct);
        }
    }
}
